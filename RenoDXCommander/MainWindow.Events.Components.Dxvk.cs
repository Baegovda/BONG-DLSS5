// MainWindow.Events.Components.Dxvk.cs — DXVK cog dialog handler (DxvkCogButton_Click).
// Built with CogDialog: every setting applies as soon as it changes; results show in the dialog.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    internal async void DxvkCogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
            var gameName = card.GameName;
            var store    = card.Source ?? "";

            // Pre-fetch both values off the UI thread — each calls _sessionLock.Wait(5000)
            // and will block the UI thread if NVAPI is hung after GPU sleep/wake.
            var (currentPresentMethod, currentPresentFlags) = await Task.Run(() => (
                _dlssPresetService.GetVulkanPresentMethod(card.GameName, card.InstallPath ?? ""),
                _dlssPresetService.GetVulkanPresentMethodFlags(card.GameName, card.InstallPath ?? "")
            ));

            var cog = new CogDialog("DXVK", Content.XamlRoot);

            // ── Variant selector ──────────────────────────────────────────────────
            // Resolve current variant — Lilium HDR is default when no override is set
            var currentOverride = ViewModel.GetDxvkVariantOverride(gameName, store);
            string currentVariantDisplay;
            if (currentOverride != null)
            {
                currentVariantDisplay = currentOverride switch
                {
                    "Stable"    => "Stable",
                    "LiliumHdr" => "Lilium HDR",
                    _           => "Development",
                };
            }
            else if (card.DxvkEnabled)
            {
                // Already installed — show effective global variant
                currentVariantDisplay = ViewModel.ResolveDxvkVariant(gameName, store) switch
                {
                    DxvkVariant.Stable    => "Stable",
                    DxvkVariant.LiliumHdr => "Lilium HDR",
                    _                     => "Development",
                };
            }
            else
            {
                // Default: Lilium HDR
                currentVariantDisplay = "Lilium HDR";
            }

            var variantItems = new List<string> { "Lilium HDR", "Development", "Stable" };
            int variantIdx = variantItems.IndexOf(currentVariantDisplay);
            if (variantIdx < 0) variantIdx = 0;

            cog.AddSection("DXVK");

            FrameworkElement? liliumPresetRow = null;
            ComboBox? variantCombo = null;
            variantCombo = cog.AddCombo("변형",
                "Lilium HDR(기본값)은 HDR 중심 빌드입니다. Development는 최신 나이틀리 빌드, Stable은 마지막 릴리스입니다. " +
                "DXVK가 설치된 게임에서 변경하면 새 변형을 설치할 수 있도록 DXVK가 제거됩니다.",
                variantItems, variantIdx, i =>
                {
                    var selected = variantItems[i];
                    if (liliumPresetRow != null)
                        liliumPresetRow.Visibility = selected == "Lilium HDR" ? Visibility.Visible : Visibility.Collapsed;
                    if (variantCombo != null)
                        _ = DxvkCogChangeVariantAsync(cog, card, variantCombo, selected);
                    return true;
                });

            // ── Lilium HDR Preset row (shown when Lilium selected) ─────────────────
            bool isLiliumSelected = variantItems[variantIdx] == "Lilium HDR";
            var isDx9Api = card.DxvkRecord?.InstalledDlls?.Any(d => d.Equals("d3d9.dll", StringComparison.OrdinalIgnoreCase)) == true
                           || card.GraphicsApi is GraphicsApiType.DirectX8 or GraphicsApiType.DirectX9;
            var presetArray = isDx9Api ? DxvkService.LiliumD3d9Presets : DxvkService.LiliumD3d11Presets;
            var presetNames = presetArray.Select(p => p.Name).ToList();
            int currentPresetIdx = ViewModel.GetLiliumPreset(gameName, store);

            var liliumPresetCombo = cog.AddCombo("Lilium 프리셋",
                "Safest는 스왑 체인만 업그레이드하며 거의 모든 곳에서 작동합니다. 상위 단계는 백 버퍼와 " +
                "렌더 타깃도 업그레이드해 HDR이 더 좋아지지만 시각적 문제가 생길 수 있습니다. 변경하면 dxvk.conf를 다시 작성합니다.",
                presetNames,
                currentPresetIdx >= 0 && currentPresetIdx < presetNames.Count ? currentPresetIdx : 0,
                idx =>
                {
                    ViewModel.SetLiliumPreset(gameName, idx, store);
                    // Re-deploy dxvk.conf with new preset if DXVK is installed
                    if (!string.IsNullOrEmpty(card.InstallPath))
                        _ = DxvkCogWriteLiliumConfAsync(cog, card.InstallPath, isDx9Api, idx, presetNames[idx]);
                    else
                        cog.SetSuccess($"Lilium 프리셋을 {presetNames[idx]}(으)로 설정했습니다.");
                });
            // AddRow puts the combo in a grid inside the row panel: hide the whole row when not Lilium
            liliumPresetRow = (liliumPresetCombo.Parent as FrameworkElement)?.Parent as FrameworkElement;
            if (liliumPresetRow != null)
                liliumPresetRow.Visibility = isLiliumSelected ? Visibility.Visible : Visibility.Collapsed;

            // ── Deploy dxvk.conf button ───────────────────────────────────────────
            cog.AddSection("dxvk.conf");
            cog.AddButtons(cog.MakeButton("dxvk.conf 배포", CogButtonKind.Primary, () =>
            {
                var error = ViewModel.CopyDxvkConf(card);
                if (error == null) cog.SetSuccess("dxvk.conf를 게임 폴더에 복사했습니다.");
                else cog.SetError($"dxvk.conf를 복사할 수 없습니다: {error}");
            }));
            cog.AddNote("dxvk.conf를 게임 폴더에 복사합니다: Lilium HDR 설치에는 선택한 Lilium 프리셋을, 그 외에는 dxvk.conf 템플릿을 사용합니다.");

            // ── Vulkan/OpenGL Present Method ──────────────────────────────────────
            cog.AddSection("NVIDIA 드라이버 프로필");

            var flagOptions = new (string Label, uint Value)[]
            {
                ("표준",    0x000802A5u),
                ("대체", 0x00080004u),
            };
            bool presentIsYes = currentPresentMethod == 0x00000001;
            ComboBox? flagsCombo = null;

            // Prefer DXGI Swapchain: No = 0x00000002 (Auto), Yes = 0x00000001 (Preferred layered on DXGI Swapchain)
            cog.AddCombo("Prefer DXGI Swapchain",
                "게임의 NVIDIA 드라이버 프로필에서 Vulkan/OpenGL 표시 방식을 'Prefer layered on DXGI Swapchain'으로 설정합니다. " +
                "DXVK에 권장: 호환성과 HDR 지원이 더 좋습니다.",
                new List<string> { "아니요", "예" }, presentIsYes ? 1 : 0, i =>
                {
                    bool isYes = i == 1;
                    uint value = isYes ? 0x00000001u : 0x00000002u;
                    if (flagsCombo != null)
                    {
                        flagsCombo.IsEnabled = isYes;
                        flagsCombo.Opacity = isYes ? 1.0 : CogDialog.DisabledOpacity;
                    }
                    // No → clear the flags; Yes → write the selected flags
                    uint? flagsValue = null;
                    if (!isYes)
                        flagsValue = 0x00000000u;
                    else if (flagsCombo != null && flagsCombo.SelectedIndex >= 0 && flagsCombo.SelectedIndex < flagOptions.Length)
                        flagsValue = flagOptions[flagsCombo.SelectedIndex].Value;
                    var capturedName = card.GameName; var capturedPath = card.InstallPath ?? "";
                    _ = DxvkCogProfileWriteAsync(cog, "Prefer DXGI Swapchain", () =>
                    {
                        bool ok = _dlssPresetService.SetVulkanPresentMethod(capturedName, capturedPath, value);
                        if (flagsValue.HasValue)
                            ok &= _dlssPresetService.SetVulkanPresentMethodFlags(capturedName, capturedPath, flagsValue.Value);
                        return ok;
                    });
                });

            // DXVK as Native flags — only active when Prefer DXGI Swapchain is Yes
            int flagsIdx = 0;
            if (presentIsYes)
            {
                var flagMatch = Array.FindIndex(flagOptions, f => f.Value == currentPresentFlags);
                flagsIdx = flagMatch >= 0 ? flagMatch : 0; // default to Standard
            }
            flagsCombo = cog.AddCombo("DXVK as Native",
                "표준은 DXVK를 네이티브로 처리합니다. 대체는 DXVK 승격과 DirectFlip을 허용합니다. Prefer DXGI Swapchain이 예일 때만 사용됩니다.",
                flagOptions.Select(f => f.Label).ToList(), flagsIdx, i =>
                {
                    var capturedName = card.GameName; var capturedPath = card.InstallPath ?? "";
                    var capturedVal = flagOptions[i].Value;
                    _ = DxvkCogProfileWriteAsync(cog, "DXVK as Native",
                        () => _dlssPresetService.SetVulkanPresentMethodFlags(capturedName, capturedPath, capturedVal));
                }, isEnabled: presentIsYes);
            if (!presentIsYes) flagsCombo.Opacity = CogDialog.DisabledOpacity;

            await cog.ShowAsync();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.DxvkCogButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Saves the per-game DXVK variant override. If DXVK is installed it's uninstalled first so the
    /// user can install the new variant; the detail panel is rebuilt either way.
    /// </summary>
    private async Task DxvkCogChangeVariantAsync(CogDialog cog, GameCardViewModel card, ComboBox variantCombo, string selected)
    {
        var gameName = card.GameName;
        var store    = card.Source ?? "";
        string variantValue = selected switch
        {
            "Lilium HDR"  => "LiliumHdr",
            "Development" => "Development",
            "Stable"      => "Stable",
            _             => "LiliumHdr",
        };
        variantCombo.IsEnabled = false;
        try
        {
            ViewModel.SetDxvkVariantOverride(gameName, variantValue, store);

            bool wasInstalled = card.DxvkEnabled || card.DxvkStatus == GameStatus.Installed || card.DxvkStatus == GameStatus.UpdateAvailable;
            if (wasInstalled)
            {
                // Already installed — uninstall first so user can install the new variant
                cog.SetStatus("DXVK 제거 중...");
                await ViewModel.HandleDxvkToggleAsync(card, false, Content.XamlRoot);
            }
            card.DxvkVariantPending = true;
            card.NotifyAll();
            PopulateDetailPanel(card);
            BuildOverridesPanel(card);

            cog.SetSuccess(wasInstalled
                ? $"변형을 {selected}(으)로 설정했습니다. DXVK가 제거되었으니 새 변형을 사용하려면 다시 설치하세요."
                : $"변형을 {selected}(으)로 설정했습니다. DXVK를 설치할 때 사용됩니다.");
            CrashReporter.Log($"[MainWindow.DxvkCogChangeVariantAsync] '{gameName}' variant → {variantValue} (wasInstalled={wasInstalled})");
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.DxvkCogChangeVariantAsync] Failed for '{gameName}' — {ex.Message}\n{ex.StackTrace}");
            cog.SetError($"변형을 변경할 수 없습니다: {ex.Message}");
        }
        finally
        {
            variantCombo.IsEnabled = true;
        }
    }

    /// <summary>Rewrites the game's dxvk.conf with the chosen Lilium HDR preset (off the UI thread).</summary>
    private static async Task DxvkCogWriteLiliumConfAsync(CogDialog cog, string installPath, bool isDx9Api, int idx, string presetName)
    {
        var confPath = Path.Combine(installPath, "dxvk.conf");
        var confContent = isDx9Api
            ? DxvkService.GetLiliumD3d9ConfContent(idx)
            : DxvkService.GetLiliumD3d11ConfContent(idx);
        try
        {
            await Task.Run(() => File.WriteAllText(confPath, confContent));
            cog.SetSuccess($"Lilium 프리셋을 {presetName}(으)로 설정했습니다. dxvk.conf가 업데이트되었습니다.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashReporter.Log($"[DxvkCog.LiliumPreset] Failed to write dxvk.conf — {ex.Message}");
            cog.SetError($"Lilium 프리셋은 저장되었지만 dxvk.conf를 쓸 수 없습니다: {ex.Message}");
        }
    }

    /// <summary>Runs an NVIDIA profile write off the UI thread and reports the result in the dialog.</summary>
    private static async Task DxvkCogProfileWriteAsync(CogDialog cog, string settingName, Func<bool> write)
    {
        try
        {
            bool ok = await Task.Run(write);
            if (ok) cog.SetSuccess($"{settingName}을(를) NVIDIA 드라이버 프로필에 저장했습니다.");
            else cog.SetError($"{settingName}을(를) NVIDIA 드라이버 프로필에 저장할 수 없습니다. 자세한 내용은 로그를 확인하세요.");
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.DxvkCogProfileWriteAsync] {settingName} write failed — {ex.Message}");
            cog.SetError($"{settingName}을(를) 저장할 수 없습니다: {ex.Message}");
        }
    }
}
