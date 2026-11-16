// DetailLayoutHost.Screenshots.cs -- Spotlight's Screenshots panel (the picture button beside the game's title):
// the game's ReShade screenshots as thumbnails, newest first, in the slide-in panel. Click = open in the default
// viewer; right-click = open, set as banner, copy, show in folder, delete (Recycle Bin). Same folder rules as the
// RHI overlay (ScreenshotLibraryService).
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

internal sealed partial class DetailLayoutHost
{
    internal const string ScreenshotsKey = "Screenshots";
    private const int ShotPage = 30;
    private const double ShotThumbWidth = 240;

    private StackPanel? _shotsRoot;
    private int _shotsSeq;

    /// <summary>The panel's content (OpenPanel). Fills in once the folder has been read (background).</summary>
    private FrameworkElement? BuildScreenshotsPanel()
    {
        if (_window.ViewModel.SelectedGame is not { } card) return null;
        _shotsRoot = new StackPanel { Spacing = 14 };
        _shotsRoot.Children.Add(MutedText("스크린샷 찾는 중…"));
        int seq = ++_shotsSeq;

        var rhiFolder = _window.ViewModel.BuildScreenshotSavePath(card.GameName);
        bool perGame = _window.ViewModel.Settings.PerGameScreenshotFolders;
        var installPath = card.InstallPath;
        _ = Task.Run(() =>
        {
            string? folder = null;
            List<FileInfo> shots = new();
            try
            {
                folder = ScreenshotLibraryService.FindFolder(installPath, rhiFolder);
                if (folder != null)
                {
                    bool own = (perGame && rhiFolder != null && SameFolder(folder, rhiFolder)) || SameFolder(folder, installPath);
                    string? exe = string.IsNullOrEmpty(installPath) ? null
                        : App.Services.GetRequiredService<IPeHeaderService>().FindGameExe(installPath);
                    shots = ScreenshotLibraryService.List(folder, exe, own);
                }
            }
            catch (Exception ex) { CrashReporter.Log($"[DetailLayoutHost.Screenshots] {card.GameName}: {ex.Message}"); }
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (seq == _shotsSeq && _panelKey == ScreenshotsKey) RenderShots(card, folder, shots);
            });
        });
        return _shotsRoot;
    }

    private void ReturnScreenshotsPanel()
    {
        _shotsSeq++;      // drop a load still running
        _shotsRoot = null;
    }

    private static bool SameFolder(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        try { return Path.GetFullPath(a).TrimEnd('\\').Equals(Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    private static TextBlock MutedText(string text) => new()
    {
        Text = text,
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
        Foreground = TabsTheme.B(TabsTheme.TextMuted),
    };

    private void RenderShots(GameCardViewModel card, string? folder, List<FileInfo> shots)
    {
        if (_shotsRoot == null) return;
        _shotsRoot.Children.Clear();

        // Summary card: how many, where, Open folder
        var head = new StackPanel { Spacing = 8 };
        head.Children.Add(new TextBlock
        {
            Text = folder == null ? "아직 스크린샷 없음"
                 : shots.Count == 0 ? "아직 스크린샷 없음"
                 : shots.Count == 1 ? "스크린샷 1개" : $"스크린샷 {shots.Count}개",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TabsTheme.B(TabsTheme.TextPrimary),
        });
        if (folder != null)
        {
            head.Children.Add(MutedText(folder));
            var open = new Button { Content = "폴더 열기", HorizontalAlignment = HorizontalAlignment.Left };
            TabsTheme.Apply(open, TabsTheme.IconButton, height: 34, fontSize: 12);
            open.Click += (_, _) => ShowInExplorer(folder, select: null);
            head.Children.Add(open);
        }
        if (shots.Count == 0)
            head.Children.Add(MutedText(folder == null
                ? "게임에서 ReShade 스크린샷 키(기본값 Print Screen)로 스크린샷을 찍거나, 설정 › ReShade › 스크린샷 폴더에서 폴더를 지정하세요."
                : "게임에서 ReShade 스크린샷 키(기본값 Print Screen)로 찍으세요."));
        else
            head.Children.Add(MutedText("스크린샷을 클릭하면 열립니다. 마우스 오른쪽 클릭으로 배너로 설정, 복사, 삭제를 할 수 있습니다."));
        _shotsRoot.Children.Add(TabsTheme.MakeCard(head, 16));
        if (shots.Count == 0) return;

        var grid = new Controls.AutoFitGrid { MinColumnWidth = ShotThumbWidth, ColumnSpacing = 10, RowSpacing = 10, StretchFewItems = false };
        _shotsRoot.Children.Add(grid);
        AddShotThumbs(card, grid, shots, 0);
    }

    /// <summary>Height / width of the game's monitor: screenshots have its shape.</summary>
    private double ScreenAspect()
    {
        try
        {
            var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(_window.AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            var b = area.OuterBounds;
            if (b.Width > 0 && b.Height > 0) return (double)b.Height / b.Width;
        }
        catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.COMException)
        {
            CrashReporter.Log($"[DetailLayoutHost.ScreenAspect] {ex.Message}");
        }
        return 9.0 / 16;
    }

    private void AddShotThumbs(GameCardViewModel card, Controls.AutoFitGrid grid, List<FileInfo> shots, int start)
    {
        double aspect = ScreenAspect();
        int decode = (int)Math.Ceiling(440 * Math.Max(1, grid.XamlRoot?.RasterizationScale ?? 1));
        int end = Math.Min(shots.Count, start + ShotPage);
        for (int i = start; i < end; i++)
            grid.Children.Add(MakeShotThumb(card, shots[i], aspect, decode));

        // "Show more" after the grid, replaced each time
        if (_shotsRoot!.Children.LastOrDefault() is Button { Tag: "ShotsMore" } old) _shotsRoot.Children.Remove(old);
        if (end < shots.Count)
        {
            var more = new Button { Content = $"더 보기 ({shots.Count - end}개 남음)", Tag = "ShotsMore", HorizontalAlignment = HorizontalAlignment.Left };
            TabsTheme.Apply(more, TabsTheme.IconButton, height: 36, fontSize: 13);
            more.Click += (_, _) => AddShotThumbs(card, grid, shots, end);
            _shotsRoot.Children.Add(more);
        }
    }

    private Button MakeShotThumb(GameCardViewModel card, FileInfo file, double aspect, int decode)
    {
        var image = new Image { Stretch = Stretch.UniformToFill };
        var fallback = new TextBlock
        {
            Text = Path.GetExtension(file.Name).ToUpperInvariant().TrimStart('.') + " 스크린샷",
            FontSize = 12,
            Foreground = TabsTheme.B(TabsTheme.TextMuted),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        var bmp = new BitmapImage { DecodePixelWidth = decode };
        bmp.ImageFailed += (_, _) => fallback.Visibility = Visibility.Visible; // e.g. some HDR .jxr files
        try { bmp.UriSource = new Uri(file.FullName); image.Source = bmp; }
        catch (UriFormatException) { fallback.Visibility = Visibility.Visible; }

        var cell = new Grid { Height = Math.Round(ShotThumbWidth * aspect), Background = TabsTheme.B("#1A2130") };
        cell.SizeChanged += (_, e) =>
        {
            double h = Math.Round(e.NewSize.Width * aspect);
            if (e.NewSize.Width > 0 && Math.Abs(cell.Height - h) > 0.5) cell.Height = h;
        };
        cell.Children.Add(image);
        cell.Children.Add(fallback);

        var btn = new Button
        {
            Content = cell,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(1),
            BorderBrush = TabsTheme.B(TabsTheme.CardBorder),
            CornerRadius = new CornerRadius(8),
            Background = TabsTheme.B("#1A2130"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(btn, $"{file.Name}\n{file.LastWriteTime:g}\n클릭하여 열기, 마우스 오른쪽 클릭으로 더 보기");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(btn, $"스크린샷 {file.LastWriteTime:g}");
        btn.Click += (_, _) => OpenShotFile(file.FullName);
        btn.ContextFlyout = MakeShotMenu(card, file, btn);
        return btn;
    }

    private MenuFlyout MakeShotMenu(GameCardViewModel card, FileInfo file, Button thumb)
    {
        var menu = new MenuFlyout();
        void Add(string text, Action action, bool enabled = true)
        {
            var item = new MenuFlyoutItem { Text = text, IsEnabled = enabled };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        Add("열기", () => OpenShotFile(file.FullName));
        // HDR .jxr screenshots can't be shown as the banner
        bool bannerOk = !file.Extension.Equals(".jxr", StringComparison.OrdinalIgnoreCase);
        Add(bannerOk ? "배너로 설정" : "배너로 설정 (.jxr 불가)", () => _ = _window.SetBannerFromFileAsync(card, file.FullName), bannerOk);
        Add("클립보드에 복사", () => _ = CopyShotAsync(file.FullName));
        Add("폴더에서 보기", () => ShowInExplorer(file.DirectoryName ?? "", select: file.FullName));
        menu.Items.Add(new MenuFlyoutSeparator());
        Add("삭제 (휴지통으로)", () =>
        {
            bool ok;
            try { ok = Overlay.OverlayNative.RecycleFile(file.FullName); }
            catch (Exception ex) { CrashReporter.Log($"[DetailLayoutHost.DeleteShot] {ex.Message}"); ok = false; }
            CrashReporter.Log($"[DetailLayoutHost.DeleteShot] {(ok ? "Moved to the Recycle Bin" : "Couldn't delete")}: {file.Name}");
            if (ok && thumb.Parent is Panel p) p.Children.Remove(thumb);
            else if (!ok) _window.ShowStatusBarMessage($"{file.Name}을(를) 삭제할 수 없습니다", TimeSpan.FromSeconds(6));
        });
        return menu;
    }

    private static void OpenShotFile(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            CrashReporter.Log($"[DetailLayoutHost.OpenShotFile] {ex.Message}");
        }
    }

    private static void ShowInExplorer(string folder, string? select)
    {
        if (string.IsNullOrEmpty(folder)) return;
        try
        {
            var args = select != null ? $"/select,\"{select}\"" : $"\"{folder}\"";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            CrashReporter.Log($"[DetailLayoutHost.ShowInExplorer] {ex.Message}");
        }
    }

    private async Task CopyShotAsync(string path)
    {
        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
            data.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(file));
            data.SetStorageItems(new[] { file });
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
            _window.ShowStatusBarMessage($"{Path.GetFileName(path)} 복사됨", TimeSpan.FromSeconds(4));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            CrashReporter.Log($"[DetailLayoutHost.CopyShot] {ex.Message}");
            _window.ShowStatusBarMessage("스크린샷을 복사할 수 없습니다", TimeSpan.FromSeconds(6));
        }
    }
}
