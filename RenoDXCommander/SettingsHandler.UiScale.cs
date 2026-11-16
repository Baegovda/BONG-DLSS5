// SettingsHandler.UiScale.cs — Settings › General › Windows: Interface size.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private ComboBox? _uiScaleCombo;
    private ComboBox? _topBarScaleCombo;
    private ComboBox? _gameListScaleCombo;
    private bool _uiScaleSelecting;

    /// <summary>Adds the "Interface size" row under Start with Windows (once) and selects the saved value.</summary>
    private void EnsureUiScaleRow()
    {
        try
        {
            if (_uiScaleCombo == null)
            {
                if (FindRowOf(_window.SettingsCardsPanel, _window.StartWithWindowsCombo) is not { } found)
                {
                    CrashReporter.Log("[SettingsHandler.EnsureUiScaleRow] Start with Windows row not found");
                    return;
                }
                var (list, anchorRow) = found;

                var row = new Grid { ColumnSpacing = 10 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
                var label = new TextBlock { Text = "인터페이스 크기" };
                if (Application.Current.Resources.TryGetValue("SettingsLabelStyle", out var ls) && ls is Style labelStyle)
                    label.Style = labelStyle;
                row.Children.Add(label);

                var combo = new ComboBox();
                if (Application.Current.Resources.TryGetValue("SettingsComboStyle", out var cs) && cs is Style comboStyle)
                    combo.Style = comboStyle;
                foreach (var p in MainWindow.UiScaleOptions) combo.Items.Add(p == 100 ? "100% (보통)" : $"{p}%");
                ToolTipService.SetToolTip(combo,
                    "Windows 배율을 바꾸지 않고 RHI 창을 작게 또는 크게 만듭니다. " +
                    "TV나 200% 이상으로 설정된 고DPI 화면에서 유용합니다. " +
                    "RHI 어디서든 Ctrl을 누른 채 마우스 휠을 돌려도 됩니다. " +
                    "팝업, 드롭다운 목록, 툴팁은 Windows 크기를 유지합니다.");
                Grid.SetColumn(combo, 1);
                row.Children.Add(combo);
                list.Children.Insert(list.Children.IndexOf(anchorRow) + 1, row);
                _uiScaleCombo = combo;
                combo.SelectionChanged += UiScaleCombo_SelectionChanged;

                // Top bar and game list sizes, on top of the interface size
                _topBarScaleCombo = AddSizeRow(list, list.Children.IndexOf(row) + 1, "상단 바 크기",
                    "모든 레이아웃에서 인터페이스 크기에 더해 상단 바를 작게 또는 크게 만듭니다. " +
                    "상단 바의 아래쪽 가장자리를 끌어서 조절할 수도 있습니다. 가장자리를 두 번 클릭하면 초기화됩니다.",
                    pct => { ViewModel.Settings.TopBarScale = pct; _window.ApplyTopBarScale(); });
                _gameListScaleCombo = AddSizeRow(list, list.Children.IndexOf(row) + 2, "게임 목록 크기",
                    "인터페이스 크기에 더해 게임 목록을 작게 또는 크게 만듭니다 (Classic+ 및 Tabs, " +
                    "Spotlight 커버는 별도 크기 사용). " +
                    "목록의 오른쪽 가장자리를 끌어서 조절할 수도 있습니다. 가장자리를 두 번 클릭하면 초기화됩니다.",
                    pct => { ViewModel.Settings.GameListScale = pct; _window.ApplyGameListScale(); });
            }
            SelectUiScale();
            SelectChromeSizes();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.EnsureUiScaleRow] {ex.Message}");
        }
    }

    /// <summary>Selects the saved size in the combo (also called after Ctrl + mouse wheel changes it).</summary>
    internal void SelectUiScale()
    {
        if (_uiScaleCombo == null) return;
        _uiScaleSelecting = true;
        // A size saved by an older version that isn't in the list (67%) shows as the nearest one
        var opts = MainWindow.UiScaleOptions;
        int saved = ViewModel.Settings.UiScale;
        int idx = Array.IndexOf(opts, opts.OrderBy(p => Math.Abs(p - saved)).First());
        _uiScaleCombo.SelectedIndex = idx;
        _uiScaleSelecting = false;
    }

    private void UiScaleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_uiScaleSelecting || _uiScaleCombo == null) return;
        try
        {
            int idx = _uiScaleCombo.SelectedIndex;
            if (idx < 0 || idx >= MainWindow.UiScaleOptions.Length) return;
            int percent = MainWindow.UiScaleOptions[idx];
            if (percent == ViewModel.Settings.UiScale) return;
            ViewModel.Settings.UiScale = percent;
            ViewModel.SaveSettingsPublic();
            _window.ApplyUiScale(percent);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.UiScaleCombo_SelectionChanged] {ex.Message}");
        }
    }
    /// <summary>A "label | percent combo" row (50-200%) inserted at <paramref name="index"/>; <paramref name="apply"/> gets the new percent.</summary>
    private ComboBox AddSizeRow(Panel list, int index, string labelText, string tooltip, Action<int> apply)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
        var label = new TextBlock { Text = labelText };
        if (Application.Current.Resources.TryGetValue("SettingsLabelStyle", out var ls) && ls is Style labelStyle)
            label.Style = labelStyle;
        row.Children.Add(label);

        var combo = new ComboBox();
        if (Application.Current.Resources.TryGetValue("SettingsComboStyle", out var cs) && cs is Style comboStyle)
            combo.Style = comboStyle;
        foreach (var p in MainWindow.UiScaleOptions) combo.Items.Add(p == 100 ? "100% (보통)" : $"{p}%");
        ToolTipService.SetToolTip(combo, tooltip);
        Grid.SetColumn(combo, 1);
        row.Children.Add(combo);
        list.Children.Insert(Math.Min(index, list.Children.Count), row);

        combo.SelectionChanged += (s, e) =>
        {
            if (_uiScaleSelecting) return;
            try
            {
                int idx = combo.SelectedIndex;
                if (idx < 0 || idx >= MainWindow.UiScaleOptions.Length) return;
                apply(MainWindow.UiScaleOptions[idx]);
                ViewModel.SaveSettingsPublic();
                CrashReporter.Log($"[SettingsHandler.SizeRow] {labelText} {MainWindow.UiScaleOptions[idx]}%");
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[SettingsHandler.SizeRow] {labelText}: {ex.Message}");
            }
        };
        return combo;
    }

    /// <summary>Selects the saved top bar / game list sizes (also called after dragging the top bar's edge).</summary>
    internal void SelectChromeSizes()
    {
        _uiScaleSelecting = true;
        try
        {
            SelectNearest(_topBarScaleCombo, ViewModel.Settings.TopBarScale);
            SelectNearest(_gameListScaleCombo, ViewModel.Settings.GameListScale);
        }
        finally { _uiScaleSelecting = false; }
    }

    private static void SelectNearest(ComboBox? combo, int percent)
    {
        if (combo == null) return;
        var opts = MainWindow.UiScaleOptions;
        combo.SelectedIndex = Array.IndexOf(opts, opts.OrderBy(p => Math.Abs(p - percent)).First());
    }
}
