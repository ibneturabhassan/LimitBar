using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LimitBar.Core.Models;
using LimitBar.Core.Providers;
using LimitBar.Core.Services;
using LimitBar.Widget;

namespace LimitBar.Host;

class Program
{
    private static UsageService? _usageService;
    private static WidgetProvider? _widgetProvider;

    [MTAThread]
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting LimitBar Host...");

        // Setup Core Services
        var providers = new List<IUsageProvider>
        {
            new CodexUsageProvider(),
            new ClaudeUsageProvider()
        };
        _usageService = new UsageService(providers);

        // Init Widget Provider
        _widgetProvider = new WidgetProvider(_usageService);

        // Perform initial refresh
        Console.WriteLine("Performing initial refresh...");
        await _usageService.RefreshAsync(RefreshReason.Startup, CancellationToken.None);

        // Background loop for automatic refresh (every 5 minutes)
        using var cts = new CancellationTokenSource();
        var timerTask = RunRefreshLoopAsync(cts.Token);

        Console.WriteLine("LimitBar Host is running. Press Enter to exit.");

        // In a real Widget provider, we would register the COM class here using CoRegisterClassObject
        // so that the Windows Widget Board can instantiate WidgetProvider.
        // For example: WinRT.ComWrappersSupport.RegisterObjectForInterface(...)

        Console.ReadLine();
        cts.Cancel();
        await timerTask;
    }

    private static async Task RunRefreshLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), token);
                Console.WriteLine($"[{DateTime.Now:T}] Automatic refresh running...");
                await _usageService!.RefreshAsync(RefreshReason.Scheduled, token);

                // Fire and forget refresh
                _ = _widgetProvider!.DoRefreshAsync();
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during refresh loop: {ex.Message}");
            }
        }
    }
}
