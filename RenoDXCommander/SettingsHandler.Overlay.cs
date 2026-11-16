// SettingsHandler.Overlay.cs -- the "RHI overlay" section of Settings (before Tools & Maintenance, after
// Achievements): on / off and the hotkey. Built in C# (XAML constraint), inserted the first time Settings
// opens, before the section navigation is built so it gets a jump button. See docs/rhi-overlay-plan.md.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private ComboBox? _rhiOverlayCombo;
    private ComboBox? _rhiOverlayPadCombo;
    private ComboBox? _rhiOverlayPauseCombo;
    private TextBox? _rhiOverlayHotkeyBox;
    private TextBlock? _rhiOverlayStatus;
    private bool _rhiOverlaySectionFailed;

    private void EnsureRhiOverlaySection()
    {
        if (_rhiOverlayCombo != null)
        {
            _rhiOverlayCombo.SelectedIndex = ViewModel.Settings.RhiOverlayEnabled ? 1 : 0;
            if (_rhiOverlayPadCombo != null) _rhiOverlayPadCombo.SelectedIndex = ViewModel.Settings.RhiOverlayController ? 1 : 0;
            if (_rhiOverlayPauseCombo != null) _rhiOverlayPauseCombo.SelectedIndex = ViewModel.Settings.RhiOverlayPauseGame ? 1 : 0;
            if (_rhiOverlayHotkeyBox != null) _rhiOverlayHotkeyBox.Text = HotkeyManager.FormatHotkeyDisplay(ViewModel.Settings.RhiOverlayHotkey);
            UpdateRhiOverlayStatus();
            return;
        }
        if (_rhiOverlaySectionFailed) return;
        try
        {
            var panel = _window.SettingsCardsPanel;
            int toolsIndex = -1;
            for (int i = 0; i < panel.Children.Count; i++)
                if (panel.Children[i] is StackPanel sp && sp.Tag is string t && t == "Section:Tools") { toolsIndex = i; break; }
            if (toolsIndex < 0)
            {
                _rhiOverlaySectionFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureRhiOverlaySection] Tools section not found — section not added");
                return;
            }

            var section = new StackPanel { Tag = "Section:RhiOverlay", Spacing = 12 };
            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock
            {
                Text = "RHI 오버레이",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            });
            head.Children.Add(new TextBlock
            {
                Text = "키나 컨트롤러로 게임 위에 여는 전체 화면 RHI 페이지입니다: 도전 과제, 스크린샷, 메모, "
                     + "플레이 시간, 게임의 모드 구성. 트레이 상태를 포함해 RHI가 실행 중이면 작동합니다.",
                FontSize = 12,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap,
                Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            });
            section.Children.Add(head);

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var body = new StackPanel { Spacing = 12 };
            card.Child = body;
            body.Children.Add(new TextBlock { Text = "오버레이", Style = FindStyle("SettingsCardTitleStyle") });

            var columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var left = new StackPanel { Spacing = 9, Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Top };
            var divider = new Border { Width = 1, Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush) };
            var right = new StackPanel { Spacing = 9, Margin = new Thickness(20, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(divider, 1);
            Grid.SetColumn(right, 2);
            columns.Children.Add(left);
            columns.Children.Add(divider);
            columns.Children.Add(right);

            // Left: on / off
            var onRow = MakeSettingRow("RHI 오버레이", out var onCell);
            var combo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
            combo.Items.Add(new ComboBoxItem { Content = "꺼짐" });
            combo.Items.Add(new ComboBoxItem { Content = "켜짐" });
            combo.SelectedIndex = ViewModel.Settings.RhiOverlayEnabled ? 1 : 0;
            ToolTipService.SetToolTip(combo,
                "게임에서 단축키를 누르면 게임 위에 RHI가 열립니다. 다시 누르거나 Esc를 누르면 돌아갑니다.\n"
              + "게임에는 아무것도 설치되지 않습니다. 테두리 없는 창 모드, 창 모드 게임 및 "
              + "Windows 전체 화면 최적화를 사용하는 게임에서 작동하며, 진짜 전용 전체 화면에서는 표시되지 않습니다.");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(combo, "RHI 오버레이");
            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedIndex < 0) return;
                bool on = combo.SelectedIndex == 1;
                if (on == ViewModel.Settings.RhiOverlayEnabled) return;
                ViewModel.Settings.RhiOverlayEnabled = on;
                ViewModel.SaveSettingsPublic();
                CrashReporter.Log($"[SettingsHandler.RhiOverlay] {(on ? "On" : "Off")}");
                _window.ApplyRhiOverlaySetting();
                UpdateRhiOverlayStatus();
            };
            onCell.Child = combo;
            left.Children.Add(onRow);
            _rhiOverlayCombo = combo;

            var padRow = MakeSettingRow("컨트롤러 (View + Menu 길게 누르기)", out var padCell);
            var padCombo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
            padCombo.Items.Add(new ComboBoxItem { Content = "꺼짐" });
            padCombo.Items.Add(new ComboBoxItem { Content = "켜짐" });
            padCombo.SelectedIndex = ViewModel.Settings.RhiOverlayController ? 1 : 0;
            ToolTipService.SetToolTip(padCombo,
                "게임에서 View + Menu(Back + Start, PlayStation은 Share + Options)를 0.5초 동안 누르면 "
              + "오버레이가 열리거나 닫힙니다. 열려 있는 동안: D-패드로 이동, A로 선택, B로 뒤로, LB / RB로 열 간 이동, 오른쪽 스틱으로 스크롤.");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(padCombo, "컨트롤러로 RHI 오버레이 열기");
            padCombo.SelectionChanged += (s, e) =>
            {
                if (padCombo.SelectedIndex < 0) return;
                bool on = padCombo.SelectedIndex == 1;
                if (on == ViewModel.Settings.RhiOverlayController) return;
                ViewModel.Settings.RhiOverlayController = on;
                ViewModel.SaveSettingsPublic();
                CrashReporter.Log($"[SettingsHandler.RhiOverlayController] {(on ? "On" : "Off")}");
                _window.ApplyRhiOverlaySetting();
            };
            padCell.Child = padCombo;
            left.Children.Add(padRow);
            _rhiOverlayPadCombo = padCombo;

            // Right: hotkey
            var keyRow = MakeSettingRow("단축키", out var keyCell);
            var box = new TextBox
            {
                Style = FindStyle("SettingsTextBoxStyle"),
                IsReadOnly = true,
                Text = HotkeyManager.FormatHotkeyDisplay(ViewModel.Settings.RhiOverlayHotkey),
            };
            ToolTipService.SetToolTip(box,
                "여기를 클릭한 후 Ctrl, Alt 또는 Shift와 함께 원하는 키를 누르세요 (예: Alt + `). "
              + "Backspace를 누르면 기본값(Alt + `)으로 돌아갑니다.");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box, "RHI 오버레이 단축키");
            box.PreviewKeyDown += RhiOverlayHotkeyBox_PreviewKeyDown;
            keyCell.Child = box;
            right.Children.Add(keyRow);
            _rhiOverlayHotkeyBox = box;

            var pauseRow = MakeSettingRow("열려 있는 동안 게임 일시 정지", out var pauseCell);
            var pauseCombo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
            pauseCombo.Items.Add(new ComboBoxItem { Content = "꺼짐" });
            pauseCombo.Items.Add(new ComboBoxItem { Content = "켜짐" });
            pauseCombo.SelectedIndex = ViewModel.Settings.RhiOverlayPauseGame ? 1 : 0;
            ToolTipService.SetToolTip(pauseCombo,
                "오버레이가 열려 있는 동안 게임을 멈추고 돌아가면 다시 재개하여, 오버레이를 사용하는 중에 "
              + "게임이 컨트롤러 입력에 반응하지 않게 합니다 (Windows에서는 다른 방법으로 게임의 컨트롤러 입력을 막을 수 없습니다).\n"
              + "실험적: 온라인 게임은 연결이 끊길 수 있고, 안티치트가 문제를 삼을 수 있으며, 일부 게임은 재개할 때 "
              + "잠깐 끊길 수 있습니다. 싱글플레이 게임 전용.");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(pauseCombo, "RHI 오버레이가 열려 있는 동안 게임 일시 정지");
            pauseCombo.SelectionChanged += (s, e) =>
            {
                if (pauseCombo.SelectedIndex < 0) return;
                bool on = pauseCombo.SelectedIndex == 1;
                if (on == ViewModel.Settings.RhiOverlayPauseGame) return;
                ViewModel.Settings.RhiOverlayPauseGame = on;
                ViewModel.SaveSettingsPublic();
                CrashReporter.Log($"[SettingsHandler.RhiOverlayPauseGame] {(on ? "On" : "Off")}");
            };
            pauseCell.Child = pauseCombo;
            right.Children.Add(pauseRow);
            _rhiOverlayPauseCombo = pauseCombo;

            body.Children.Add(columns);
            _rhiOverlayStatus = new TextBlock { Style = FindStyle("SettingsHelpStyle"), TextWrapping = TextWrapping.Wrap };
            body.Children.Add(_rhiOverlayStatus);
            UpdateRhiOverlayStatus();
            section.Children.Add(card);

            panel.Children.Insert(toolsIndex, section);
        }
        catch (Exception ex)
        {
            _rhiOverlaySectionFailed = true;
            _rhiOverlayCombo = null;
            CrashReporter.Log($"[SettingsHandler.EnsureRhiOverlaySection] Failed — {ex.Message}");
        }
    }

    /// <summary>A Settings row: label left, the control (put in <paramref name="cell"/>) right.</summary>
    private Grid MakeSettingRow(string label, out Border cell)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
        row.Children.Add(new TextBlock { Text = label, Style = FindStyle("SettingsLabelStyle"), VerticalAlignment = VerticalAlignment.Center });
        cell = new Border();
        Grid.SetColumn(cell, 1);
        row.Children.Add(cell);
        return row;
    }

    private void UpdateRhiOverlayStatus()
    {
        if (_rhiOverlayStatus == null) return;
        var key = HotkeyManager.FormatHotkeyDisplay(ViewModel.Settings.RhiOverlayHotkey);
        if (!ViewModel.Settings.RhiOverlayEnabled)
        {
            _rhiOverlayStatus.Text = "오버레이가 꺼져 있습니다.";
            _rhiOverlayStatus.Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush);
        }
        else if (_window.RhiOverlayHotkeyActive)
        {
            _rhiOverlayStatus.Text = $"게임에서 {key}을(를) 누르면 오버레이가 열립니다.";
            _rhiOverlayStatus.Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush);
        }
        else
        {
            _rhiOverlayStatus.Text = $"다른 프로그램이 {key}을(를) 사용 중입니다. 다른 키를 선택하세요.";
            _rhiOverlayStatus.Foreground = UIFactory.Brush(ResourceKeys.AccentAmberBrush);
        }
    }

    private void RhiOverlayHotkeyBox_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var key = e.Key;
        if (IsModifierKey(key) || IsHotkeyBoxPassThroughKey(key)) return;
        e.Handled = true;

        string hotkey;
        if (key == Windows.System.VirtualKey.Back)
            hotkey = SettingsViewModel.DefaultRhiOverlayHotkey;
        else
        {
            bool shift = IsKeyDown(Windows.System.VirtualKey.Shift);
            bool ctrl = IsKeyDown(Windows.System.VirtualKey.Control);
            bool alt = IsKeyDown(Windows.System.VirtualKey.Menu);
            int vk = (int)key;
            // A key on its own (or Shift + a letter) would be taken from every program while RHI runs
            bool functionKey = vk >= 112 && vk <= 135;
            if (!ctrl && !alt && !(shift && functionKey))
            {
                if (_rhiOverlayStatus != null)
                {
                    _rhiOverlayStatus.Text = "게임과 다른 프로그램이 키 단독 입력을 계속 받을 수 있도록 Ctrl 또는 Alt(F 키는 Shift)와 함께 사용하세요.";
                    _rhiOverlayStatus.Foreground = UIFactory.Brush(ResourceKeys.AccentAmberBrush);
                }
                return;
            }
            hotkey = HotkeyManager.BuildHotkeyString(vk, shift, ctrl, alt);
        }

        if (sender is TextBox box) box.Text = HotkeyManager.FormatHotkeyDisplay(hotkey);
        if (hotkey != ViewModel.Settings.RhiOverlayHotkey)
        {
            ViewModel.Settings.RhiOverlayHotkey = hotkey;
            ViewModel.SaveSettingsPublic();
            CrashReporter.Log($"[SettingsHandler.RhiOverlayHotkey] Saved: {hotkey}");
        }
        _window.ApplyRhiOverlaySetting();
        UpdateRhiOverlayStatus();
    }

    private static bool IsKeyDown(Windows.System.VirtualKey key) =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private static bool IsModifierKey(Windows.System.VirtualKey key) =>
        key is Windows.System.VirtualKey.Control or Windows.System.VirtualKey.Shift or Windows.System.VirtualKey.Menu
            or Windows.System.VirtualKey.LeftControl or Windows.System.VirtualKey.RightControl
            or Windows.System.VirtualKey.LeftShift or Windows.System.VirtualKey.RightShift
            or Windows.System.VirtualKey.LeftMenu or Windows.System.VirtualKey.RightMenu
            or (Windows.System.VirtualKey)91 or (Windows.System.VirtualKey)92;
}
