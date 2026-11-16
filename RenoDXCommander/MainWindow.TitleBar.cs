// MainWindow.TitleBar.cs -- BONG DLSS5 (BongHub theme): no system title bar. The window's content runs to the top
// edge; Windows keeps drawing its minimise / maximise / close buttons there (transparent backgrounds). The app's top
// bar becomes the drag area (InputNonClientPointerSource Caption region) with its buttons, boxes and the size grip
// left clickable (Passthrough regions), and it keeps clear of the caption buttons with a right margin.

using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private bool _glassTitleBar;
    private bool _dragHooked;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _dragTimer;
    private string _lastDragKey = "";

    /// <summary>Extends the content into the title bar (glass on) or puts the system title bar back (glass off).</summary>
    private void ApplyGlassTitleBar()
    {
        if (AppWindow.TitleBar is not { } tb) return;
        bool on = GlassWanted(this);
        try
        {
            tb.ExtendsContentIntoTitleBar = on;
            if (on)
            {
                tb.PreferredHeightOption = TitleBarHeightOption.Tall;
                tb.ButtonBackgroundColor = Colors.Transparent;
                tb.ButtonInactiveBackgroundColor = Colors.Transparent;
                tb.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF);
                tb.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(0x48, 0xFF, 0xFF, 0xFF);
                tb.ButtonForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0xE6, 0xEA, 0xF2);
                tb.ButtonHoverForegroundColor = Colors.White;
                tb.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0x8D, 0x97, 0xAA);
            }
            _glassTitleBar = on;
            if (!on && _xamlTopBar != null) _xamlTopBar.Margin = new Thickness(0);
            if (!on) InputNonClientPointerSource.GetForWindowId(AppWindow.Id).ClearAllRegionRects();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException)
        {
            _crashReporter.Log($"[MainWindow.ApplyGlassTitleBar] {ex.Message}");
        }

        if (!_dragHooked && Content is FrameworkElement root)
        {
            _dragHooked = true;
            _dragTimer = DispatcherQueue.CreateTimer();
            _dragTimer.Interval = TimeSpan.FromMilliseconds(120);
            _dragTimer.IsRepeating = false;
            _dragTimer.Tick += (s, e) => UpdateDragRegions();
            root.LayoutUpdated += (s, e) => { if (_glassTitleBar) _dragTimer.Start(); };
            root.SizeChanged += (s, e) => { if (_glassTitleBar) _dragTimer.Start(); };
        }
        _dragTimer?.Start();
    }

    /// <summary>Caption (drag) = the top bar; Passthrough = everything clickable on it. Physical pixels, client coordinates.</summary>
    private void UpdateDragRegions()
    {
        if (!_glassTitleBar || _xamlTopBar == null || Content?.XamlRoot is not { } xr || AppWindow.TitleBar is not { } tb) return;
        try
        {
            double scale = xr.RasterizationScale;
            // Keep the bar's own controls out from under the caption buttons
            double inset = tb.RightInset / scale;
            if (Math.Abs(_xamlTopBar.Margin.Right - inset) > 0.5) _xamlTopBar.Margin = new Thickness(0, 0, inset, 0);

            var bar = Bounds(_xamlTopBar, scale);
            if (bar.Width <= 0 || bar.Height <= 0) return;
            // The drag strip covers the top bar and the space beside it up to the caption buttons
            var caption = new RectInt32(0, 0, bar.X + bar.Width, Math.Max(bar.Y + bar.Height, tb.Height));

            var pass = new List<RectInt32>();
            if (Content is DependencyObject rootObj) CollectClickable(rootObj, caption, scale, pass, 0);

            string key = $"{caption.Width}x{caption.Height}|" + string.Join(";", pass.Select(r => $"{r.X},{r.Y},{r.Width},{r.Height}"));
            if (key == _lastDragKey) return;
            _lastDragKey = key;
            var src = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
            src.SetRegionRects(NonClientRegionKind.Caption, new[] { caption });
            src.SetRegionRects(NonClientRegionKind.Passthrough, pass.ToArray());
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException)
        {
            _crashReporter.Log($"[MainWindow.UpdateDragRegions] {ex.Message}");
        }
    }

    private static RectInt32 Bounds(FrameworkElement el, double scale)
    {
        var p = el.TransformToVisual(null).TransformPoint(new Windows.Foundation.Point(0, 0));
        return new RectInt32((int)Math.Round(p.X * scale), (int)Math.Round(p.Y * scale),
            (int)Math.Round(el.ActualWidth * scale), (int)Math.Round(el.ActualHeight * scale));
    }

    /// <summary>Visible clickable controls (and the bar's size grip) that overlap the drag strip.</summary>
    private static void CollectClickable(DependencyObject node, RectInt32 caption, double scale, List<RectInt32> into, int depth)
    {
        if (depth > 40) return;
        if (node is UIElement { Visibility: Visibility.Collapsed }) return;
        if (node is FrameworkElement fe && fe.ActualWidth > 0 && fe.ActualHeight > 0
            && node is ButtonBase or TextBox or ComboBox or AutoSuggestBox or Controls.ResizeGrip or ToggleSwitch)
        {
            var r = Bounds(fe, scale);
            if (r.Y < caption.Y + caption.Height && r.Y + r.Height > caption.Y) into.Add(r);
            return; // its insides are covered by its own rect
        }
        int n = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < n; i++) CollectClickable(VisualTreeHelper.GetChild(node, i), caption, scale, into, depth + 1);
    }
}
