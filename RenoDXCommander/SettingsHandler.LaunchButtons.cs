// SettingsHandler.LaunchButtons.cs -- Settings › Display & HDR › Display › "When a game starts": two Show / Hide
// rows (built in C#, XAML constraint) for the HDR and Resolution buttons on game pages, each placed under its own
// setting. Hiding a button only hides it: a game's own HDR / resolution choice still applies at launch.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private bool _launchButtonRowsAdded;

    private void EnsureLaunchButtonRows()
    {
        if (_launchButtonRowsAdded) return;
        _launchButtonRowsAdded = true;
        try
        {
            AddLaunchButtonRow(_window.HdrAutoToggleCombo, "게임 페이지의 HDR 버튼",
                "게임 페이지에서 실행 시 HDR 버튼을 표시하거나 숨깁니다. 숨겨도 RHI에서 실행하면 게임별 HDR 설정은 계속 적용됩니다.",
                () => ViewModel.Settings.ShowHdrLaunchButton, v => ViewModel.Settings.ShowHdrLaunchButton = v);
            AddLaunchButtonRow(_window.ResAutoToggleCombo, "게임 페이지의 해상도 버튼",
                "게임 페이지에서 해상도 전환 버튼을 표시하거나 숨깁니다. 숨겨도 RHI에서 실행하면 게임별 해상도 설정은 계속 적용됩니다.",
                () => ViewModel.Settings.ShowResLaunchButton, v => ViewModel.Settings.ShowResLaunchButton = v);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            CrashReporter.Log($"[SettingsHandler.EnsureLaunchButtonRows] Failed — {ex.Message}");
        }
    }

    /// <summary>Inserts a Show / Hide row just below the setting row that holds <paramref name="anchor"/>.</summary>
    private void AddLaunchButtonRow(FrameworkElement anchor, string label, string tooltip, Func<bool> get, Action<bool> set)
    {
        // Searched down from the card: FrameworkElement.Parent was null for these XAML elements
        var path = new List<UIElement>();
        bool found = FindPath(_window.ResolutionControlCard, anchor, path);
        int ci = found ? path.FindLastIndex(e => e is StackPanel) : -1; // the column; the next element is the row
        if (ci < 0 || ci + 1 >= path.Count || path[ci] is not StackPanel column || path[ci + 1] is not FrameworkElement row)
        {
            CrashReporter.Log($"[SettingsHandler.AddLaunchButtonRow] '{label}' — setting row not found, not added");
            return;
        }
        var combo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
        combo.Items.Add(new ComboBoxItem { Content = "표시" });
        combo.Items.Add(new ComboBoxItem { Content = "숨기기" });
        combo.SelectedIndex = get() ? 0 : 1;
        ToolTipService.SetToolTip(combo, tooltip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(combo, label);
        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedIndex < 0) return;
            bool show = combo.SelectedIndex == 0;
            if (get() == show) return;
            set(show);
            ViewModel.SaveSettingsPublic();
            CrashReporter.Log($"[SettingsHandler.LaunchButtons] {label}: {(show ? "Show" : "Hide")}");
            _window.RefreshLaunchButtonsShown();
        };
        column.Children.Insert(column.Children.IndexOf(row) + 1, SettingRow(label, combo));
    }

    /// <summary>Fills <paramref name="path"/> with the elements from <paramref name="node"/> down to <paramref name="target"/>.</summary>
    private static bool FindPath(UIElement node, UIElement target, List<UIElement> path)
    {
        path.Add(node);
        if (ReferenceEquals(node, target)) return true;
        IEnumerable<UIElement> kids = node switch
        {
            Panel p => p.Children,
            Border b when b.Child != null => new[] { b.Child },
            ContentControl c when c.Content is UIElement u => new[] { u },
            _ => Array.Empty<UIElement>(),
        };
        foreach (var k in kids)
            if (FindPath(k, target, path)) return true;
        path.RemoveAt(path.Count - 1);
        return false;
    }
}
