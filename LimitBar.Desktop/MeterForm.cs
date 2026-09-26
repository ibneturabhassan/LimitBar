using System.Runtime.InteropServices;
using LimitBar.Core.Models;
using W = System.Windows;
using C = System.Windows.Controls;
using M = System.Windows.Media;

namespace LimitBar.Desktop;

// WPF's layered window preserves per-pixel alpha; GDI's opaque backbuffer did not.
internal sealed class MeterForm : W.Window, IDisposable
{
    private readonly C.StackPanel _body = new();
    private readonly C.Border _card;
    private readonly M.Brush _muted = Brush("#BBC8DA");
    private readonly M.Brush _text = Brush("#F0F4FC");
    internal int SessionRowCount { get; private set; }
    public event EventHandler? RefreshRequested;

    public MeterForm()
    {
        Title = "LimitBar";
        Width = 380;
        SizeToContent = W.SizeToContent.Height;
        WindowStyle = W.WindowStyle.None;
        ResizeMode = W.ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = M.Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        FontFamily = new M.FontFamily("Segoe UI");
        UseLayoutRounding = true;
        _card = new C.Border
        {
            CornerRadius = new W.CornerRadius(18),
            Padding = new W.Thickness(24, 20, 24, 16),
            BorderThickness = new W.Thickness(1),
            BorderBrush = Brush("#608DA7CC"),
            Background = new M.LinearGradientBrush(M.Color.FromArgb(205, 29, 43, 65),
                M.Color.FromArgb(180, 15, 24, 39), 90),
            Child = _body
        };
        Content = _card;
        W.Automation.AutomationProperties.SetName(this, "LimitBar usage details");
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Hide(); };
    }

    private static M.Brush Brush(string color) => (M.Brush)new M.BrushConverter().ConvertFromString(color)!;

    private C.TextBlock Text(string text, double size, M.Brush color, bool bold = false) => new()
    {
        Text = text, FontSize = size, Foreground = color,
        FontWeight = bold ? W.FontWeights.SemiBold : W.FontWeights.Normal,
        TextWrapping = W.TextWrapping.Wrap,
        Margin = new W.Thickness(0, 0, 0, 3)
    };

    public void UpdateUsage(IReadOnlyList<UsageSnapshot> snapshots, IReadOnlyDictionary<string, UsageSnapshot> lastGood,
        bool refreshing, DateTimeOffset? lastCheck)
    {
        _body.Children.Clear();
        SessionRowCount = 0;
        var header = new C.DockPanel { Margin = new W.Thickness(0, 0, 0, 20) };
        var live = snapshots.Count > 0 && snapshots.All(s => s.Status == UsageStatus.Available);
        var status = Text(refreshing ? "Syncing" : live ? "● Live" : "Offline", 12, _muted);
        status.VerticalAlignment = W.VerticalAlignment.Center;
        C.DockPanel.SetDock(status, C.Dock.Right);
        header.Children.Add(status);
        header.Children.Add(Text("LimitBar", 26, _text, true));
        _body.Children.Add(header);
        AddProvider("openai", "Codex", Brush("#84EFCF"));
        Separator();
        AddProvider("claude", "Claude", Brush("#FFC296"));
        Separator();
        var footer = new C.DockPanel();
        var refresh = new C.Button
        {
            Content = "↻", FontSize = 24, Foreground = _text, Background = M.Brushes.Transparent,
            BorderThickness = new W.Thickness(0), Padding = new W.Thickness(8, 0, 8, 2),
            Cursor = System.Windows.Input.Cursors.Hand, IsEnabled = !refreshing, ToolTip = "Refresh usage"
        };
        W.Automation.AutomationProperties.SetName(refresh, "Refresh usage");
        refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        C.DockPanel.SetDock(refresh, C.Dock.Right);
        footer.Children.Add(refresh);
        var checkedText = Text(refreshing ? "Updating usage…" : lastCheck is { } check
            ? $"Checked {Math.Max(0, (int)(DateTimeOffset.UtcNow - check).TotalMinutes)}m ago"
            : "Waiting for first reading", 12, _muted);
        checkedText.VerticalAlignment = W.VerticalAlignment.Center;
        footer.Children.Add(checkedText);
        _body.Children.Add(footer);
        ToolTip = string.Join("\n", snapshots.Where(s => s.ErrorMessage is not null).Select(s => $"{s.ProviderName}: {s.ErrorMessage}"));
        UpdateLayout();

        void AddProvider(string id, string name, M.Brush accent)
        {
            var snapshot = snapshots.FirstOrDefault(s => s.ProviderId == id);
            var fresh = snapshot?.Status == UsageStatus.Available;
            lastGood.TryGetValue(id, out var cached);
            var shown = fresh ? snapshot : cached;
            _body.Children.Add(Text(name, 19, accent, true));
            if (!fresh) _body.Children.Add(Text(cached is null ? snapshot?.Status.ToString() ?? "Connecting" : "Stale reading", 12, _muted));
            AddWindow("Weekly", shown?.Weekly, fresh, accent);
            if (shown?.Session?.RemainingPercent is not null)
            {
                SessionRowCount++;
                var label = shown.Session.WindowDuration is { } duration ? $"{duration.TotalHours:0.#}-hour window" : "Session";
                AddWindow(label, shown.Session, fresh, accent);
            }
        }
    }

    private void Separator() => _body.Children.Add(new C.Border
    {
        Height = 1, Background = Brush("#405D7495"), Margin = new W.Thickness(0, 17, 0, 17)
    });

    private void AddWindow(string label, UsageWindow? window, bool fresh, M.Brush accent)
    {
        var row = new C.StackPanel { Margin = new W.Thickness(0, 12, 0, 0) };
        row.Children.Add(Text(label, 12, _muted));
        var now = DateTimeOffset.UtcNow;
        var due = window?.ResetsAt <= now;
        row.Children.Add(Text(due ? "Reset due" : window?.RemainingPercent is { } percent ? $"{percent:0.#}% left" : "Not available",
            19, fresh ? _text : _muted, true));
        if (window?.ResetsAt is { } at)
            row.Children.Add(Text(due ? "Awaiting fresh data" : at - now >= TimeSpan.FromDays(1)
                ? $"Resets {at.ToLocalTime():ddd, HH:mm}" : $"Resets in {Countdown(at - now)}", 12, _muted));
        var track = new C.Grid { Height = 4, Margin = new W.Thickness(0, 7, 0, 0) };
        track.Children.Add(new C.Border { Background = Brush("#60536883"), CornerRadius = new W.CornerRadius(2) });
        var remaining = fresh && !due ? Math.Clamp(window?.RemainingPercent ?? 0, 0, 100) : 0;
        track.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(remaining, W.GridUnitType.Star) });
        track.ColumnDefinitions.Add(new C.ColumnDefinition { Width = new W.GridLength(100 - remaining, W.GridUnitType.Star) });
        C.Grid.SetColumnSpan(track.Children[0], 2);
        if (remaining > 0) track.Children.Add(new C.Border { Background = accent, CornerRadius = new W.CornerRadius(2) });
        row.Children.Add(track);
        _body.Children.Add(row);
    }

    public Rectangle Bounds
    {
        get
        {
            GetWindowRect(new System.Windows.Interop.WindowInteropHelper(this).Handle, out var r);
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }
    }

    public void PlaceAbove(Rectangle anchor)
    {
        UpdateLayout();
        var scale = M.VisualTreeHelper.GetDpi(this);
        var point = PopupLocation(anchor, new Size((int)Math.Ceiling(ActualWidth * scale.DpiScaleX),
            (int)Math.Ceiling(ActualHeight * scale.DpiScaleY)), Screen.FromRectangle(anchor).WorkingArea, (int)(8 * scale.DpiScaleY));
        SetWindowPos(new System.Windows.Interop.WindowInteropHelper(this).Handle, new IntPtr(-1),
            point.X, point.Y, 0, 0, 0x0010 | 0x0001 | 0x0040);
    }

    public void Dispose() => Close();

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    private static string Countdown(TimeSpan until) => until.TotalHours >= 1
        ? $"{(int)until.TotalHours}h {until.Minutes:00}m" : $"{Math.Max(1, (int)Math.Ceiling(until.TotalMinutes))}m";

    internal static Point PopupLocation(Rectangle anchor, Size popup, Rectangle workArea, int gap = 8)
    {
        var x = anchor.Left + (anchor.Width - popup.Width) / 2;
        var y = anchor.Top - popup.Height - gap;
        return new Point(Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - popup.Width)),
            Math.Clamp(y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - popup.Height)));
    }

    internal static bool ResetConfirmed(UsageWindow? previous, UsageWindow? current, DateTimeOffset now) =>
        previous?.ResetsAt is { } oldReset && oldReset <= now &&
        current?.ResetsAt is { } newReset && newReset > now && newReset > oldReset && current.RemainingPercent.HasValue;

    internal static string WindowText(UsageWindow? window, DateTimeOffset now)
    {
        if (window?.RemainingPercent is not { } remaining) return "Not available";
        if (window.ResetsAt is not { } reset) return $"{remaining:0.#}% left · reset unavailable";
        if (reset <= now) return "Reset due · awaiting fresh data";
        var until = reset - now;
        return $"{remaining:0.#}% left · resets in {(until.TotalDays >= 1 ? $"{(int)until.TotalDays}d {until.Hours}h" : Countdown(until))}";
    }

}
