// SettingsHandler.Controller.cs -- the "Controller support" row in Settings › General › Windows and
// Admin Mode, under Drop Helper. Built in C# (XAML constraint), added the first time Settings opens.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private ComboBox? _controllerCombo;
    private bool _controllerRowFailed;

    private void EnsureControllerSetting()
    {
        if (_controllerCombo != null)
        {
            _controllerCombo.SelectedIndex = ViewModel.Settings.ControllerSupport ? 1 : 0;
            return;
        }
        if (_controllerRowFailed) return;
        try
        {
            // The Drop Helper row's Grid sits in the card column's StackPanel: add ours right after it
            // Found by walking the cards (the combo's Parent was null when Settings first opened)
            if (FindRowWith(_window.SettingsCardsPanel, _window.DropHelperCombo, null) is not (Grid dropRow, StackPanel column))
            {
                _controllerRowFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureControllerSetting] Drop Helper row not found — setting not added");
                return;
            }

            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
            row.Children.Add(new TextBlock
            {
                Text = "컨트롤러 지원",
                Style = FindStyle("SettingsLabelStyle"),
                VerticalAlignment = VerticalAlignment.Center,
            });

            var combo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
            combo.Items.Add(new ComboBoxItem { Content = "꺼짐" });
            combo.Items.Add(new ComboBoxItem { Content = "켜짐" });
            combo.SelectedIndex = ViewModel.Settings.ControllerSupport ? 1 : 0;
            ToolTipService.SetToolTip(combo,
                "Spotlight 레이아웃에서 Xbox 또는 PlayStation 컨트롤러를 사용합니다 (RHI가 활성 창일 때만).\n"
              + "방향 패드 또는 왼쪽 스틱: 이동   A: 선택   B: 뒤로   Y: 실행   X: 게임 메뉴\n"
              + "LB / RB: 이전 / 다음 게임 (사이드 패널 및 설정에서는 섹션)\n"
              + "LT / RT: 10개 게임 뒤로 / 앞으로 (길게 누르면 계속)   보기: 필터   메뉴: 설정\n"
              + "오른쪽 스틱: 스크롤   왼쪽 스틱 클릭: 전체 / 설치됨 / 즐겨찾기   오른쪽 스틱 클릭: 게임 커버로 돌아가기\n"
              + "PlayStation 컨트롤러(DualShock 4, DualSense, DualSense Edge)는 USB 또는 Bluetooth로 바로 작동합니다.");
            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedIndex < 0) return;
                bool on = combo.SelectedIndex == 1;
                if (on == ViewModel.Settings.ControllerSupport) return;
                ViewModel.Settings.ControllerSupport = on;
                ViewModel.SaveSettingsPublic();
                CrashReporter.Log($"[SettingsHandler.ControllerSupport] {(on ? "On" : "Off")}");
                _window.UpdateControllerTimer();
            };
            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);

            column.Children.Insert(column.Children.IndexOf(dropRow) + 1, row);
            _controllerCombo = combo;
        }
        catch (Exception ex)
        {
            _controllerRowFailed = true;
            CrashReporter.Log($"[SettingsHandler.EnsureControllerSetting] Failed — {ex.Message}");
        }
    }

    /// <summary>The row Grid holding <paramref name="target"/> and the StackPanel that holds the row.</summary>
    private static (Grid Row, StackPanel Column)? FindRowWith(UIElement node, UIElement target, StackPanel? column)
    {
        switch (node)
        {
            case Grid g when g.Children.Contains(target) && column != null:
                return (g, column);
            case Panel p:
                var col = p as StackPanel ?? column;
                foreach (var child in p.Children)
                    if (FindRowWith(child, target, col) is { } found) return found;
                break;
            case Border { Child: { } child }:
                return FindRowWith(child, target, column);
            case ScrollViewer { Content: UIElement content }:
                return FindRowWith(content, target, column);
        }
        return null;
    }
}
