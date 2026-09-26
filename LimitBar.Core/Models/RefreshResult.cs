using System.Collections.Generic;

namespace LimitBar.Core.Models;

public sealed record RefreshResult
{
    public required IReadOnlyList<UsageSnapshot> Snapshots { get; init; }
}
