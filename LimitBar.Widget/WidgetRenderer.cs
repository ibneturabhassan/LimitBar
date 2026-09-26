using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using LimitBar.Core.Models;

namespace LimitBar.Widget;

public class WidgetRenderer
{
    public string GetDataJson(IReadOnlyList<UsageSnapshot> snapshots, RefreshState refreshState)
    {
        var providersData = snapshots.Select(s => new
        {
            providerName = s.ProviderName,
            sessionText = s.Status == UsageStatus.Available
                ? FormatWindow(s.Session) : s.Status.ToString(),
            weeklyText = s.Status == UsageStatus.Available ? FormatWindow(s.Weekly) : "",
            resetText = s.Status == UsageStatus.Available
                ? $"Session reset: {GetResetString(s.Session?.ResetsAt)} | Weekly: {GetResetString(s.Weekly?.ResetsAt)}"
                : ""
        });

        var lastUpdated = refreshState.IsRefreshing
            ? "Refreshing..."
            : $"Updated {(refreshState.CompletedAt.HasValue ? GetRelativeTime(refreshState.CompletedAt.Value) : "Never")}";

        var dataObject = new
        {
            providers = providersData,
            lastUpdated = lastUpdated
        };

        return JsonSerializer.Serialize(dataObject);
    }

    private string FormatWindow(UsageWindow? window) => window?.RemainingPercent is { } remaining
        ? GenerateProgressBar(remaining) + $"  {remaining}%" : "N/A";

    private string GenerateProgressBar(double remainingPercent)
    {
        int totalBlocks = 12;
        int filledBlocks = (int)Math.Round((Math.Clamp(remainingPercent, 0, 100) / 100.0) * totalBlocks);
        return new string('█', filledBlocks) + new string('░', totalBlocks - filledBlocks);
    }

    private string GetResetString(DateTimeOffset? resetsAt)
    {
        if (!resetsAt.HasValue) return "N/A";

        var diff = resetsAt.Value - DateTimeOffset.Now;
        if (diff <= TimeSpan.Zero) return "awaiting refresh";
        if (diff.TotalHours < 24)
        {
            return $"{(int)diff.TotalHours}h {diff.Minutes}m";
        }
        return resetsAt.Value.ToLocalTime().ToString("ddd HH:mm");
    }

    private string GetRelativeTime(DateTimeOffset time)
    {
        var diff = DateTimeOffset.Now - time;
        if (diff.TotalMinutes < 1) return "just now";
        return $"{(int)diff.TotalMinutes} min ago";
    }
}
