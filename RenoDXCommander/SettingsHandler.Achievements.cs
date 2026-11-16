// SettingsHandler.Achievements.cs -- the "Achievements" section of Settings (before Tools & Maintenance):
// opt-in toggles, one per store in a single card, for showing a game's achievements on the Spotlight page. Built in
// C# (XAML constraint), inserted the first time Settings opens, before the section navigation is built
// so it gets a jump button like the XAML sections.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private ComboBox? _steamAchCombo;
    private ComboBox? _xboxAchCombo;
    private bool _achievementsSectionFailed;

    private void EnsureAchievementsSection()
    {
        if (_steamAchCombo != null)
        {
            _steamAchCombo.SelectedIndex = ViewModel.Settings.SteamAchievements ? 1 : 0;
            if (_xboxAchCombo != null) _xboxAchCombo.SelectedIndex = ViewModel.Settings.XboxAchievements ? 1 : 0;
            return;
        }
        if (_achievementsSectionFailed) return;
        try
        {
            var panel = _window.SettingsCardsPanel;
            int toolsIndex = -1;
            for (int i = 0; i < panel.Children.Count; i++)
                if (panel.Children[i] is StackPanel sp && sp.Tag is string t && t == "Section:Tools") { toolsIndex = i; break; }
            if (toolsIndex < 0)
            {
                _achievementsSectionFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureAchievementsSection] Tools section not found — section not added");
                return;
            }

            var section = new StackPanel { Tag = "Section:Achievements", Spacing = 12 };
            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock
            {
                Text = "도전 과제",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            });
            head.Children.Add(new TextBlock
            {
                Text = "스포트라이트 페이지에 도전 과제를 표시합니다. 기본값은 꺼짐이며, 보고 싶은 스토어를 켜세요.",
                FontSize = 12,
                LineHeight = 17,
                TextWrapping = TextWrapping.Wrap,
                Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            });
            section.Children.Add(head);

            // One card: Steam left, Xbox right (the usual two-column Settings grid)
            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var body = new StackPanel { Spacing = 12 };
            card.Child = body;
            body.Children.Add(new TextBlock { Text = "스토어", Style = FindStyle("SettingsCardTitleStyle") });

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

            left.Children.Add(MakeAchievementRow(
                "Steam",
                "스포트라이트 레이아웃에서 실행 버튼 아래에 선택한 Steam 게임의 도전 과제를 표시합니다: "
              + "진행률, 최근 달성 및 최근 달성 목록. 클릭하면 달성 날짜와 희귀도가 포함된 전체 목록이 표시됩니다.\n"
              + "이 PC의 Steam 파일에서 읽으므로 로그인, API 키, 공개 프로필이 필요 없습니다. "
              + "이 PC에서 플레이하지 않은 게임은 한 번 실행하기 전까지 도전 과제가 표시되지 않습니다.",
                () => ViewModel.Settings.SteamAchievements,
                on => ViewModel.Settings.SteamAchievements = on,
                out _steamAchCombo));
            right.Children.Add(MakeAchievementRow(
                "Xbox",
                "스포트라이트 레이아웃에서 실행 버튼 아래에 선택한 Xbox / Game Pass 게임의 도전 과제를 "
              + "달성 날짜, 희귀도, 게이머 점수와 함께 표시합니다.\n"
              + "이 PC의 Xbox 앱 데이터에서 읽으므로 BONG DLSS5에서 로그인할 필요가 없습니다. Xbox 앱은 게임의 도전 과제를 "
              + "한 번 표시한 뒤에만 보관합니다. 게임에 아무것도 표시되지 않거나 새 달성이 누락되면 Xbox 앱에서 "
              + "해당 게임 페이지를 한 번 여세요.",
                () => ViewModel.Settings.XboxAchievements,
                on => ViewModel.Settings.XboxAchievements = on,
                out _xboxAchCombo));
            body.Children.Add(columns);
            body.Children.Add(new TextBlock
            {
                Text = "진행률, 달성 날짜, 아이콘, 희귀도를 이 PC의 각 스토어 파일에서 읽습니다. 스포트라이트 레이아웃 전용입니다.",
                Style = FindStyle("SettingsHelpStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            section.Children.Add(card);

            panel.Children.Insert(toolsIndex, section);
        }
        catch (Exception ex)
        {
            _achievementsSectionFailed = true;
            _steamAchCombo = null;
            CrashReporter.Log($"[SettingsHandler.EnsureAchievementsSection] Failed — {ex.Message}");
        }
    }

    /// <summary>One store's "Steam  [Off / On]" row; details in the combo's tooltip.</summary>
    private Grid MakeAchievementRow(string store, string tooltip, Func<bool> get, Action<bool> set, out ComboBox comboOut)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
        row.Children.Add(new TextBlock
        {
            Text = store,
            Style = FindStyle("SettingsLabelStyle"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var combo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
        combo.Items.Add(new ComboBoxItem { Content = "꺼짐" });
        combo.Items.Add(new ComboBoxItem { Content = "켜짐" });
        combo.SelectedIndex = get() ? 1 : 0;
        ToolTipService.SetToolTip(combo, tooltip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(combo, $"{store} 도전 과제 표시");
        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedIndex < 0) return;
            bool on = combo.SelectedIndex == 1;
            if (on == get()) return;
            set(on);
            ViewModel.SaveSettingsPublic();
            CrashReporter.Log($"[SettingsHandler.{store}Achievements] {(on ? "On" : "Off")}");
            _window.AchievementsSettingChanged();
        };
        Grid.SetColumn(combo, 1);
        row.Children.Add(combo);
        comboOut = combo;
        return row;
    }
}
