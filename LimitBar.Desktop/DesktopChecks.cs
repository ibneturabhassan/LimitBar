using System.IO;
using LimitBar.Core.Models;

namespace LimitBar.Desktop;

internal static class DesktopChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var now = DateTimeOffset.UtcNow;
        var old = new UsageWindow { RemainingPercent = 0, ResetsAt = now.AddSeconds(-1) };
        var next = new UsageWindow { RemainingPercent = 100, ResetsAt = now.AddHours(5), WindowDuration = TimeSpan.FromHours(5) };
        Check(MeterForm.WindowText(old, now).Contains("awaiting"), "Elapsed reset must not imply replenished usage");
        Check(!MeterForm.ResetConfirmed(old, old, now), "Old data must not confirm a reset");
        Check(MeterForm.ResetConfirmed(old, next, now), "Fresh reset window should confirm reset");
        Check(MeterForm.WindowText(null, now) == "Not available", "Missing window must be unavailable");
        using var form = new MeterForm();
        Check(form.AllowsTransparency && form.WindowStyle == System.Windows.WindowStyle.None, "Popup preserves per-pixel alpha");
        var snapshots = new[]
        {
            new UsageSnapshot { ProviderId = "openai", ProviderName = "OpenAI", Status = UsageStatus.Available, Session = next with { RemainingPercent = 72 }, Weekly = next with { RemainingPercent = 41, ResetsAt = now.AddDays(3) }, RetrievedAt = now },
            new UsageSnapshot { ProviderId = "claude", ProviderName = "Claude", Status = UsageStatus.Available, Session = next with { RemainingPercent = 88 }, Weekly = next with { RemainingPercent = 67, ResetsAt = now.AddDays(5) }, RetrievedAt = now }
        };
        form.UpdateUsage(snapshots, new Dictionary<string, UsageSnapshot>(), false, now);
        form.Show();
        Application.DoEvents();
        form.UpdateLayout();
        var fullHeight = form.ActualHeight;
        CheckTextLayout((System.Windows.DependencyObject)form.Content);
        static void CheckTextLayout(System.Windows.DependencyObject parent)
        {
            if (parent is System.Windows.Controls.TextBlock text)
            {
                // Use the layout's original measurement. Remeasuring at the rounded
                // ActualWidth can wrap a label onto a second line on other font/DPI setups.
                var pixel = 1 / System.Windows.Media.VisualTreeHelper.GetDpi(text).DpiScaleY;
                Check(text.ActualHeight + pixel >= text.DesiredSize.Height - text.Margin.Top - text.Margin.Bottom,
                    "Text must have its complete measured height: " + text.Text);
            }
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                CheckTextLayout(System.Windows.Media.VisualTreeHelper.GetChild(parent, i));
        }
        Check(form.SessionRowCount == 2, "Available sessions are displayed");
        var image = new System.Windows.Media.Imaging.RenderTargetBitmap((int)form.ActualWidth, (int)form.ActualHeight,
            96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        image.Render(form);
        var pixels = new byte[(int)image.Width * (int)image.Height * 4];
        image.CopyPixels(pixels, (int)image.Width * 4, 0);
        var alpha = pixels[((int)image.Width * 10 + (int)image.Width / 2) * 4 + 3];
        Check(alpha > 0 && alpha < 255, "Card surface is actually translucent");
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using (var output = File.Create(Path.Combine(Environment.CurrentDirectory, "LimitBar.Desktop", "preview.png"))) encoder.Save(output);
        form.UpdateUsage(snapshots.Select(s => s with { Session = null }).ToArray(), new Dictionary<string, UsageSnapshot>(), false, now);
        form.UpdateLayout();
        Check(form.SessionRowCount == 0 && form.ActualHeight < fullHeight, "Missing sessions disappear and card shrinks");
        form.UpdateUsage(snapshots.Select(s => s with { Session = new UsageWindow() }).ToArray(), new Dictionary<string, UsageSnapshot>(), false, now);
        Check(form.SessionRowCount == 0, "Session without percentage is hidden");
        form.Hide();
        Check(TaskbarOverlay.Caption("Codex", snapshots[0], now) == "Codex 41% (72%) · 5h 00m", "Weekly then session, with session reset");
        Check(TaskbarOverlay.Caption("Codex", snapshots[0] with { Session = null }, now) == "Codex 41% · 3d 00h", "Weekly-only countdown with no parentheses");
        Check(TaskbarOverlay.Caption("Codex", snapshots[0] with { Weekly = null }, now) == "Codex — (72%) · 5h 00m", "Missing weekly data is not zero");
        Check(TaskbarOverlay.Caption("Codex", snapshots[0] with { Session = next with { ResetsAt = null } }, now).EndsWith("reset unavailable"), "Missing session reset is not substituted with weekly reset");
        Check(TaskbarOverlay.Caption("Codex", snapshots[0] with { Session = old }, now).Contains("reset due"), "Overlay reset is not falsely confirmed");
        Check(TaskbarOverlay.Caption("Codex", snapshots[0] with { Status = UsageStatus.TimedOut }, now).Contains("offline"), "Failure must not display a fresh percentage");
        var taskbar = new Rectangle(-1920, 1040, 1920, 40);
        var placed = TaskbarOverlay.Place(taskbar, new Size(268, 36), 2);
        Check(taskbar.Contains(placed) && placed.Right == taskbar.Right, "Placement clamps on negative-origin monitors");
        Check(taskbar.Contains(TaskbarOverlay.Place(taskbar, new Size(268, 36), double.NaN)), "Invalid saved position falls back safely");
        var workArea = new Rectangle(0, 0, 1920, 1040);
        var anchor = new Rectangle(500, 1040, 154, 40);
        var popupSize = new Size(380, 520);
        var popupPoint = MeterForm.PopupLocation(anchor, popupSize, workArea);
        Check(popupPoint == new Point(387, 512), "Popup is centered immediately above the overlay");
        Check(workArea.Contains(new Rectangle(MeterForm.PopupLocation(anchor with { X = 0 }, popupSize, workArea), popupSize)), "Popup stays inside left screen edge");
        Check(workArea.Contains(new Rectangle(MeterForm.PopupLocation(anchor with { X = 1880 }, popupSize, workArea), popupSize)), "Popup stays inside right screen edge");
        using var overlay = new TaskbarOverlay();
        Check(overlay.TransparencyKey == overlay.BackColor && overlay.Opacity == 1,
            "Overlay background is transparent without fading the usage text");
        overlay.UpdateUsage(snapshots, false);
        overlay.Show();
        Application.DoEvents();
        var rows = overlay.Controls.OfType<Label>().OrderBy(label => label.Top).ToArray();
        Check(rows.Length == 2 && rows[0].Left == rows[1].Left && rows[0].Bottom <= rows[1].Top,
            "Provider readings form two non-overlapping vertical rows");
        Check(!overlay.ShowInTaskbar && overlay.FormBorderStyle == FormBorderStyle.None, "Overlay has no taskbar app button or border");
        using var overlayBitmap = new Bitmap(overlay.Width, overlay.Height);
        overlay.DrawToBitmap(overlayBitmap, new Rectangle(Point.Empty, overlay.Size));
        overlayBitmap.Save(Path.Combine(Environment.CurrentDirectory, "LimitBar.Desktop", "overlay-preview.png"));
        overlay.Hide();
        File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "LimitBar.Desktop", "checks-result.txt"), $"Desktop checks passed: reset semantics, absent data, borderless popup, no pin button, overlay labels, stacked rows, anchored popup, bounded placement, form and overlay render. Per-pixel transparency and missing-session collapse verified.");
    }
}
