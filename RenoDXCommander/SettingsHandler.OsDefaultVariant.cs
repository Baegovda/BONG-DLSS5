// SettingsHandler.OsDefaultVariant.cs — Settings › Components › OptiScaler: default OptiScaler version.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private const string OsDefaultVariantRowTag = "OsDefaultVariantRow";

    // The dropdown's internal values, in order (built-ins, then custom builds: OsVariants.All)
    private List<string> _osDefaultVariantValues = new();

    private ComboBox? _osDefaultVariantCombo;

    /// <summary>
    /// Adds the "Default version" row above GPU type (once), built in C# because of the
    /// MainWindow.xaml constraint, and selects the saved value.
    /// </summary>
    private void EnsureOsDefaultVariantRow()
    {
        try
        {
            if (_osDefaultVariantCombo == null)
            {
                // Found by searching down from the Settings cards: FrameworkElement.Parent can be
                // null here before the page has been laid out
                if (FindRowOf(_window.SettingsCardsPanel, _window.OsGpuCombo) is not { } found)
                {
                    CrashReporter.Log("[SettingsHandler.EnsureOsDefaultVariantRow] GPU type row not found");
                    return;
                }
                var (list, gpuRow) = found;

                var row = new Grid { ColumnSpacing = 10, Tag = OsDefaultVariantRowTag };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
                var label = new TextBlock { Text = "기본 버전" };
                if (Application.Current.Resources.TryGetValue("SettingsLabelStyle", out var ls) && ls is Style labelStyle)
                    label.Style = labelStyle;
                row.Children.Add(label);

                var combo = new ComboBox();
                if (Application.Current.Resources.TryGetValue("SettingsComboStyle", out var cs) && cs is Style comboStyle)
                    combo.Style = comboStyle;
                _osDefaultVariantCombo = combo;
                FillOsDefaultVariants();
                ToolTipService.SetToolTip(combo,
                    "게임의 OptiScaler ⚙에서 버전을 선택하지 않은 게임에 설치되는 OptiScaler 버전입니다. " +
                    "이미 OptiScaler가 있는 게임은 기존 버전을 유지합니다. " +
                    "Stable은 공식 릴리스, Nightly는 일일 빌드, DLSS NR과 F5 DLSSNR은 Neural Rendering 포크입니다. " +
                    "아래에서 추가한 사용자 지정 빌드도 표시됩니다.");
                Grid.SetColumn(combo, 1);
                row.Children.Add(combo);
                list.Children.Insert(list.Children.IndexOf(gpuRow), row);
                _osDefaultVariantCombo = combo;
                SelectOsDefaultVariant();
                combo.SelectionChanged += OsDefaultVariantCombo_SelectionChanged;
                // Custom builds added / removed: refresh the list (the event can come from any thread)
                OsCustomBuildService.Changed += () => _window.DispatcherQueue.TryEnqueue(() =>
                {
                    FillOsDefaultVariants();
                    SelectOsDefaultVariant();
                });
                list.Children.Insert(list.Children.IndexOf(gpuRow), MakeOsNightlyCleanupRow());
                return;
            }
            FillOsDefaultVariants();
            SelectOsDefaultVariant();
            if (_osNightlyCleanupCombo != null) _osNightlyCleanupCombo.SelectedIndex = ViewModel.Settings.OsNightlyCleanup ? 1 : 0;
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.EnsureOsDefaultVariantRow] {ex.Message}");
        }
    }

    /// <summary>The panel holding the row that contains <paramref name="target"/>, and that row.</summary>
    private static (Panel List, FrameworkElement Row)? FindRowOf(DependencyObject root, UIElement target)
    {
        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Panel row && row.Children.Contains(target) && child is FrameworkElement fe)
                    return (panel, fe);
                if (FindRowOf(child, target) is { } hit) return hit;
            }
        }
        else if (root is Border { Child: { } bc }) return FindRowOf(bc, target);
        else if (root is ScrollViewer { Content: DependencyObject sc }) return FindRowOf(sc, target);
        else if (root is ContentControl { Content: DependencyObject cc }) return FindRowOf(cc, target);
        return null;
    }

    private ComboBox? _osNightlyCleanupCombo;

    /// <summary>"Remove old Nightly builds" Off / On, under Default version.</summary>
    private Grid MakeOsNightlyCleanupRow()
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 210 });
        row.Children.Add(new TextBlock { Text = "이전 Nightly 빌드 제거", Style = FindStyle("SettingsLabelStyle") });
        var combo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
        combo.Items.Add("꺼짐");
        combo.Items.Add("켜짐");
        combo.SelectedIndex = ViewModel.Settings.OsNightlyCleanup ? 1 : 0;
        ToolTipService.SetToolTip(combo,
            "RHI는 다운로드한 모든 OptiScaler Nightly 빌드의 사본을 보관하며, 각각 약 200-300 MB입니다. " +
            "켜짐: 새 Nightly 빌드를 다운로드하면 이전 빌드가 삭제됩니다. RHI는 최신 빌드와 " +
            "게임의 OptiScaler ⚙에서 고정한 빌드는 유지합니다. 삭제된 빌드는 나중에 선택하면 다시 다운로드됩니다. " +
            "이미 OptiScaler가 설치된 게임에는 영향이 없습니다.");
        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedIndex < 0) return;
            bool on = combo.SelectedIndex == 1;
            if (on == ViewModel.Settings.OsNightlyCleanup) return;
            ViewModel.Settings.OsNightlyCleanup = on;
            ViewModel.SaveSettingsPublic();
            CrashReporter.Log($"[SettingsHandler.OsNightlyCleanup] {(on ? "On" : "Off")}");
            if (!on) return;
            // Clear the builds that have piled up so far
            _ = Task.Run(() =>
            {
                var (removed, bytes) = ViewModel.CleanUpOldNightlyBuilds();
                if (removed == 0) return;
                var size = bytes >= 1024L * 1024 * 1024
                    ? $"{bytes / (1024.0 * 1024 * 1024):0.0} GB"
                    : $"{bytes / (1024 * 1024)} MB";
                _window.DispatcherQueue.TryEnqueue(() => _window.ShowStatusBarMessage(
                    $"이전 OptiScaler Nightly 빌드 {removed}개를 제거했습니다 ({size} 확보)",
                    TimeSpan.FromSeconds(8)));
            });
        };
        Grid.SetColumn(combo, 1);
        row.Children.Add(combo);
        _osNightlyCleanupCombo = combo;
        return row;
    }

    private void FillOsDefaultVariants()
    {
        if (_osDefaultVariantCombo == null) return;
        _osDefaultVariantSelecting = true;
        var all = OsVariants.All();
        _osDefaultVariantValues = all.Select(v => v.Value).ToList();
        _osDefaultVariantCombo.Items.Clear();
        foreach (var (_, label) in all) _osDefaultVariantCombo.Items.Add(label);
        _osDefaultVariantSelecting = false;
    }

    private bool _osDefaultVariantSelecting;

    private void SelectOsDefaultVariant()
    {
        if (_osDefaultVariantCombo == null) return;
        _osDefaultVariantSelecting = true;
        int idx = _osDefaultVariantValues.IndexOf(ViewModel.Settings.OsDefaultVariant);
        _osDefaultVariantCombo.SelectedIndex = idx >= 0 ? idx : 0;
        _osDefaultVariantSelecting = false;
    }

    private void OsDefaultVariantCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_osDefaultVariantSelecting || _osDefaultVariantCombo == null) return;
        try
        {
            int idx = _osDefaultVariantCombo.SelectedIndex;
            if (idx < 0 || idx >= _osDefaultVariantValues.Count) return;
            ViewModel.SetOsDefaultVariant(_osDefaultVariantValues[idx]);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.OsDefaultVariantCombo_SelectionChanged] {ex.Message}");
        }
    }
}
