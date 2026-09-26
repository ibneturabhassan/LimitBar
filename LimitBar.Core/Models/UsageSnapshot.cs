namespace LimitBar.Core.Models;

public sealed record UsageSnapshot
{
    public required string ProviderId { get; init; }

    public required string ProviderName { get; init; }

    public UsageWindow? Session { get; init; }

    public UsageWindow? Weekly { get; init; }

    public DateTimeOffset RetrievedAt { get; init; }

    public UsageStatus Status { get; init; }

    public string? ErrorMessage { get; init; }
}
