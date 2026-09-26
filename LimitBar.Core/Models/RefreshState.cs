using System;

namespace LimitBar.Core.Models;

public sealed record RefreshState
{
    public bool IsRefreshing { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public RefreshReason? Reason { get; init; }
}
