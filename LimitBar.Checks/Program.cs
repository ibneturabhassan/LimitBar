using System.Net;
using System.Text.Json;
using LimitBar.Core.Models;
using LimitBar.Core.Providers;
using LimitBar.Core.Services;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"Failed: {name}");
}

using var codexData = JsonDocument.Parse("""
{"rateLimits":{"primary":{"usedPercent":99}},"rateLimitsByLimitId":{"codex":{
"primary":{"usedPercent":25,"windowDurationMins":300,"resetsAt":1800000000},
"secondary":{"usedPercent":110,"windowDurationMins":10080,"resetsAt":null}}}}
""");
var codex = new CodexUsageProvider().Parse(codexData.RootElement);
Check(codex.Session?.RemainingPercent == 75 && codex.Weekly?.RemainingPercent == 0,
    "Codex bucket preference and over-limit clamp");
Check(codex.Session?.ResetsAt == DateTimeOffset.FromUnixTimeSeconds(1800000000) &&
    codex.Weekly?.WindowDuration == TimeSpan.FromDays(7), "Codex time units");
using var legacy = JsonDocument.Parse("""{"rateLimits":{"primary":null,"secondary":null}}""");
Check(new CodexUsageProvider().Parse(legacy.RootElement).Status == UsageStatus.UnsupportedVersion,
    "Absent limits are not zero usage");

using var weeklyOnly = JsonDocument.Parse("""{"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":1,"windowDurationMins":10080}}}}""");
var weeklySnapshot = new CodexUsageProvider().Parse(weeklyOnly.RootElement);
Check(weeklySnapshot.Session is null && weeklySnapshot.Weekly?.RemainingPercent == 99,
    "Weekly primary window is labeled weekly, including map-only responses");
using var rendered = JsonDocument.Parse(new LimitBar.Widget.WidgetRenderer().GetDataJson(
    new[] { weeklySnapshot }, new RefreshState()));
Check(rendered.RootElement.GetProperty("providers")[0].GetProperty("sessionText").GetString() == "N/A",
    "Missing session is not rendered as zero usage");

var credentials = Path.Combine(Path.GetTempPath(), $"limitbar-check-{Guid.NewGuid():N}.json");
var handler = new StubHttp();
using var client = new HttpClient(handler);
var claude = new ClaudeUsageProvider(credentials, client);
try
{
    Check((await claude.GetUsageAsync(default)).Status == UsageStatus.NotAuthenticated, "Missing credentials");
    await File.WriteAllTextAsync(credentials, """{"claudeAiOauth":{"accessToken":"test-only","expiresAt":1}}""");
    Check((await claude.GetUsageAsync(default)).Status == UsageStatus.NotAuthenticated && handler.Calls == 0,
        "Expired credentials never sent");
    await File.WriteAllTextAsync(credentials, """{"claudeAiOauth":{"accessToken":"test-only","expiresAt":4102444800000}}""");
    var snapshot = await claude.GetUsageAsync(default);
    Check(snapshot.Session?.RemainingPercent == 87.5 && snapshot.Weekly is null,
        "Claude percentage and nullable window");
    Check(snapshot.Session?.ResetsAt == DateTimeOffset.Parse("2026-10-01T12:00:00Z"), "Claude reset date");
    handler.Status = HttpStatusCode.Unauthorized;
    Check((await claude.GetUsageAsync(default)).Status == UsageStatus.NotAuthenticated, "HTTP authentication failure");
    handler.Status = HttpStatusCode.TooManyRequests;
    Check((await claude.GetUsageAsync(default)).ErrorMessage!.Contains("rate-limited"), "HTTP throttling");
    handler.Status = HttpStatusCode.OK;
    handler.Body = """{"five_hour":{"utilization":"bad"}}""";
    Check((await claude.GetUsageAsync(default)).Status == UsageStatus.ParseError, "Malformed usage");
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try
    {
        await new UsageService(new[] { claude }).RefreshAsync(RefreshReason.Manual, cancellation.Token);
        throw new Exception("Cancellation was swallowed");
    }
    catch (OperationCanceledException) { }
}
finally { File.Delete(credentials); }
Console.WriteLine("All usage provider checks passed (offline; no account credentials read).");

sealed class StubHttp : HttpMessageHandler
{
    public int Calls { get; private set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public string Body { get; set; } = """
        {"five_hour":{"utilization":12.5,"resets_at":"2026-10-01T12:00:00Z"},"seven_day":null}
        """;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (request.RequestUri?.AbsoluteUri != "https://api.anthropic.com/api/oauth/usage" ||
            request.Headers.Authorization?.Parameter != "test-only" ||
            request.Headers.GetValues("anthropic-beta").Single() != "oauth-2025-04-20")
            throw new Exception("Incorrect usage request");
        Calls++;
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body) });
    }
}
