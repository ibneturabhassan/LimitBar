using System.Runtime.InteropServices;
using System.Text;
using LimitBar.Core.Models;

namespace LimitBar.Desktop;

internal sealed class TaskbarOverlay : Form
{
    private readonly Label _codex;
    private readonly Label _claude;
    private readonly ToolTip _tip = new();
    private Point? _dragStart;
    private int _dragLeft;
    private bool _dragged;
    public bool IsDragging => _dragStart is not null;
    public double Position { get; set; } = 0.12;
    public event EventHandler? DetailsRequested;
    public event EventHandler? PositionChanged;
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var parameters = base.CreateParams; parameters.ExStyle |= 0x08000000 | 0x80; return parameters; }
    }

    public TaskbarOverlay()
    {
        Text = "LimitBar taskbar meter";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(270, 40);
        // Color-key only the background, keeping usage text fully opaque.
        BackColor = Color.FromArgb(1, 2, 3);
        TransparencyKey = BackColor;
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        AccessibleName = "LimitBar usage meter";
        _codex = MakeLabel(0, "Codex · checking", Color.FromArgb(86, 211, 183));
        _claude = MakeLabel(20, "Claude · checking", Color.FromArgb(231, 174, 126));
        foreach (var control in new Control[] { this, _codex, _claude })
        {
            control.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                _dragStart = Cursor.Position;
                _dragLeft = Left;
                _dragged = false;
                control.Capture = true;
            };
            control.MouseMove += (_, _) =>
            {
                if (_dragStart is not { } start) return;
                var delta = Cursor.Position.X - start.X;
                if (Math.Abs(delta) > SystemInformation.DragSize.Width) _dragged = true;
                if (!_dragged) return;
                var bar = TaskbarBounds();
                var span = Math.Max(1, bar.Width - Width);
                Position = Math.Clamp((double)(_dragLeft + delta - bar.Left) / span, 0, 1);
                AlignToTaskbar();
            };
            control.MouseUp += (_, e) =>
            {
                if (e.Button != MouseButtons.Left || _dragStart is null) return;
                _dragStart = null;
                control.Capture = false;
                if (_dragged) PositionChanged?.Invoke(this, EventArgs.Empty);
                else DetailsRequested?.Invoke(this, EventArgs.Empty);
            };
        }
    }

    private Label MakeLabel(int y, string text, Color color)
    {
        var label = new Label { Text = text, Location = new Point(8, y), Size = new Size(254, 20),
            TextAlign = ContentAlignment.MiddleLeft, ForeColor = color, AutoEllipsis = true };
        Controls.Add(label);
        return label;
    }

    public void SetMenu(ContextMenuStrip menu)
    {
        ContextMenuStrip = menu;
        _codex.ContextMenuStrip = menu;
        _claude.ContextMenuStrip = menu;
    }

    internal static string Caption(string name, UsageSnapshot? snapshot, DateTimeOffset now)
    {
        if (snapshot is null) return $"{name} · checking";
        if (snapshot.Status != UsageStatus.Available) return $"{name} · offline";
        var session = snapshot.Session?.RemainingPercent is not null ? snapshot.Session : null;
        var window = session ?? snapshot.Weekly;
        if (window?.ResetsAt <= now) return $"{name} · reset due";
        var weekly = snapshot.Weekly?.RemainingPercent is { } percent ? $"{percent:0}%" : "—";
        var sessionText = session is not null ? $" ({session.RemainingPercent:0}%)" : "";
        var countdown = "reset unavailable";
        if (window?.ResetsAt is { } reset)
        {
            var remaining = reset - now;
            countdown = remaining.TotalDays >= 1 ? $"{(int)remaining.TotalDays}d {remaining.Hours:00}h"
                : remaining.TotalHours >= 1 ? $"{(int)remaining.TotalHours}h {remaining.Minutes:00}m"
                : $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))}m";
        }
        return $"{name} {weekly}{sessionText} · {countdown}";
    }

    public void UpdateUsage(IReadOnlyList<UsageSnapshot> snapshots, bool refreshing)
    {
        _codex.Text = Caption("Codex", snapshots.FirstOrDefault(s => s.ProviderId == "openai"), DateTimeOffset.UtcNow);
        _claude.Text = Caption("Claude", snapshots.FirstOrDefault(s => s.ProviderId == "claude"), DateTimeOffset.UtcNow);
        AccessibleDescription = (refreshing ? "Refreshing. " : "Weekly remaining, session remaining in parentheses, then session reset countdown; weekly reset when no session is available. ") +
            "Hover for details. Drag to reposition. Right-click for settings and exit.";
    }

    public void Notify(string message)
    {
        if (Visible) _tip.Show(message, this, Width / 2, -45, 7000);
    }

    internal static Rectangle Place(Rectangle bar, Size size, double position)
    {
        if (!double.IsFinite(position)) position = 0.12;
        var width = Math.Min(size.Width, bar.Width);
        var height = Math.Min(size.Height, bar.Height);
        return new Rectangle(bar.Left + (int)((bar.Width - width) * Math.Clamp(position, 0, 1)),
            bar.Top + (bar.Height - height) / 2, width, height);
    }

    public void AlignToTaskbar()
    {
        var bar = TaskbarBounds();
        var screen = Screen.FromRectangle(bar).Bounds;
        var visibleBar = Rectangle.Intersect(bar, screen);
        // Respect taskbar auto-hide and full-screen applications; never cover games or video.
        var foreground = GetForegroundWindow();
        var className = new StringBuilder(128);
        GetClassName(foreground, className, className.Capacity);
        var fullscreen = foreground != Handle && foreground != FindWindow("Shell_TrayWnd", null) &&
            className.ToString() is not ("Progman" or "WorkerW") && GetWindowRect(foreground, out var front) &&
            front.Left <= screen.Left && front.Top <= screen.Top && front.Right >= screen.Right && front.Bottom >= screen.Bottom;
        if (visibleBar.Height < 12 || visibleBar.Width < 120 || fullscreen)
        {
            Hide();
            return;
        }
        Bounds = Place(visibleBar, new Size((int)(270 * DeviceDpi / 96f), (int)(40 * DeviceDpi / 96f)), Position);
        if (!Visible) Show();
        // Explorer can raise its own topmost taskbar after us. Reassert our order
        // without activating the overlay or stealing focus from the user's app.
        if (ContextMenuStrip?.Visible != true)
            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002 | 0x0040);
    }

    private static Rectangle TaskbarBounds()
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        return taskbar != IntPtr.Zero && GetWindowRect(taskbar, out var rect)
            ? Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom) : Rectangle.Empty;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
