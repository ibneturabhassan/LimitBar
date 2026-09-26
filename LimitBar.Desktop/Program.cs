using System.IO;
using System.Text.Json;
using LimitBar.Core.Models;
using LimitBar.Core.Providers;
using LimitBar.Core.Services;
using Microsoft.Win32;

namespace LimitBar.Desktop;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--self-test")) { DesktopChecks.Run(); return; }
        if (args.Contains("--screenshots")) { DemoAssets.Export(); return; }
        using var instance = new Mutex(true, "Local\\LimitBar.Desktop", out var first);
        if (!first) { MessageBox.Show("LimitBar is already running. Click the taskbar meter to open it.", "LimitBar"); return; }
        using var context = new DesktopApp();
        Application.Run(context);
    }
}

internal sealed class Preferences
{
    public double OverlayPosition { get; set; } = 0.12;
    public bool Notifications { get; set; } = true;
}

internal sealed class DesktopApp : ApplicationContext
{
    private readonly UsageService _service = new(new IUsageProvider[] { new CodexUsageProvider(), new ClaudeUsageProvider() });
    private readonly MeterForm _form = new();
    private readonly TaskbarOverlay _overlay = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _hoverTimer = new() { Interval = 100 };
    private long? _hoverStarted;
    private long? _leftPopup;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Dictionary<string, UsageSnapshot> _good = new();
    private readonly HashSet<string> _resetChecks = new();
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LimitBar", "settings.json");
    private readonly Preferences _preferences;
    private IReadOnlyList<UsageSnapshot> _snapshots = Array.Empty<UsageSnapshot>();
    private DateTimeOffset _nextRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset _lastTick = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastCheck;
    private bool _refreshing;
    private bool _exiting;
    private int _failures;

