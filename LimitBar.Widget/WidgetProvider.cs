using System;
using System.Collections.Generic;
using Microsoft.Windows.Widgets.Providers;
using LimitBar.Core.Models;
using LimitBar.Core.Services;
using System.Threading;
using System.Threading.Tasks;

namespace LimitBar.Widget;

// In a real MSIX, this would be registered as the COM server for the widget.
public class WidgetProvider : IWidgetProvider
{
    private readonly UsageService _usageService;
    private readonly WidgetRenderer _renderer;
    private readonly Dictionary<string, string> _activeWidgets = new();

    private RefreshState _refreshState = new RefreshState();

    public WidgetProvider(UsageService usageService)
    {
        _usageService = usageService;
        _renderer = new WidgetRenderer();
    }

    public void CreateWidget(WidgetContext widgetContext)
    {
        _activeWidgets[widgetContext.Id] = widgetContext.Id;
        UpdateWidget(widgetContext.Id);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        _activeWidgets.Remove(widgetId);
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        if (actionInvokedArgs.Verb == "refresh")
        {
            // Trigger manual refresh
            _ = DoRefreshAsync();
        }
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        UpdateWidget(contextChangedArgs.WidgetContext.Id);
    }

    public void Activate(WidgetContext widgetContext)
    {
        UpdateWidget(widgetContext.Id);
    }

    public void Deactivate(string widgetId)
    {
    }

    private void UpdateWidget(string widgetId)
    {
        var dataJson = _renderer.GetDataJson(_usageService.GetCachedSnapshots(), _refreshState);

        var updateOptions = new WidgetUpdateRequestOptions(widgetId)
        {
            Template = WidgetTemplates.MainTemplate,
            Data = dataJson
        };

        try
        {
            WidgetManager.GetDefault().UpdateWidget(updateOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update widget: {ex.Message}");
        }
    }

    private void UpdateAllWidgets()
    {
        foreach (var widgetId in _activeWidgets.Keys)
        {
            UpdateWidget(widgetId);
        }
    }

    public async Task DoRefreshAsync()
    {
        if (_refreshState.IsRefreshing) return;

        _refreshState = new RefreshState
        {
            IsRefreshing = true,
            StartedAt = DateTimeOffset.Now,
            Reason = RefreshReason.Manual
        };
        UpdateAllWidgets();

        await _usageService.RefreshAsync(RefreshReason.Manual, CancellationToken.None);

        _refreshState = new RefreshState
        {
            IsRefreshing = false,
            StartedAt = _refreshState.StartedAt,
            CompletedAt = DateTimeOffset.Now,
            Reason = RefreshReason.Manual
        };
        UpdateAllWidgets();
    }
}
