using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    // ── ReLimiter Target FPS ──────────────────────────────────────────────────

    /// <summary>VRR cap presets matching the MFG dialog pattern.</summary>
    private static readonly (int Fps, string Label)[] _ulTargetFpsPresets =
    {
        (59,  "59 FPS (60Hz VRR 상한)"),
        (73,  "73 FPS (75Hz VRR 상한)"),
        (97,  "97 FPS (100Hz VRR 상한)"),
        (116, "116 FPS (120Hz VRR 상한)"),
        (138, "138 FPS (144Hz VRR 상한)"),
        (157, "157 FPS (165Hz VRR 상한)"),
        (171, "171 FPS (180Hz VRR 상한)"),
        (189, "189 FPS (200Hz VRR 상한)"),
        (224, "224 FPS (240Hz VRR 상한)"),
        (258, "258 FPS (280Hz VRR 상한)"),
        (275, "275 FPS (300Hz VRR 상한)"),
        (324, "324 FPS (360Hz VRR 상한)"),
        (416, "416 FPS (480Hz VRR 상한)"),
        (431, "431 FPS (500Hz VRR 상한)"),
    };
    private static readonly HashSet<int> _ulTargetFpsPresetValues = new(_ulTargetFpsPresets.Select(p => p.Fps));

    private bool _suppressUlTargetFpsChange;

    private void InitializeUlTargetFpsCombo()
    {
        _suppressUlTargetFpsChange = true;
        var combo = _window.UlTargetFpsCombo;
        combo.Items.Clear();

        combo.Items.Add("꺼짐");
        foreach (var preset in _ulTargetFpsPresets)
            combo.Items.Add(preset.Label);

        // If current value is a custom FPS (not in presets), insert it before "Custom..."
        int currentFps = ViewModel.Settings.UlTargetFps;
        if (currentFps > 0 && !_ulTargetFpsPresetValues.Contains(currentFps))
            combo.Items.Add($"{currentFps} FPS (Custom)");

        combo.Items.Add("Custom...");

        // Select based on current value
        if (currentFps == 0)
            combo.SelectedIndex = 0; // Off
        else
        {
            int matchIdx = Array.FindIndex(_ulTargetFpsPresets, p => p.Fps == currentFps);
            if (matchIdx >= 0)
                combo.SelectedIndex = matchIdx + 1; // +1 for "Off" at index 0
            else
            {
                // Custom value — select the "(Custom)" item
                combo.SelectedIndex = combo.Items.Count - 2; // before "Custom..."
            }
        }

        _suppressUlTargetFpsChange = false;
    }

    public async void UlTargetFpsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_suppressUlTargetFpsChange) return;
            if (sender is not ComboBox combo || combo.SelectedIndex < 0) return;

            var selectedText = combo.SelectedItem as string ?? "";

            // "Custom..." opens a dialog for manual entry
            if (selectedText == "Custom...")
            {
                var panel = new StackPanel { Spacing = 8 };
                panel.Children.Add(new TextBlock
                {
                    Text = "사용자 지정 FPS 값을 입력하세요 (20-1000):",
                    FontSize = 12,
                });
                var inputBox = new TextBox
                {
                    PlaceholderText = "예: 165",
                    FontSize = 12,
                };
                panel.Children.Add(inputBox);

                var dialog = new ContentDialog
                {
                    Title = "사용자 지정 목표 FPS",
                    Content = panel,
                    PrimaryButtonText = "설정",
                    CloseButtonText = "취소",
                    XamlRoot = _window.Content.XamlRoot,
                    RequestedTheme = ElementTheme.Dark,
                };

                var result = await DialogService.ShowSafeAsync(dialog);
                if (result == ContentDialogResult.Primary &&
                    int.TryParse(inputBox.Text, out int customFps) &&
                    customFps >= 20 && customFps <= 1000)
                {
                    ViewModel.Settings.UlTargetFps = customFps;
                    ViewModel.SaveSettingsPublic();
                    InitializeUlTargetFpsCombo(); // Refresh to show custom value
                }
                else
                {
                    // User cancelled or invalid — revert selection
                    InitializeUlTargetFpsCombo();
                }
                return;
            }

            // Handle preset/Off selection
            int newFps;
            if (combo.SelectedIndex == 0)
                newFps = 0; // Off
            else if (combo.SelectedIndex - 1 < _ulTargetFpsPresets.Length)
                newFps = _ulTargetFpsPresets[combo.SelectedIndex - 1].Fps;
            else
                return; // Custom label item — don't set

            ViewModel.Settings.UlTargetFps = newFps;
            ViewModel.SaveSettingsPublic();
        }
        catch (Exception unhandledEx)
        {
            CrashReporter.Log($"[SettingsHandler.UlTargetFpsCombo_SelectionChanged] Unhandled error — {unhandledEx.Message}\n{unhandledEx.StackTrace}");
        }
    }

    // ── OptiScaler Hotkey ─────────────────────────────────────────────────────

    public void OsGpuCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is string selected)
        {
            ViewModel.Settings.OsGpuType = selected;
            ViewModel.SaveSettingsPublic();

            // Show DLSS inputs combo only for AMD or Intel
            bool showDlss = !string.Equals(selected, "NVIDIA", StringComparison.OrdinalIgnoreCase);
            _window.OsDlssInputsPanel.Visibility = showDlss ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public void OsDlssInputsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedIndex >= 0)
        {
            ViewModel.Settings.OsDlssInputs = combo.SelectedIndex == 1; // 0=Off, 1=On
            ViewModel.SaveSettingsPublic();
        }
    }

    public async Task GlobalUpdateInclusion_ClickAsync(object sender, RoutedEventArgs e)
    {
        var settings = ViewModel.Settings;
        var xamlRoot = (sender as FrameworkElement)?.XamlRoot ?? _window.Content.XamlRoot;
        if (xamlRoot == null) return;

        var rsCheck = new CheckBox { Content = "ReShade", IsChecked = !settings.GlobalSkipRsUpdates, FontSize = 12, Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush), Margin = new Thickness(0, 4, 0, 4) };
        var rdxCheck = new CheckBox { Content = "RenoDX", IsChecked = !settings.GlobalSkipRdxUpdates, FontSize = 12, Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush), Margin = new Thickness(0, 4, 0, 4) };
        var ulCheck = new CheckBox { Content = "ReLimiter", IsChecked = !settings.GlobalSkipUlUpdates, FontSize = 12, Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush), Margin = new Thickness(0, 4, 0, 4) };
        var dcCheck = new CheckBox { Content = "Display Commander", IsChecked = !settings.GlobalSkipDcUpdates, FontSize = 12, Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush), Margin = new Thickness(0, 4, 0, 4) };
        var osCheck = new CheckBox { Content = "OptiScaler", IsChecked = !settings.GlobalSkipOsUpdates, FontSize = 12, Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush), Margin = new Thickness(0, 4, 0, 4) };
        var refCheck = new CheckBox { Content = "RE Framework", IsChecked = !settings.GlobalSkipRefUpdates, FontSize = 12, Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush), Margin = new Thickness(0, 4, 0, 4) };

        var checkPanel = new StackPanel { Spacing = 0 };
        var inclusionHeader = new TextBlock { Text = "모두 업데이트에 전역으로 포함할 구성 요소:", FontSize = 12, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush), Margin = new Thickness(0, 0, 0, 8) };
        ToolTipService.SetToolTip(inclusionHeader,
            "구성 요소는 여기와 해당 게임의 업데이트 포함 설정에서 모두 켜져 있을 때만 업데이트됩니다.\n여기서 끄면 게임별 설정과 관계없이 모든 게임에서 건너뜁니다.");
        checkPanel.Children.Add(inclusionHeader);
        checkPanel.Children.Add(rsCheck);
        checkPanel.Children.Add(rdxCheck);
        checkPanel.Children.Add(ulCheck);
        checkPanel.Children.Add(dcCheck);
        checkPanel.Children.Add(osCheck);
        checkPanel.Children.Add(refCheck);

        var dialog = new ContentDialog
        {
            Title = "전역 업데이트 포함",
            Content = checkPanel,
            PrimaryButtonText = "저장",
            CloseButtonText = "취소",
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        var result = await DialogService.ShowSafeAsync(dialog);
        if (result == ContentDialogResult.Primary)
        {
            settings.GlobalSkipRsUpdates = !(rsCheck.IsChecked == true);
            settings.GlobalSkipRdxUpdates = !(rdxCheck.IsChecked == true);
            settings.GlobalSkipUlUpdates = !(ulCheck.IsChecked == true);
            settings.GlobalSkipDcUpdates = !(dcCheck.IsChecked == true);
            settings.GlobalSkipOsUpdates = !(osCheck.IsChecked == true);
            settings.GlobalSkipRefUpdates = !(refCheck.IsChecked == true);
            ViewModel.SaveSettingsPublic();
            RefreshGlobalUpdateSummary();
        }
    }

    public void RefreshGlobalUpdateSummary()
    {
        var tb = _window.GlobalUpdateSummaryText;
        tb.Inlines.Clear();
        var settings = ViewModel.Settings;
        var items = new (string label, bool isOn)[]
        {
            ("ReShade", !settings.GlobalSkipRsUpdates),
            ("RenoDX", !settings.GlobalSkipRdxUpdates),
            ("ReLimiter", !settings.GlobalSkipUlUpdates),
            ("Display Commander", !settings.GlobalSkipDcUpdates),
            ("OptiScaler", !settings.GlobalSkipOsUpdates),
            ("RE Framework", !settings.GlobalSkipRefUpdates),
        };
        for (int i = 0; i < items.Length; i++)
        {
            var (label, isOn) = items[i];
            tb.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
            {
                Text = $"{label}: ",
                Foreground = UIFactory.Brush(ResourceKeys.SettingsLabelBrush),
            });
            tb.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
            {
                Text = isOn ? "켜짐" : "꺼짐",
                Foreground = UIFactory.Brush(isOn ? ResourceKeys.AccentGreenBrush : ResourceKeys.AccentRedBrush),
            });
            if (i < items.Length - 1)
            {
                tb.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
                {
                    Text = "  ·  ",
                    Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
                });
            }
        }
    }

    public void CacheAllShadersCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.Settings.IsLoadingSettings) return;
        if (sender is ComboBox combo)
        {
            ViewModel.Settings.CacheAllShaders = combo.SelectedIndex == 1;
            ViewModel.SaveSettingsPublic();
        }
    }

    public void OsHotkeyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is string selected)
        {
            _currentOsHotkeyString = selected;

            // Persist immediately and write to INIs_Folder
            ViewModel.Settings.OsHotkey = _currentOsHotkeyString;
            ViewModel.SaveSettingsPublic();

            // Write ShortcutKey to the OptiScaler.ini template in INIs_Folder
            try
            {
                Directory.CreateDirectory(AuxInstallService.InisDir);
                OptiScalerService.WriteShortcutKey(OptiScalerService.OsIniPath, _currentOsHotkeyString);
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[SettingsHandler.OsHotkeyCombo_SelectionChanged] Failed to write ShortcutKey — {ex.Message}");
            }
        }
    }

    public async void ApplyOsHotkey_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.Settings.OsHotkey = _currentOsHotkeyString;
            ViewModel.SaveSettingsPublic();

            // Write to INIs_Folder template
            try
            {
                Directory.CreateDirectory(AuxInstallService.InisDir);
                OptiScalerService.WriteShortcutKey(OptiScalerService.OsIniPath, _currentOsHotkeyString);
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[SettingsHandler.ApplyOsHotkey_Click] Failed to write template — {ex.Message}");
            }

            // Apply to all games where OptiScaler is installed
            int updatedCount = 0;
            foreach (var card in ViewModel.AllCards)
            {
                if (string.IsNullOrEmpty(card.InstallPath)) continue;
                if (card.IsHidden) continue; // hidden games are left alone
                if (!card.IsOsInstalled) continue;

                var gameIniPath = Path.Combine(card.InstallPath, OptiScalerService.IniFileName);
                if (!File.Exists(gameIniPath)) continue;

                try
                {
                    OptiScalerService.WriteShortcutKey(gameIniPath, _currentOsHotkeyString);
                    updatedCount++;
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[SettingsHandler.ApplyOsHotkey_Click] Failed for '{card.GameName}' — {ex.Message}");
                }
            }

            var dialog = new ContentDialog
            {
                Title = "OptiScaler 단축키",
                Content = $"OptiScaler.ini 파일 {updatedCount}개를 업데이트했습니다.",
                CloseButtonText = "확인",
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(dialog);
        }
        catch (Exception unhandledEx)
        {
            CrashReporter.Log($"[SettingsHandler.ApplyOsHotkey_Click] Unhandled error — {unhandledEx.Message}\n{unhandledEx.StackTrace}");
        }
    }
}
