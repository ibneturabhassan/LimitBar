using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LimitBar.Core.Models;
using LimitBar.Core.Providers;
using LimitBar.Core.Services;

namespace LimitBar.Cli;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("LimitBar\n");

        var providers = new List<IUsageProvider>
        {
            new CodexUsageProvider(),
            new ClaudeUsageProvider()
        };

        var usageService = new UsageService(providers);

        using var cts = new CancellationTokenSource();
        var result = await usageService.RefreshAsync(RefreshReason.Manual, cts.Token);

        foreach (var snapshot in result.Snapshots)
        {
            Console.WriteLine(snapshot.ProviderName);

            if (snapshot.Status != UsageStatus.Available)
            {
                Console.WriteLine($"Status: {snapshot.Status}");
                if (!string.IsNullOrEmpty(snapshot.ErrorMessage))
                {
                    Console.WriteLine($"Error: {snapshot.ErrorMessage}");
                }
            }
            else
            {
                if (snapshot.Session != null)
                {
                    Console.WriteLine($"Session: {snapshot.Session.RemainingPercent}% remaining");
                }

                if (snapshot.Weekly != null)
                {
                    Console.WriteLine($"Weekly: {snapshot.Weekly.RemainingPercent}% remaining");
                }

                if (snapshot.Session?.ResetsAt != null)
                {
                    var resetIn = snapshot.Session.ResetsAt.Value - DateTimeOffset.Now;
                    if (resetIn < TimeSpan.Zero) resetIn = TimeSpan.Zero;
                    var hours = Math.Floor(resetIn.TotalHours);
                    var minutes = resetIn.Minutes;
                    Console.WriteLine($"Session reset: {hours}h {minutes}m");
                }

                if (snapshot.Weekly?.ResetsAt != null)
                {
                    var weeklyReset = snapshot.Weekly.ResetsAt.Value.ToLocalTime();
                    Console.WriteLine($"Weekly reset: {weeklyReset.ToString("ddd HH:mm")}");
                }
            }

            Console.WriteLine();
        }
    }
}
