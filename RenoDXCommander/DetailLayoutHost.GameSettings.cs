// DetailLayoutHost.GameSettings.cs -- Spotlight's Game settings panel (the cog beside the game's title): per-game
// versions of Settings-page options that the Tune this game sections don't cover. A built panel, like Screenshots.
// Rows are label + one-line description + control (AddGameSettingRow); add new settings there.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

internal sealed partial class DetailLayoutHost
{
    internal const string GameSettingsKey = "GameSettings";

    /// <summary>The panel's content (OpenPanel).</summary>
    private FrameworkElement? BuildGameSettingsPanel()
    {
        if (_window.ViewModel.SelectedGame is not { } card) return null;
        var root = new StackPanel { Spacing = 14 };
        root.Children.Add(MutedText("설정 페이지의 설정 중 이 게임에만 적용되는 항목입니다."));

        var rows = new StackPanel { Spacing = 16 };
        AddOverlayRow(rows, card);
        AddDisplayModeRow(rows, card);
        root.Children.Add(TabsTheme.MakeCard(rows, 16));
        return root;
    }

    private void AddOverlayRow(StackPanel rows, GameCardViewModel card)
    {
        var vm = _window.ViewModel;
        string name = card.GameName, store = card.Source ?? "";
        var toggle = new ToggleSwitch
        {
            IsOn = !vm.IsOverlayDisabled(name, store),
            OnContent = "켜짐",
            OffContent = "꺼짐",
            MinWidth = 0,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, "BONG DLSS5 오버레이");
        toggle.Toggled += (s, e) =>
        {
            try
            {
                vm.SetOverlayDisabled(name, !toggle.IsOn, store);
                _window.RefreshOverlayPassthrough();
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[DetailLayoutHost.GameSettings] Overlay toggle for '{name}' failed — {ex.Message}");
            }
        };
        string desc = "단축키나 컨트롤러로 이 게임 위에 BONG DLSS5 오버레이를 엽니다. 꺼져 있으면 단축키가 게임으로 전달됩니다.";
        if (!vm.Settings.RhiOverlayEnabled) desc += " 현재 설정에서 모든 게임의 오버레이가 꺼져 있습니다.";
        AddGameSettingRow(rows, "BONG DLSS5 오버레이", desc, toggle);
    }

    private void AddDisplayModeRow(StackPanel rows, GameCardViewModel card)
    {
        var vm = _window.ViewModel;
        var btn = new Button { Content = "선택...", Padding = new Thickness(14, 0, 14, 0) };
        TabsTheme.Apply(btn, TabsTheme.BlueButton, height: 34, fontSize: 12);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(btn, "디스플레이 모드 설정");
        btn.Click += async (s, e) =>
        {
            try { await _window.ShowDisplayModeGameDialogAsync(card); }
            catch (Exception ex) when (ex is InvalidOperationException) { CrashReporter.Log($"[DetailLayoutHost.GameSettings] Display modes — {ex.Message}"); }
        };
        string desc = vm.DisplayModesOn
            ? $"이 게임이 디스플레이 모드 간에 전환하는 항목(현재 {vm.ActiveDisplayModeName(card)})과 모드별 값입니다."
            : "이 게임이 디스플레이 모드 간에 전환하는 항목과 모드별 값입니다. 설정 › 디스플레이에서 디스플레이 모드가 꺼져 있습니다.";
        AddGameSettingRow(rows, "디스플레이 모드", desc, btn);
    }

    /// <summary>One setting: label and a one-line description on the left, the control on the right.</summary>
    private static void AddGameSettingRow(StackPanel rows, string label, string description, FrameworkElement control)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = TabsTheme.B(TabsTheme.TextPrimary),
        });
        text.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = TabsTheme.B(TabsTheme.TextSecondary),
        });
        grid.Children.Add(text);

        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        rows.Children.Add(grid);
    }
}
