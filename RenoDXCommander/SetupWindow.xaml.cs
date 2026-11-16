// SetupWindow.xaml.cs — First-launch setup window.
// Shown before MainWindow on first launch to ask the user how they want ReShade managed.
// UI is built imperatively (same pattern as DetailPanelBuilder) — no separate XAML template.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Services;
using WinRT.Interop;

namespace RenoDXCommander;

/// <summary>
/// First-launch setup window. Asks the user whether RHI should manage ReShade
/// automatically or leave it alone. Invokes <see cref="OnComplete"/> with the choice
/// and then closes itself.
/// </summary>
public sealed partial class SetupWindow : Window
{
    private ForegroundActivationTarget? _foregroundTarget;
    /// <summary>
    /// Called after the user clicks either button.
    /// <c>true</c> = "Manage ReShade for me", <c>false</c> = "I'll manage it myself".
    /// </summary>
    public Action<bool>? OnComplete { get; set; }

    public SetupWindow()
    {
        InitializeComponent();

        Title = "BONG DLSS5 설정";

        // Size and position — scale by display DPI so window is correct at any Windows scaling setting
        var hwndForDpi = WindowNative.GetWindowHandle(this);
        uint dpi = NativeInterop.GetDpiForWindow(hwndForDpi);
        double dpiScale = dpi / 96.0;
        int logicalW = 600, logicalH = 620;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            (int)(logicalW * dpiScale),
            (int)(logicalH * dpiScale)));
        CenterOnPrimaryDisplay();

        // Dark title bar — same as MainWindow
        var hwnd = WindowNative.GetWindowHandle(this);
        NativeInterop.EnableDarkTitleBar(hwnd);

        if (AppWindow.TitleBar is { } titleBar)
        {
            var res = Application.Current.Resources;
            titleBar.BackgroundColor               = (Windows.UI.Color)res["TitleBarBackground"];
            titleBar.ForegroundColor               = (Windows.UI.Color)res["TitleBarForeground"];
            titleBar.InactiveBackgroundColor       = (Windows.UI.Color)res["TitleBarInactiveBackground"];
            titleBar.InactiveForegroundColor       = (Windows.UI.Color)res["TitleBarInactiveForeground"];
            titleBar.ButtonBackgroundColor         = (Windows.UI.Color)res["TitleBarButtonBackground"];
            titleBar.ButtonForegroundColor         = (Windows.UI.Color)res["TitleBarButtonForeground"];
            titleBar.ButtonHoverBackgroundColor    = (Windows.UI.Color)res["TitleBarButtonHoverBackground"];
            titleBar.ButtonHoverForegroundColor    = (Windows.UI.Color)res["TitleBarButtonHoverForeground"];
            titleBar.ButtonPressedBackgroundColor  = (Windows.UI.Color)res["TitleBarButtonPressedBackground"];
            titleBar.ButtonPressedForegroundColor  = (Windows.UI.Color)res["TitleBarButtonPressedForeground"];
            titleBar.ButtonInactiveBackgroundColor = (Windows.UI.Color)res["TitleBarButtonInactiveBackground"];
            titleBar.ButtonInactiveForegroundColor = (Windows.UI.Color)res["TitleBarButtonInactiveForeground"];
        }

        // Prevent resizing
        AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped);
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter overlapped)
        {
            overlapped.IsResizable  = false;
            overlapped.IsMaximizable = false;
        }

        BuildContent();

        Activated += (_, _) =>
        {
            _foregroundTarget ??= new ForegroundActivationTarget(hwnd,
                action => DispatcherQueue.TryEnqueue(() => action()), () =>
                {
                    AppWindow.Show();
                    Activate();
                    ForegroundActivation.Request(hwnd);
                });
        };
        Closed += (_, _) => _foregroundTarget?.Dispose();
    }

    private void CenterOnPrimaryDisplay()
    {
        try
        {
            var displayArea = Microsoft.UI.Windowing.DisplayArea.Primary;
            var workArea = displayArea.WorkArea;
            var winSize = AppWindow.Size;
            int x = workArea.X + (workArea.Width  - winSize.Width)  / 2;
            int y = workArea.Y + (workArea.Height - winSize.Height) / 2;
            AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }
        catch { /* non-critical */ }
    }

    private void BuildContent()
    {
        var root = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 16,
            Padding = new Thickness(36, 32, 36, 32),
        };

        // ── Title ──
        root.Children.Add(new TextBlock
        {
            Text = "BONG DLSS5에 오신 것을 환영합니다",
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
        });

        // ── Description ──
        const string description =
            "BONG DLSS5가 모든 게임의 ReShade를 자동으로 관리할 수 있습니다. 포함되는 내용은 다음과 같습니다:\n\n" +
            "  - 클릭 한 번으로 각 게임에 ReShade를 설치하고 업데이트합니다\n" +
            "  - 게임에 필요한 대로 게임별로 올바른 DLL 이름을 유지합니다\n" +
            "  - 셰이더 팩을 전역으로 관리합니다 - 한 번 설치하면 모든 게임에 자동으로 배포됩니다\n" +
            "  - 관리를 시작하기 전에 게임 폴더에 이미 있는 셰이더를 백업합니다\n" +
            "  - RenoDX, OptiScaler 또는 다른 구성 요소를 설치할 때 ReShade를 동기화합니다\n\n" +
            "특정 셰이더 모음, 직접 조정한 설정, 선호하는 버전 등 이미 사용자 지정 ReShade 구성이 있다면 " +
            "\"직접 관리하겠습니다\"를 선택하세요. BONG DLSS5가 ReShade를 전혀 건드리지 않습니다.";

        root.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 13,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
        });

        // ── Spacer ──
        root.Children.Add(new Border { Height = 4 });

        // ── Separator ──
        root.Children.Add(UIFactory.MakeSeparator());

        // ── Buttons ──
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(0, 4, 0, 0),
        };

        // "Manage ReShade for me" — accent blue style
        var manageBtn = new Button
        {
            Content = "ReShade 관리 맡기기",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 14,
            Padding = new Thickness(12, 10, 12, 10),
            Background = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            CornerRadius = new CornerRadius(6),
        };
        VisualStateManager.GoToState(manageBtn, "Normal", false);

        var selfBtn = new Button
        {
            Content = "직접 관리하겠습니다",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 14,
            Padding = new Thickness(12, 10, 12, 10),
            Background = UIFactory.Brush(ResourceKeys.SurfaceRaisedBrush),
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            CornerRadius = new CornerRadius(6),
        };
        VisualStateManager.GoToState(selfBtn, "Normal", false);

        manageBtn.Click += (_, _) => Complete(true);
        selfBtn.Click   += (_, _) => Complete(false);

        buttonPanel.Children.Add(manageBtn);
        buttonPanel.Children.Add(selfBtn);
        root.Children.Add(buttonPanel);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = root,
        };

        RootContent.Content = scroll;
    }

    private void Complete(bool manageReShade)
    {
        // Defer off the button's Click handler stack to avoid an access violation
        // when the callback closes this window while still inside event processing.
        DispatcherQueue.TryEnqueue(() =>
        {
            try { OnComplete?.Invoke(manageReShade); }
            catch (Exception ex) { CrashReporter.Log($"[SetupWindow.Complete] OnComplete threw — {ex.Message}"); }
        });
    }
}
