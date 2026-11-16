// DetailPanelBuilder.Overrides.Dxvk.cs — Management section.
// DXVK controls have moved to DetailPanelBuilder.Extras.cs under the API Upgrades sub-header.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    internal static readonly string[] DcDllOverrideNames =
    [
        "dxgi.dll", "d3d9.dll", "d3d11.dll", "d3d12.dll", "ddraw.dll",
        "hid.dll", "version.dll", "opengl32.dll", "dbghelp.dll",
        "vulkan-1.dll", "winmm.dll",
    ];

    private void BuildManagementSection(GameCardViewModel card, string capturedName, OverridesPanelCtx ctx)
    {
        _window.ViewModel.SetLastUiAction($"BuildManagementSection({card.GameName})");
        // ── Management section (single row: 5 buttons side by side with separators) ──
        _window.ManagementPanel.Children.Clear();

        // Collapsible header
        const string mgmtSectionKey = "Management";
        bool mgmtCollapsed = Layout.IsCollapsed(mgmtSectionKey);

        var mgmtArrow = new TextBlock
        {
            Text      = mgmtCollapsed ? "▶" : "▼",
            FontSize  = 10,
            Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(0, 0, 6, 0),
        };
        var mgmtTitle = new TextBlock
        {
            Text       = "관리",
            FontSize   = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var mgmtHeaderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        ToolTipService.SetToolTip(mgmtArrow, SectionHeaderTooltip);
        ToolTipService.SetToolTip(mgmtTitle, SectionHeaderTooltip);
        mgmtHeaderRow.Children.Add(MakeDragHandle(_window.ManagementContainer));
        mgmtHeaderRow.Children.Add(mgmtArrow);
        mgmtHeaderRow.Children.Add(mgmtTitle);
        _window.ManagementPanel.Children.Add(mgmtHeaderRow);
        Layout.RegisterHeader(mgmtSectionKey, mgmtHeaderRow);

        // Body wrapper
        var mgmtBody = new StackPanel { Spacing = 6, Visibility = mgmtCollapsed ? Visibility.Collapsed : Visibility.Visible };
        _window.ManagementPanel.Children.Add(mgmtBody);

        mgmtHeaderRow.PointerEntered += (s, e) => mgmtTitle.Foreground = UIFactory.Brush(ResourceKeys.AccentTealBrush);
        mgmtHeaderRow.PointerExited  += (s, e) => mgmtTitle.Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush);
        var mgmtHandCursor  = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
        var mgmtArrowCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
        var mgmtCursorProp  = DetailPanelBuilder.CursorProp;
        mgmtHeaderRow.PointerEntered += (s, e) => mgmtCursorProp?.SetValue(mgmtHeaderRow, mgmtHandCursor);
        mgmtHeaderRow.PointerExited  += (s, e) => mgmtCursorProp?.SetValue(mgmtHeaderRow, mgmtArrowCursor);
        mgmtHeaderRow.PointerPressed += (s, e) =>
        {
            bool nowCollapsed = mgmtBody.Visibility == Visibility.Visible;
            mgmtBody.Visibility = nowCollapsed ? Visibility.Collapsed : Visibility.Visible;
            mgmtArrow.Text = nowCollapsed ? "▶" : "▼";
            Layout.SetCollapsed(mgmtSectionKey, nowCollapsed);
        };

        var mgmtRow = new Grid { ColumnSpacing = 0 };
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mgmtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var changeFolderBtn = new Button
        {
            Content = "설치 폴더 변경",
            FontSize = 11,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush),
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Tag = card,
        };
        changeFolderBtn.Click += (s, ev) => _window.BrowseFolder_Click(s, ev);
        ToolTipService.SetToolTip(changeFolderBtn, "이 게임의 설치 폴더를 변경합니다. 자동 감지가 잘못된 폴더를 선택했을 때 사용하세요.");
        Grid.SetColumn(changeFolderBtn, 0);
        mgmtRow.Children.Add(changeFolderBtn);

        var sep1 = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush), Margin = new Thickness(8, 4, 8, 4) };
        Grid.SetColumn(sep1, 1);
        mgmtRow.Children.Add(sep1);

        var resetFolderBtn = new Button
        {
            Content = "설치 폴더 초기화",
            FontSize = 11,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush),
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Tag = card,
            IsEnabled = !card.IsManuallyAdded,
        };
        resetFolderBtn.Click += (s, ev) => _window.ResetInstallFolder_Click(s, ev);
        ToolTipService.SetToolTip(resetFolderBtn, card.IsManuallyAdded
            ? "직접 추가한 게임이므로 되돌아갈 감지된 폴더가 없습니다. 설치 폴더 변경을 사용하세요."
            : "변경한 경우, RHI가 감지한 설치 폴더로 되돌립니다.");
        Grid.SetColumn(resetFolderBtn, 2);
        mgmtRow.Children.Add(resetFolderBtn);

        var sepReset = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush), Margin = new Thickness(8, 4, 8, 4) };
        Grid.SetColumn(sepReset, 3);
        mgmtRow.Children.Add(sepReset);

        var removeGameBtn = new Button
        {
            Content = "RHI에서 제거",
            FontSize = 11,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Tag = card,
        };
        removeGameBtn.Click += (s, ev) => _window.RemoveManualGame_Click(s, ev);
        ToolTipService.SetToolTip(removeGameBtn, card.IsManuallyAdded
            ? "RHI에서 이 게임을 제거합니다. 직접 추가한 게임이므로 다시 추가하면 복구됩니다."
            : "RHI에서 이 게임을 영구적으로 제거합니다. 숨기기와 달리 RHI가 게임을 검색해도 다시 나타나지 않습니다. 설정 › 도구 및 유지 관리 › 제거된 게임에서 되돌릴 수 있습니다.");
        Grid.SetColumn(removeGameBtn, 4);
        mgmtRow.Children.Add(removeGameBtn);

        var sep2 = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush), Margin = new Thickness(8, 4, 8, 4) };
        Grid.SetColumn(sep2, 5);
        mgmtRow.Children.Add(sep2);

        var mgmtResetOverridesBtn = new Button
        {
            Content = "재정의 초기화",
            FontSize = 11,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
        mgmtResetOverridesBtn.Click += (s, ev) =>
        {
            // Call reset action directly — automation peer invoke fails on Visibility.Collapsed buttons
            ctx.ResetAction?.Invoke();
        };
        Grid.SetColumn(mgmtResetOverridesBtn, 6);
        ToolTipService.SetToolTip(mgmtResetOverridesBtn, "게임별 재정의(DLL 이름, 채널, 셰이더, 애드온, 실행 설정, 업데이트 포함 여부)를 모두 기본값으로 초기화합니다.");
        mgmtRow.Children.Add(mgmtResetOverridesBtn);

        var sep3 = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush), Margin = new Thickness(8, 4, 8, 4) };
        Grid.SetColumn(sep3, 7);
        mgmtRow.Children.Add(sep3);

        var reportBtn = new Button
        {
            Content = "보고서 복사",
            FontSize = 11,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush),
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
        async void CopyReport()
        {
            try
            {
                var targetCard = _window.ViewModel.AllCards.FirstOrDefault(c =>
                    c.GameName.Equals(capturedName, StringComparison.OrdinalIgnoreCase));
                if (targetCard != null)
                    await GameReportEncoder.ShowAndCopyAsync(_window.Content.XamlRoot, targetCard, _window.ViewModel);
            }
            catch (Exception ex)
            {
                Services.CrashReporter.Log($"[BuildManagementSection] Copy Report failed — {ex.Message}");
            }
        }
        reportBtn.Click += (s, ev) => CopyReport();
        Grid.SetColumn(reportBtn, 8);
        ToolTipService.SetToolTip(reportBtn, "이 게임의 진단 보고서를 클립보드에 복사합니다. Discord 또는 GitHub 지원 요청 시 유용합니다.");
        mgmtRow.Children.Add(reportBtn);

        if (Layout.IsTabs)
        {
            // Tabs (mockup A): the same four actions as tiles
            var tiles = new Controls.AutoFitGrid { MinColumnWidth = 220 };
            tiles.Children.Add(TabsTheme.MakeTile("설치 폴더 변경", "이 게임에 다른 폴더를 지정합니다", false,
                (s, ev) => _window.BrowseFolder_Click(changeFolderBtn, ev), TabsTooltip(changeFolderBtn)));
            tiles.Children.Add(TabsTheme.MakeTile("재정의 초기화", "모든 게임별 설정을 전역 설정으로 되돌립니다", false,
                (s, ev) => ctx.ResetAction?.Invoke(), TabsTooltip(mgmtResetOverridesBtn)));
            tiles.Children.Add(TabsTheme.MakeTile("보고서 복사", "지원 요청용으로 이 게임의 구성을 복사합니다", false,
                (s, ev) => CopyReport(), TabsTooltip(reportBtn)));
            if (!card.IsManuallyAdded)
                tiles.Children.Add(TabsTheme.MakeTile("설치 폴더 초기화", "RHI가 감지한 폴더로 되돌립니다", false,
                    (s, ev) => _window.ResetInstallFolder_Click(resetFolderBtn, ev), TabsTooltip(resetFolderBtn)));
            tiles.Children.Add(TabsTheme.MakeTile("RHI에서 제거", card.IsManuallyAdded
                    ? "직접 추가한 이 게임을 제거합니다"
                    : "RHI에서 이 게임을 영구적으로 제거합니다. 설정에서 되돌릴 수 있습니다", true,
                (s, ev) => _window.RemoveManualGame_Click(removeGameBtn, ev), TabsTooltip(removeGameBtn)));
            mgmtBody.Children.Add(tiles);
            return;
        }

        mgmtBody.Children.Add(mgmtRow);
    }

    private static string? TabsTooltip(DependencyObject d) => ToolTipService.GetToolTip(d) as string;
}
