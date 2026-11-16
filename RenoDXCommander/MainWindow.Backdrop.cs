// MainWindow.Backdrop.cs -- BONG DLSS5's glass look (BongHub theme only): a Windows acrylic backdrop behind the
// window, the page / bar / sidebar surfaces made see-through so it shows, and two soft blue and purple glows
// behind the content. Other colour themes keep RHI's solid surfaces.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private Grid? _glowLayer;
    private Controls.GlassBackdrop? _glassBackdrop;
    private MicaBackdrop? _moveBackdrop;
    private NativeInterop.SubclassProc? _moveProc; // keep the native callback alive
    private const uint WM_ENTERSIZEMOVE = 0x0231, WM_EXITSIZEMOVE = 0x0232;

    /// <summary>
    /// Live acrylic re-blurs what's behind the window on every frame while it's dragged, which stutters on big 4K
    /// screens (user report). While the window is moved or resized it uses Mica (wallpaper-tinted, no live blur), and the
    /// acrylic comes back when the mouse is let go. Subclass id 3 (1 = ForegroundActivationTarget, 2 = overlay hotkey).
    /// </summary>
    private void HookMoveForBackdrop()
    {
        if (_moveProc != null) return;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _moveProc = (h, msg, wp, lp, id, data) =>
        {
            if (msg is WM_ENTERSIZEMOVE or WM_EXITSIZEMOVE)
            {
                try
                {
                    if (GlassWanted(this))
                        SystemBackdrop = msg == WM_ENTERSIZEMOVE
                            ? (_moveBackdrop ??= new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt })
                            : (_glassBackdrop ??= new Controls.GlassBackdrop());
                }
                catch (Exception ex)
                {
                    // Never let a managed exception unwind through the native window procedure
                    CrashReporter.Log($"[MainWindow.MoveBackdrop] {ex.Message}");
                }
            }
            return NativeInterop.DefSubclassProc(h, msg, wp, lp);
        };
        if (!NativeInterop.SetWindowSubclass(hwnd, _moveProc, new UIntPtr(3), UIntPtr.Zero))
            CrashReporter.Log("[MainWindow.MoveBackdrop] Could not watch window moves");
    }
    private bool _glassHooked;

    /// <summary>Surface brushes and how much of them stays (the rest lets the acrylic through). Stacked surfaces add up.</summary>
    private static readonly (string Key, double Opacity)[] GlassSurfaces =
    {
        ("SurfaceBaseBrush", 0.30),
        ("SurfaceHeaderBrush", 0.45),
        ("SurfaceSidebarBrush", 0.40),
        ("SurfaceStatusBarBrush", 0.50),
        ("SurfaceToolbarBrush", 0.45),
    };

    /// <summary>Hex surfaces the top bar, sidebar and Tabs pages paint with (shared UIFactory brushes).</summary>
    private static readonly (string Hex, double Opacity)[] GlassHexSurfaces =
    {
        (TabsTheme.Page, 0.35),
        (TabsTheme.PanelBg, 0.45),
    };

    private static bool GlassWanted(MainWindow w) =>
        string.Equals(w.ViewModel.Settings.ColorTheme, ThemePresets.BongHubId, StringComparison.OrdinalIgnoreCase);

    /// <summary>Turns the glass look on or off to match the colour theme. Safe to call again.</summary>
    private void ApplyGlassLook()
    {
        bool on = GlassWanted(this);
        try
        {
            if (on && SystemBackdrop is not Controls.GlassBackdrop) SystemBackdrop = _glassBackdrop ??= new Controls.GlassBackdrop(); // stays see-through when unfocused
            else if (!on && SystemBackdrop is Controls.GlassBackdrop or MicaBackdrop) SystemBackdrop = null;
            if (on) HookMoveForBackdrop();

            foreach (var (key, opacity) in GlassSurfaces)
                if (Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b)
                    b.Opacity = on ? opacity : 1.0;
            foreach (var (hex, opacity) in GlassHexSurfaces)
                UIFactory.GetBrush(hex).Opacity = on ? opacity : 1.0;

            EnsureGlowLayer();
            if (_glowLayer != null) _glowLayer.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            CrashReporter.Log($"[MainWindow.ApplyGlassLook] glass {(on ? "on" : "off")}");
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            CrashReporter.Log($"[MainWindow.ApplyGlassLook] {ex.Message}");
        }

        if (!_glassHooked)
        {
            _glassHooked = true;
            ThemeService.ThemeChanged += () => DispatcherQueue.TryEnqueue(ApplyGlassLook);
        }
    }

    /// <summary>Two soft glows (blue top-left, purple bottom-right) behind everything in the window's root grid.</summary>
    private void EnsureGlowLayer()
    {
        if (_glowLayer != null) return;
        if (GameViewPanel.Parent is not FrameworkElement content || content.Parent is not Grid root) return;

        Rectangle Glow(string colour, double cx, double cy) => new()
        {
            IsHitTestVisible = false,
            Fill = new RadialGradientBrush
            {
                Center = new Windows.Foundation.Point(cx, cy),
                GradientOrigin = new Windows.Foundation.Point(cx, cy),
                RadiusX = 0.75, RadiusY = 0.9,
                GradientStops =
                {
                    new GradientStop { Color = ThemePalette.Hex(colour) with { A = 0x40 }, Offset = 0 },
                    new GradientStop { Color = ThemePalette.Hex(colour) with { A = 0x00 }, Offset = 1 },
                },
            },
        };

        _glowLayer = new Grid { IsHitTestVisible = false };
        _glowLayer.Children.Add(Glow("#4C8AFE", 0.12, 0.05));
        _glowLayer.Children.Add(Glow("#A98BFF", 0.95, 0.95));
        Grid.SetRowSpan(_glowLayer, Math.Max(1, root.RowDefinitions.Count));
        Grid.SetColumnSpan(_glowLayer, Math.Max(1, root.ColumnDefinitions.Count));
        root.Children.Insert(0, _glowLayer);
    }
}
