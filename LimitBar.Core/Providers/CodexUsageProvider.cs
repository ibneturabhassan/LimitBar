using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using LimitBar.Core.Models;

namespace LimitBar.Core.Providers;

public class CodexUsageProvider : IUsageProvider
{
    public string Id => "openai";
    public string DisplayName => "OpenAI";

    public async Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        var snapshot = await GetUsageAsync(cancellationToken);
        return snapshot.Status switch
        {
            UsageStatus.CliNotInstalled => ProviderAvailability.NotInstalled,
            UsageStatus.NotAuthenticated => ProviderAvailability.NotAuthenticated,
            _ => ProviderAvailability.Available
        };
    }

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var start = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "codex",
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        if (OperatingSystem.IsWindows())
        {
            // Resolve native installs and npm's shim without interpolating shell input.
            foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
                "if (-not (Get-Command codex -ErrorAction SilentlyContinue)) { exit 127 }; & codex app-server" })
                start.ArgumentList.Add(arg);
        }
        else start.ArgumentList.Add("app-server");

        using var process = new Process { StartInfo = start };
        var started = false;
        try
        {
            started = process.Start();
            // Drain diagnostics without exposing raw server output or account information.
            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();
            await SendAsync(new { id = 0, method = "initialize", @params = new
                { clientInfo = new { name = "limitbar", version = "0.1.0" } } });
            await ReadAsync(0);
            await SendAsync(new { method = "initialized" });
            await SendAsync(new { id = 1, method = "account/read", @params = new { refreshToken = false } });
            var account = await ReadAsync(1);
            if (!account.TryGetProperty("account", out var identity) || identity.ValueKind == JsonValueKind.Null)
                return Failure(UsageStatus.NotAuthenticated, "Sign in with codex login first.");
            if (identity.GetProperty("type").GetString() == "apiKey")
                return Failure(UsageStatus.UnsupportedVersion, "Usage windows require a ChatGPT account, not API-key authentication.");
            await SendAsync(new { id = 2, method = "account/rateLimits/read" });
            return Parse(await ReadAsync(2));

            async Task SendAsync(object message)
            {
                await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), timeout.Token);
                await process.StandardInput.FlushAsync(timeout.Token);
            }

            async Task<JsonElement> ReadAsync(int id)
            {
                while (await process.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    if (!root.TryGetProperty("id", out var responseId) ||
                        responseId.ValueKind != JsonValueKind.Number || responseId.GetInt32() != id) continue;
                    if (root.TryGetProperty("error", out _))
                        throw new InvalidOperationException("Codex account query failed.");
                    return root.GetProperty("result").Clone();
                }
                await process.WaitForExitAsync(timeout.Token);
                if (process.ExitCode == 127) throw new Win32Exception("Codex is not installed.");
                throw new IOException("Codex app server exited before responding.");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(UsageStatus.TimedOut, "Codex usage query timed out after 20 seconds.");
        }
        catch (Win32Exception)
        {
            return Failure(UsageStatus.CliNotInstalled, "Install Codex CLI and make it available on PATH.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or ArgumentOutOfRangeException)
        {
            return Failure(UsageStatus.ParseError, "Codex returned an unrecognized usage response.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return Failure(UsageStatus.UnknownError, "Codex usage query failed. Check CLI version, login status, and network access.");
        }
        finally
        {
            if (started)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }
        }
    }

    internal UsageSnapshot Parse(JsonElement result)
    {
        result.TryGetProperty("rateLimits", out var limits);
        if (result.TryGetProperty("rateLimitsByLimitId", out var buckets) &&
            buckets.ValueKind == JsonValueKind.Object && buckets.TryGetProperty("codex", out var codex))
            limits = codex;
        var primary = ReadWindow(limits, "primary");
        var secondary = ReadWindow(limits, "secondary");
        // Accounts can return a weekly primary window with no secondary window.
        var session = primary?.WindowDuration >= TimeSpan.FromDays(7) ? secondary : primary;
        var weekly = primary?.WindowDuration >= TimeSpan.FromDays(7) ? primary : secondary;
        return session is null && weekly is null
            ? Failure(UsageStatus.UnsupportedVersion, "This account did not return usage windows.")
            : new UsageSnapshot { ProviderId = Id, ProviderName = DisplayName, Session = session,
                Weekly = weekly, RetrievedAt = DateTimeOffset.UtcNow, Status = UsageStatus.Available };
    }

    private static UsageWindow? ReadWindow(JsonElement limits, string name)
    {
        if (limits.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || !limits.TryGetProperty(name, out var window) ||
            window.ValueKind == JsonValueKind.Null) return null;
        var used = window.GetProperty("usedPercent").GetDouble();
        if (!double.IsFinite(used) || used < 0) throw new JsonException();
        return new UsageWindow
        {
            UsedPercent = used, RemainingPercent = Math.Clamp(100 - used, 0, 100),
            ResetsAt = window.TryGetProperty("resetsAt", out var reset) && reset.ValueKind != JsonValueKind.Null
                ? DateTimeOffset.FromUnixTimeSeconds(reset.GetInt64()) : null,
            WindowDuration = window.TryGetProperty("windowDurationMins", out var duration) && duration.ValueKind != JsonValueKind.Null
                ? TimeSpan.FromMinutes(duration.GetInt32()) : null
        };
    }

    private UsageSnapshot Failure(UsageStatus status, string message) => new()
    {
        ProviderId = Id, ProviderName = DisplayName, Status = status,
        RetrievedAt = DateTimeOffset.UtcNow, ErrorMessage = message
    };
}
