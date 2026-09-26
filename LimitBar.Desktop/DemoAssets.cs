using System.IO;
using LimitBar.Core.Models;
using W = System.Windows;
using C = System.Windows.Controls;
using M = System.Windows.Media;
using I = System.Windows.Media.Imaging;

namespace LimitBar.Desktop;

// Render the real controls with fictional readings. Never query accounts for public assets.
internal static class DemoAssets
{
    public static void Export()
    {
        var output = Path.Combine(Environment.CurrentDirectory, "docs", "assets");
        Directory.CreateDirectory(output);
        var now = DateTimeOffset.UtcNow;
        var session = new UsageWindow { RemainingPercent = 72, ResetsAt = now.AddHours(2).AddMinutes(18), WindowDuration = TimeSpan.FromHours(5) };
        var data = new[]
        {
            new UsageSnapshot { ProviderId = "openai", ProviderName = "Codex", Status = UsageStatus.Available,
                Weekly = new UsageWindow { RemainingPercent = 41, ResetsAt = now.AddDays(3) }, Session = session, RetrievedAt = now },
            new UsageSnapshot { ProviderId = "claude", ProviderName = "Claude", Status = UsageStatus.Available,
                Weekly = new UsageWindow { RemainingPercent = 67, ResetsAt = now.AddDays(5) }, Session = session with { RemainingPercent = 88 }, RetrievedAt = now }
        };
        using var popup = new MeterForm();
        popup.UpdateUsage(data, new Dictionary<string, UsageSnapshot>(), false, now);
        popup.Show();
        popup.UpdateLayout();
        Save((W.FrameworkElement)popup.Content, "hover-card.png");
        popup.UpdateUsage(data.Select(s => s with { Session = null }).ToArray(), new Dictionary<string, UsageSnapshot>(), false, now);
        popup.UpdateLayout();
        Save((W.FrameworkElement)popup.Content, "weekly-only.png");
        popup.UpdateUsage(data, new Dictionary<string, UsageSnapshot>(), false, now);
        popup.UpdateLayout();

        var scene = new C.Canvas { Width = 1440, Height = 960, Background = new M.LinearGradientBrush(
            M.Color.FromRgb(15, 23, 38), M.Color.FromRgb(38, 67, 83), 30) };
        Add(Text("LIMITBAR / WINDOWS", 19, "#84EFCF"), 80, 82);
        Add(Text("Your limits.\nOne glance away.", 66, "#F0F4FC", true), 80, 145);
        Add(Text("Codex + Claude usage\non your Windows taskbar.", 27, "#BBC8DA"), 84, 340);
        Add(Text("Weekly remaining · Session remaining · Reset time", 19, "#BBC8DA"), 84, 458);
        Add(Text("Hover for details. Get back to work.", 24, "#F0F4FC"), 84, 560);
        Add(Text("Actual app UI · Sample readings\nIllustrative desktop background", 16, "#BBC8DA"), 84, 795);

        // VisualBrush uses the actual popup tree, including its per-pixel opacity.
        var view = new System.Windows.Shapes.Rectangle
        {
            Width = 380, Height = popup.ActualHeight,
            Fill = new M.VisualBrush((M.Visual)popup.Content) { Stretch = M.Stretch.Fill }
        };
        Add(view, 918, 888 - popup.ActualHeight);
        Add(new C.Border { Width = 1440, Height = 56, Background = new M.SolidColorBrush(M.Color.FromArgb(235, 17, 25, 38)) }, 0, 904);
        var readings = new C.StackPanel();
        readings.Children.Add(Text(TaskbarOverlay.Caption("Codex", data[0], now), 15, "#56D3B7", true));
        readings.Children.Add(Text(TaskbarOverlay.Caption("Claude", data[1], now), 15, "#E7AE7E", true));
        Add(readings, 980, 910);
        Save(scene, "overview.png");
        popup.Hide();

        var icon = new C.Canvas { Width = 256, Height = 256 };
        icon.Children.Add(new C.Border { Width = 256, Height = 256, CornerRadius = new W.CornerRadius(54), Background = Color("#152236") });
        var top = new C.Border { Width = 156, Height = 32, CornerRadius = new W.CornerRadius(16), Background = Color("#84EFCF") };
        var bottom = new C.Border { Width = 106, Height = 32, CornerRadius = new W.CornerRadius(16), Background = Color("#FFC296") };
        C.Canvas.SetLeft(top, 50); C.Canvas.SetTop(top, 80);
        C.Canvas.SetLeft(bottom, 50); C.Canvas.SetTop(bottom, 144);
        icon.Children.Add(top); icon.Children.Add(bottom);
        var png = Render(icon);
        var iconDir = Path.Combine(Environment.CurrentDirectory, "LimitBar.Desktop", "Assets");
        Directory.CreateDirectory(iconDir);
        using var bytes = new MemoryStream();
        png.Save(bytes);
        using var writer = new BinaryWriter(File.Create(Path.Combine(iconDir, "LimitBar.ico")));
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
        writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0);
        writer.Write((ushort)1); writer.Write((ushort)32); writer.Write((uint)bytes.Length); writer.Write((uint)22);
        writer.Write(bytes.ToArray());

        void Add(W.UIElement element, double x, double y)
        { C.Canvas.SetLeft(element, x); C.Canvas.SetTop(element, y); scene.Children.Add(element); }
        void Save(W.FrameworkElement element, string name)
        { using var stream = File.Create(Path.Combine(output, name)); Render(element).Save(stream); }
    }

    private static M.Brush Color(string value) => (M.Brush)new M.BrushConverter().ConvertFromString(value)!;
    private static C.TextBlock Text(string text, double size, string color, bool bold = false) => new()
    {
        Text = text, FontSize = size, FontFamily = new M.FontFamily("Segoe UI"), Foreground = Color(color),
        FontWeight = bold ? W.FontWeights.SemiBold : W.FontWeights.Normal
    };

    private static I.PngBitmapEncoder Render(W.FrameworkElement element)
    {
        if (!double.IsNaN(element.Width) && !double.IsNaN(element.Height))
        {
            element.Measure(new W.Size(element.Width, element.Height));
            element.Arrange(new W.Rect(0, 0, element.Width, element.Height));
        }
        element.UpdateLayout();
        var bitmap = new I.RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, M.PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new I.PngBitmapEncoder();
        encoder.Frames.Add(I.BitmapFrame.Create(bitmap));
        return encoder;
    }
}
