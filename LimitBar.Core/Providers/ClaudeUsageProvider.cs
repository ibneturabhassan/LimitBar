using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using LimitBar.Core.Models;

namespace LimitBar.Core.Providers;

public class ClaudeUsageProvider : IUsageProvider
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(20) };
    private readonly string _credentialsPath;
    private readonly HttpClient _client;

    public ClaudeUsageProvider() : this(Path.Combine(
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"),
        ".credentials.json"), Client) { }

    internal ClaudeUsageProvider(string credentialsPath, HttpClient client)
    {
        _credentialsPath = credentialsPath;
        _client = client;
    }

    public string Id => "claude";
    public string DisplayName => "Claude";

    public async Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        var snapshot = await GetUsageAsync(cancellationToken);
        return snapshot.Status == UsageStatus.NotAuthenticated
            ? ProviderAvailability.NotAuthenticated : ProviderAvailability.Available;
    }

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!File.Exists(_credentialsPath))
                return Failure(UsageStatus.NotAuthenticated, "Sign in with claude auth login first.");
            using var credentials = JsonDocument.Parse(await File.ReadAllTextAsync(_credentialsPath, cancellationToken));
            if (!credentials.RootElement.TryGetProperty("claudeAiOauth", out var oauth) ||
                oauth.ValueKind != JsonValueKind.Object ||
                !oauth.TryGetProperty("accessToken", out var access) || string.IsNullOrWhiteSpace(access.GetString()))
                return Failure(UsageStatus.NotAuthenticated, "Claude subscription credentials are missing. Run claude auth login.");
            // ponytail: let Claude own token refresh; add a supported refresh API when one exists.
            if (oauth.TryGetProperty("expiresAt", out var expiry) && expiry.ValueKind == JsonValueKind.Number &&
                expiry.GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                return Failure(UsageStatus.NotAuthenticated, "Claude sign-in expired. Open Claude Code to refresh it, then retry.");

            // Internal endpoint used by Claude Code; isolated here because its schema can change.
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.GetString());
            request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
            using var response = await _client.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return Failure(UsageStatus.NotAuthenticated, "Claude rejected the sign-in. Run claude auth login.");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return Failure(UsageStatus.UnknownError, "Claude usage requests are rate-limited. Wait before refreshing.");
            if (!response.IsSuccessStatusCode)
                return Failure(UsageStatus.UnknownError, $"Claude usage request failed (HTTP {(int)response.StatusCode}).");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return Parse(document.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(UsageStatus.TimedOut, "Claude usage query timed out.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentOutOfRangeException)
        {
            return Failure(UsageStatus.ParseError, "Claude credentials or usage response have an unrecognized format.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException)
        {
            return Failure(UsageStatus.UnknownError, "Unable to read Claude usage. Check credential file access and network connectivity.");
        }
    }

    internal UsageSnapshot Parse(JsonElement root)
    {
        var session = ReadWindow(root, "five_hour", TimeSpan.FromHours(5));
        var weekly = ReadWindow(root, "seven_day", TimeSpan.FromDays(7));
        return session is null && weekly is null
            ? Failure(UsageStatus.UnsupportedVersion, "This Claude account did not return usage windows.")
            : new UsageSnapshot { ProviderId = Id, ProviderName = DisplayName, Session = session,
                Weekly = weekly, RetrievedAt = DateTimeOffset.UtcNow, Status = UsageStatus.Available };
    }

    private static UsageWindow? ReadWindow(JsonElement root, string name, TimeSpan duration)
    {
        if (!root.TryGetProperty(name, out var window) || window.ValueKind == JsonValueKind.Null) return null;
        var used = window.GetProperty("utilization").GetDouble();
        if (!double.IsFinite(used) || used < 0) throw new JsonException();
        return new UsageWindow
        {
            UsedPercent = used, RemainingPercent = Math.Clamp(100 - used, 0, 100), WindowDuration = duration,
            ResetsAt = window.TryGetProperty("resets_at", out var reset) && reset.ValueKind != JsonValueKind.Null
                ? reset.GetDateTimeOffset() : null
        };
    }

    private UsageSnapshot Failure(UsageStatus status, string message) => new()
    {
        ProviderId = Id, ProviderName = DisplayName, Status = status,
        RetrievedAt = DateTimeOffset.UtcNow, ErrorMessage = message
    };
}
