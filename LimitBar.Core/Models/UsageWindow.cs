namespace LimitBar.Core.Models;

public sealed record UsageWindow
{
    public double? UsedPercent { get; init; }

    public double? RemainingPercent { get; init; }

    public DateTimeOffset? ResetsAt { get; init; }

    public TimeSpan? WindowDuration { get; init; }
}