    public DesktopApp()
    {
        try { _preferences = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(_settingsPath)) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { _preferences = new(); }
        _form.RefreshRequested += async (_, _) => await RefreshAsync();
        _form.Closing += (_, e) => { if (!_exiting) { e.Cancel = true; _form.Hide(); Save(); } };
        _form.Deactivated += (_, _) => _form.Hide();
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show meter", null, (_, _) => Show());
        menu.Items.Add("Refresh now", null, async (_, _) => await RefreshAsync());
        var notifications = new ToolStripMenuItem("Notifications") { Checked = _preferences.Notifications, CheckOnClick = true };
        notifications.CheckedChanged += (_, _) => { _preferences.Notifications = notifications.Checked; Save(); };
        menu.Items.Add(notifications);
        var startup = new ToolStripMenuItem("Start with Windows") { Checked = IsStartupEnabled() };
        startup.Click += (_, _) =>
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (startup.Checked) key.DeleteValue("LimitBar", false);
                else key.SetValue("LimitBar", $"\"{Environment.ProcessPath}\" --background");
                startup.Checked = !startup.Checked;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            { MessageBox.Show("Could not change startup settings.", "LimitBar"); }
        };
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit LimitBar", null, async (_, _) =>
        {
            _exiting = true;
            _timer.Stop();
            _hoverTimer.Stop();
            _shutdown.Cancel();
            // Keep the UI message loop alive while providers cancel and stop child processes.
            while (_refreshing) await Task.Delay(50);
            ExitThread();
        });
        menu.Items.Insert(1, new ToolStripMenuItem("Reset taskbar position", null, (_, _) => { _overlay.Position = 0.12; _overlay.AlignToTaskbar(); Save(); }));
        _overlay.Position = _preferences.OverlayPosition;
        _overlay.SetMenu(menu);
        _overlay.DetailsRequested += (_, _) => Show();
        _overlay.PositionChanged += (_, _) => Save();
        _overlay.AlignToTaskbar();
        _timer.Tick += async (_, _) =>
        {
            _overlay.AlignToTaskbar();
            var now = DateTimeOffset.UtcNow;
            var resumed = now - _lastTick > TimeSpan.FromSeconds(30);
            _lastTick = now;
            if (!_refreshing && (resumed || now >= _nextRefresh || ResetDue(now))) await RefreshAsync();
            if (!_exiting) Render();
        };
        _timer.Start();
        _hoverTimer.Tick += (_, _) => UpdateHover();
        _hoverTimer.Start();
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("LimitBar") is string;
    }

    private bool ResetDue(DateTimeOffset now)
    {
        if (_failures > 0) return false;
        var due = false;
        foreach (var snapshot in _snapshots.Where(s => s.Status == UsageStatus.Available))
            foreach (var window in new[] { snapshot.Session, snapshot.Weekly })
                if (window?.ResetsAt is { } reset && reset <= now)
                    due |= _resetChecks.Add($"{snapshot.ProviderId}:{reset.ToUnixTimeSeconds()}");
        return due;
    }

    private async Task RefreshAsync()
    {
        if (_refreshing || _exiting) return;
        _refreshing = true;
        Render();
        try
        {
            var result = await _service.RefreshAsync(RefreshReason.Scheduled, _shutdown.Token);
            if (_exiting) return;
            foreach (var snapshot in result.Snapshots.Where(s => s.Status == UsageStatus.Available))
            {
                if (_good.TryGetValue(snapshot.ProviderId, out var previous) && _preferences.Notifications)
                {
                    NotifyWindow(snapshot.ProviderName, "short window", previous.Session, snapshot.Session);
                    NotifyWindow(snapshot.ProviderName, "weekly window", previous.Weekly, snapshot.Weekly);
                }
                _good[snapshot.ProviderId] = snapshot;
            }
            _snapshots = result.Snapshots;
            _lastCheck = DateTimeOffset.UtcNow;
            _failures = result.Snapshots.Any(s => s.Status != UsageStatus.Available) ? Math.Min(_failures + 1, 3) : 0;
            _nextRefresh = DateTimeOffset.UtcNow.AddMinutes(3 * Math.Pow(2, _failures));
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        finally { _refreshing = false; if (!_exiting) Render(); }
    }

    private void NotifyWindow(string provider, string label, UsageWindow? old, UsageWindow? current)
    {
        if (MeterForm.ResetConfirmed(old, current, DateTimeOffset.UtcNow))
            _overlay.Notify($"{provider}: {label} reset confirmed.");
        else if (old?.RemainingPercent > 10 && current?.RemainingPercent <= 10)
            _overlay.Notify($"{provider}: {current.RemainingPercent:0}% remaining in the {label}.");
    }

    private void Render()
    {
        _form.UpdateUsage(_snapshots, _good, _refreshing, _lastCheck);
        _overlay.UpdateUsage(_snapshots, _refreshing);
        if (_form.IsVisible) _form.PlaceAbove(_overlay.Bounds);
    }

    private void UpdateHover()
    {
        if (_exiting) return;
        if (_overlay.IsDragging || _overlay.ContextMenuStrip?.Visible == true)
        {
            _hoverStarted = _leftPopup = null;
            _form.Hide();
            return;
        }
        var overOverlay = _overlay.Visible && _overlay.Bounds.Contains(Cursor.Position);
        var overPopup = _form.IsVisible && _form.Bounds.Contains(Cursor.Position);
        var now = Environment.TickCount64;
        if (overOverlay || overPopup)
        {
            _leftPopup = null;
            if (overOverlay && !_form.IsVisible)
            {
                _hoverStarted ??= now;
                if (now - _hoverStarted >= 250) Show(activate: false);
            }
        }
        else
        {
            _hoverStarted = null;
            _leftPopup ??= now;
            // Allow the pointer to cross the small gap into the popup.
            if (now - _leftPopup >= 350) _form.Hide();
        }
    }

    private void Show(bool activate = true)
    {
        _form.Show();
        _form.PlaceAbove(_overlay.Bounds);
        if (activate) _form.Activate();
    }

    private void Save()
    {
        _preferences.OverlayPosition = _overlay.Position;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_preferences));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    protected override void ExitThreadCore()
    {
        _exiting = true;
        Save();
        _timer.Stop();
        _hoverTimer.Stop();
        _shutdown.Cancel();
        _overlay.Close();
        _form.Close();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _hoverTimer.Dispose(); _timer.Dispose(); _overlay.ContextMenuStrip?.Dispose(); _overlay.Dispose(); _form.Dispose(); _shutdown.Dispose(); }
        base.Dispose(disposing);
    }
}
