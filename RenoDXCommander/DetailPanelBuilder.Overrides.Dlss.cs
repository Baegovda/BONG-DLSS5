// DetailPanelBuilder.Overrides.Dlss.cs — DLSS/Streamline column builder helpers.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    /// <summary>
    /// Builds a single DLSS/Streamline column with label, version ComboBox, optional preset ComboBox, and optional render scale ComboBox.
    /// </summary>
    private StackPanel BuildDlssColumn(string label, bool isPresent,
        IReadOnlyList<string> availableVersions, string? installedVersion,
        (string Name, uint Value)[]? presets, uint currentPreset,
        Func<string, Task> onVersionSelected, Action<uint>? onPresetSelected,
        uint currentRenderScale = 0, Action<uint>? onRenderScaleSelected = null,
        string? originalVersion = null, bool driverOverrideActive = false,
        Action<bool>? onDriverOverrideToggled = null)
    {
        var col = new StackPanel { Spacing = 4, Opacity = isPresent ? 1.0 : 0.4 };

        var columnLabel = new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
        };
        // The column is dimmed (and its version combo usually disabled) when the DLL is
        // missing or is 1.x. Disabled controls show no tooltip, so the reason goes on the label.
        if (!isPresent)
            ToolTipService.SetToolTip(columnLabel,
                installedVersion?.StartsWith("1.") == true
                    ? "DLSS / Streamline 1.x는 2.x 이상으로 교체할 수 없습니다."
                    : "이 게임에는 이 DLL이 없습니다.");
        col.Children.Add(columnLabel);

        // Version ComboBox
        var versionLabel = new TextBlock { Text = "버전", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) };
        col.Children.Add(versionLabel);

        // Build items list with (Default) marker on the game's original/default version
        var items = new List<string>();

        if (!isPresent && installedVersion == null && onDriverOverrideToggled == null)
        {
            // Game truly doesn't have this component — show "None"
            items.Add("None");
        }
        else
        {
            string? formattedOriginal = originalVersion != null
                ? DlssStreamlineService.FormatVersion(originalVersion)
                : (installedVersion != null ? installedVersion : null);

            foreach (var ver in availableVersions)
                items.Add(ver);
            items.Add("Custom");

            // Add a "Default (x.x.x)" entry at the top so the user can restore the original
            if (formattedOriginal != null)
            {
                var defaultLabel = $"Default ({formattedOriginal})";
                items.Insert(0, defaultLabel);
            }
            else
            {
                items.Insert(0, "Default");
            }

            // "NVIDIA Override" as a selectable option when the caller supports it
            if (onDriverOverrideToggled != null)
                items.Add("NVIDIA Override");
        }

        // Find selected index based on installed version (or NVIDIA Override if active)
        int selectedIndex = 0;
        if (driverOverrideActive && onDriverOverrideToggled != null)
        {
            // Select the "NVIDIA Override" entry at the end of the list
            selectedIndex = items.Count - 1;
        }
        else if (installedVersion != null && (isPresent || onDriverOverrideToggled != null))
        {
            if (installedVersion.Equals("Custom", StringComparison.OrdinalIgnoreCase))
            {
                // "Custom" is second-to-last (before "NVIDIA Override" if present)
                selectedIndex = onDriverOverrideToggled != null ? items.Count - 2 : items.Count - 1;
            }
            else
            {
                bool matched = false;
                for (int i = 0; i < items.Count; i++)
                {
                    // Strip the "(Default)" suffix if present for comparison
                    var itemBase = items[i];
                    if (itemBase.StartsWith("Default (", StringComparison.OrdinalIgnoreCase))
                        itemBase = itemBase.Substring("Default (".Length).TrimEnd(')');

                    if (installedVersion.Equals(itemBase, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                        matched = true;
                        break;
                    }
                }

                // Installed version not in manifest list (e.g. early access / custom build)
                // Insert it before "Custom" so it shows correctly rather than falling back to (Default)
                if (!matched)
                {
                    var insertIdx = onDriverOverrideToggled != null ? items.Count - 2 : items.Count - 1; // before Custom / NVIDIA Override
                    items.Insert(insertIdx, installedVersion);
                    selectedIndex = insertIdx;
                }
            }
        }

        var versionCombo = new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = selectedIndex,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = isPresent || (onDriverOverrideToggled != null),
            Opacity = 1.0,
            MaxDropDownHeight = 300,
        };

        if (driverOverrideActive)
            ToolTipService.SetToolTip(versionCombo, "NVIDIA Override가 활성화되어 있습니다 — 드라이버가 이 게임에 자체 최신 DLL을 주입하고 있습니다. 다른 버전을 선택하면 재정의를 끄고 해당 버전을 배포합니다.");
        else if (onDriverOverrideToggled != null)
            ToolTipService.SetToolTip(versionCombo, "게임 폴더에 복사할 DLL 버전을 선택합니다. Default는 게임의 원래 DLL을 복원합니다. Custom은 %LocalAppData%\\RHI\\Custom\\DLSS\\의 사용자 파일을 사용합니다. NVIDIA Override는 디스크의 파일 대신 드라이버가 자체 최신 버전을 주입하게 합니다 — NVIDIA App이나 Profile Inspector에서 DLSS Override를 켜는 것과 같습니다.");
        else
            ToolTipService.SetToolTip(versionCombo, "게임 폴더에 복사할 DLL 버전을 선택합니다. Default는 게임의 원래 DLL을 복원합니다. Custom은 %LocalAppData%\\RHI\\Custom\\DLSS\\의 사용자 파일을 사용합니다.");

        col.Children.Add(versionCombo);

        bool versionInit = true;
        versionCombo.SelectionChanged += async (s, ev) =>
        {
            if (versionInit) return;
            var selected = versionCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(selected)) return;

            if (selected == "NVIDIA Override")
            {
                // Enable driver DLL override — no DLL swap needed
                onDriverOverrideToggled?.Invoke(true);
                return;
            }

            // If we were on NVIDIA Override and switched away, disable it first
            if (driverOverrideActive || (ev.RemovedItems.Count > 0 && ev.RemovedItems[0] as string == "NVIDIA Override"))
                onDriverOverrideToggled?.Invoke(false);

            versionCombo.IsEnabled = false;
            try
            {
                // If it's the (Default) item, treat as "Default" (restore original)
                if (selected.StartsWith("Default", StringComparison.OrdinalIgnoreCase))
                    await onVersionSelected("Default");
                else
                    await onVersionSelected(selected);
            }
            catch (DlssSwapException ex)
            {
                CrashReporter.Log($"[DetailPanelBuilder.BuildDlssColumn] {label} swap to '{selected}' failed — {ex.Message}");
                // Put the dropdown back to what is actually on disk
                if (ev.RemovedItems.Count > 0)
                {
                    versionInit = true;
                    versionCombo.SelectedItem = ev.RemovedItems[0];
                    versionInit = false;
                }
                await ShowDlssSwapErrorAsync(ex.Message);
            }
            finally
            {
                versionCombo.IsEnabled = isPresent || (onDriverOverrideToggled != null);
            }
        };
        versionInit = false;

        // Preset ComboBox (only for SR, RR, FG)
        if (presets != null && isPresent)
        {
            col.Children.Add(new TextBlock { Text = "프리셋", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });

            var presetItems = presets.Select(p => p.Name).ToList();
            int presetIdx = 0;
            for (int i = 0; i < presets.Length; i++)
            {
                if (presets[i].Value == currentPreset) { presetIdx = i; break; }
            }

            var presetCombo = new ComboBox
            {
                ItemsSource = presetItems,
                SelectedIndex = presetIdx,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = isPresent,
                MaxDropDownHeight = 300,
            };

            // Add tooltip explaining presets
            string presetTooltip = label switch
            {
                "DLSS Super Resolution" => "DLSS 업스케일링 모델을 재정의합니다. J/K는 1세대 트랜스포머(DLSS 4.0)를 사용합니다. L/M은 시간적 안정성이 더 좋은 2세대 트랜스포머(DLSS 4.5)를 사용합니다. NVIDIA Recommended는 NVIDIA의 해상도별 프리셋 선택을 사용합니다.",
                "Ray Reconstruction" => "레이 재구성 노이즈 제거 모델을 재정의합니다. 높은 프리셋일수록 최신 모델입니다. NVIDIA Recommended는 NVIDIA의 해상도별 프리셋 선택을 사용합니다.",
                "Frame Generation" => "프레임 생성 보간 모델을 재정의합니다. 높은 프리셋일수록 최신 모델입니다. NVIDIA Recommended는 NVIDIA의 해상도별 프리셋 선택을 사용합니다.",
                _ => ""
            };
            if (!string.IsNullOrEmpty(presetTooltip))
                ToolTipService.SetToolTip(presetCombo, presetTooltip);

            bool presetInit = true;
            presetCombo.SelectionChanged += (s, ev) =>
            {
                if (presetInit) return;
                var idx = presetCombo.SelectedIndex;
                if (idx >= 0 && idx < presets.Length)
                    onPresetSelected?.Invoke(presets[idx].Value);
            };
            presetInit = false;
            col.Children.Add(presetCombo);
        }

        // Render Scale ComboBox (only for SR and RR)
        if (onRenderScaleSelected != null && isPresent)
        {
            col.Children.Add(new TextBlock { Text = "렌더 배율", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
            var rsOptions = DlssPresetService.RenderScaleOptions;
            var rsItems = rsOptions.Select(o => o.Name).ToList();

            // Determine current selection
            int rsIdx = 0; // Off
            if (currentRenderScale > 0)
            {
                // Check if it matches a named option
                int namedIdx = Array.FindIndex(rsOptions, o => o.Value == currentRenderScale);
                if (namedIdx >= 0)
                    rsIdx = namedIdx;
                else
                    rsIdx = rsItems.Count - 1; // Custom
            }

            // If Custom is selected, show the percentage in the item text
            if (rsIdx == rsItems.Count - 1 && currentRenderScale > 0)
                rsItems[^1] = $"사용자 지정 ({currentRenderScale}%)";

            var rsCombo = new ComboBox
            {
                ItemsSource = rsItems,
                SelectedIndex = rsIdx,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = isPresent,
                MaxDropDownHeight = 300,
            };
            ToolTipService.SetToolTip(rsCombo,
                "DLSS 렌더 해상도 배율을 재정의합니다. Off = 게임이 배율을 제어합니다.\n이름 있는 프리셋은 고정 비율을 설정합니다. Custom은 33-100% 사이의 값을 직접 입력할 수 있습니다.");

            bool rsInit = true;
            rsCombo.SelectionChanged += (s, ev) =>
            {
                if (rsInit) return;
                var idx = rsCombo.SelectedIndex;
                if (idx < 0 || idx >= rsOptions.Length) return;

                if (rsOptions[idx].Name == "Custom")
                {
                    // Show a TextBox inline — replace the combo temporarily
                    var parent = rsCombo.Parent as StackPanel;
                    if (parent == null) return;
                    var comboIdx = parent.Children.IndexOf(rsCombo);
                    var inputBox = new TextBox
                    {
                        PlaceholderText = "33-100",
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        MaxLength = 3,
                    };
                    ToolTipService.SetToolTip(inputBox,
                        "출력 해상도 대비 렌더 배율(33~100%). Enter로 적용, Esc로 취소.");
                    inputBox.KeyDown += (ks, ke) =>
                    {
                        if (ke.Key == Windows.System.VirtualKey.Enter)
                        {
                            ke.Handled = true;
                            // Defocus the TextBox by briefly disabling it — WinUI 3 has no
                            // direct "clear focus" API, and FocusManager.TryMoveFocus is
                            // unreliable in imperative panel contexts.
                            inputBox.IsEnabled = false;
                            inputBox.IsEnabled = true;
                            if (uint.TryParse(inputBox.Text, out var val) && val >= 33 && val <= 100)
                            {
                                onRenderScaleSelected(val);
                            }
                            else
                            {
                                // Invalid — revert to Off
                                onRenderScaleSelected(0);
                            }
                        }
                        else if (ke.Key == Windows.System.VirtualKey.Escape)
                        {
                            ke.Handled = true;
                            inputBox.IsEnabled = false;
                            inputBox.IsEnabled = true;
                            // Cancel — revert
                            onRenderScaleSelected(currentRenderScale);
                        }
                    };
                    inputBox.LostFocus += (ls, le) =>
                    {
                        if (uint.TryParse(inputBox.Text, out var val) && val >= 33 && val <= 100)
                            onRenderScaleSelected(val);
                        else
                            onRenderScaleSelected(currentRenderScale); // revert
                    };
                    parent.Children[comboIdx] = inputBox;
                    inputBox.Focus(FocusState.Programmatic);
                }
                else
                {
                    onRenderScaleSelected(rsOptions[idx].Value);
                }
            };
            rsInit = false;
            col.Children.Add(rsCombo);
        }

        return col;
    }

    private static Border MakeDlssDivider(int column)
    {
        var divider = new Border
        {
            Width = 1,
            Background = UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 0),
        };
        Grid.SetColumn(divider, column);
        return divider;
    }

    /// <summary>Tells the user why a DLSS / Streamline swap couldn't be applied.</summary>
    private async Task ShowDlssSwapErrorAsync(string message)
    {
        var xamlRoot = _window.Content?.XamlRoot;
        if (xamlRoot == null) return;
        await DialogService.ShowSafeAsync(new ContentDialog
        {
            Title = "DLSS 파일을 변경할 수 없습니다",
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "확인",
            XamlRoot = xamlRoot,
        });
    }
}