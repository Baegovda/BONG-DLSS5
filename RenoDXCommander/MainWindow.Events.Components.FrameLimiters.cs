// MainWindow.Events.Components.FrameLimiters.cs — ReLimiter (UL) and Display Commander (DC) cog dialog handlers.

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
    /// <summary>VRR frame rate caps (same as the global settings), used by the ReLimiter and Display Commander ⚙.</summary>
    private static readonly (int Fps, string Label)[] VrrFpsPresets =
    {
        (59,  "59 (60Hz VRR)"),
        (73,  "73 (75Hz VRR)"),
        (97,  "97 (100Hz VRR)"),
        (116, "116 (120Hz VRR)"),
        (138, "138 (144Hz VRR)"),
        (157, "157 (165Hz VRR)"),
        (171, "171 (180Hz VRR)"),
        (189, "189 (200Hz VRR)"),
        (224, "224 (240Hz VRR)"),
        (258, "258 (280Hz VRR)"),
        (275, "275 (300Hz VRR)"),
        (324, "324 (360Hz VRR)"),
        (416, "416 (480Hz VRR)"),
        (431, "431 (500Hz VRR)"),
    };

    private async void UlCogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
            // relimiter.ini lives in the addon deploy path (honours AddonPath in reshade.ini)
            string? ulIniFile = string.IsNullOrEmpty(card.InstallPath)
                ? null
                : Path.Combine(ModInstallService.GetAddonDeployPath(card.InstallPath), "relimiter.ini");
            bool ulIniExists = ulIniFile != null && File.Exists(ulIniFile);

            // Re-resolved on every write, as before (AddonPath may change while the dialog is open)
            string? CurrentUlIniFile() => string.IsNullOrEmpty(card.InstallPath)
                ? null
                : Path.Combine(ModInstallService.GetAddonDeployPath(card.InstallPath), "relimiter.ini");

            // Find the relimiter log file (relimiter_*.log)
            string? logFile = null;
            if (!string.IsNullOrEmpty(card.InstallPath) && Directory.Exists(card.InstallPath))
            {
                try
                {
                    logFile = Directory.GetFiles(card.InstallPath, "relimiter_*.log").FirstOrDefault();
                }
                catch (Exception ex) { CrashReporter.Log($"[MainWindow.UlCogButton_Click] Failed to locate relimiter log file — {ex.Message}"); }
            }

            // Read current per-game values from the game's relimiter.ini
            int currentTargetFps = 0;
            bool currentDlssHooks = ViewModel.Settings.UlDlssHooks; // default to global
            if (ulIniExists)
            {
                try
                {
                    var ulIni = AuxInstallService.ParseIni(File.ReadAllLines(ulIniFile!));
                    if (ulIni.TryGetValue("FrameLimiter", out var flSection))
                    {
                        if (flSection.TryGetValue("target_fps", out var fpsVal) && int.TryParse(fpsVal, out var parsedFps))
                            currentTargetFps = parsedFps;
                        if (flSection.TryGetValue("dlss_info_hooks", out var hooksVal))
                            currentDlssHooks = hooksVal.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch (Exception ex) { CrashReporter.Log($"[MainWindow.UlCogButton_Click] Failed to read relimiter.ini, using defaults — {ex.Message}"); }
            }

            var cog = new CogDialog("ReLimiter", Content.XamlRoot);

            // ── Frame Limiter ─────────────────────────────────────────────────────
            cog.AddSection("프레임 제한기");
            if (!ulIniExists)
                cog.AddWarning("이 설정을 변경하려면 relimiter.ini를 배포하세요 (파일 아래).");

            var vrrPresets = VrrFpsPresets;
            var vrrFpsSet = new HashSet<int>(vrrPresets.Select(p => p.Fps));

            // Items: Off, presets, "<n> (Custom)" when the current value isn't a preset, "Custom..."
            List<string> BuildFpsItems(int fps)
            {
                var items = new List<string> { "꺼짐" };
                items.AddRange(vrrPresets.Select(p => p.Label));
                if (fps > 0 && !vrrFpsSet.Contains(fps))
                    items.Add($"{fps} (사용자 지정)");
                items.Add("Custom...");
                return items;
            }
            int FpsIndex(List<string> items, int fps)
            {
                if (fps == 0) return 0; // Off
                int matchIdx = Array.FindIndex(vrrPresets, p => p.Fps == fps);
                return matchIdx >= 0 ? matchIdx + 1 : items.Count - 2; // +1 for "Off"; custom item sits before "Custom..."
            }

            var fpsItems = BuildFpsItems(currentTargetFps);
            var targetFpsCombo = CogDialog.MakeCombo(fpsItems, FpsIndex(fpsItems, currentTargetFps));
            bool suppressFpsChange = false;

            var targetFpsRow = cog.AddRow("목표 FPS", targetFpsCombo,
                "이 게임의 프레임 제한으로, relimiter.ini에 저장됩니다. 각 VRR 프리셋은 해당 주사율 바로 아래 값입니다. " +
                "꺼짐은 제한을 해제하고, Custom...에서는 직접 값을 입력할 수 있습니다.");

            // Inline custom FPS input (shown when "Custom..." is selected; no nested dialogs)
            var customFpsBox = CogDialog.MakeTextBox("", "20-1000");
            var customFpsBtn = CogDialog.StyledButton("설정", CogButtonKind.Secondary, compact: true);
            var customFpsRow = cog.AddRow("사용자 지정 FPS", CogDialog.Inline(customFpsBox, customFpsBtn),
                "20~1000 사이의 프레임 레이트를 입력한 뒤 설정 또는 Enter를 누르세요.");
            customFpsRow.Visibility = Visibility.Collapsed;

            // Rebuild the combo after setting a custom value
            void RefreshFpsCombo(int newFps)
            {
                suppressFpsChange = true;
                currentTargetFps = newFps;
                var items = BuildFpsItems(newFps);
                targetFpsCombo.ItemsSource = items;
                targetFpsCombo.SelectedIndex = FpsIndex(items, newFps);
                customFpsRow.Visibility = Visibility.Collapsed;
                suppressFpsChange = false;
            }

            // Writes target_fps; returns false (with the error shown) on failure
            bool WriteTargetFps(int fps)
            {
                var iniFile = CurrentUlIniFile();
                if (iniFile == null || !File.Exists(iniFile))
                {
                    cog.SetError("relimiter.ini를 찾을 수 없습니다. 먼저 배포하세요.");
                    return false;
                }
                try
                {
                    AuxInstallService.ApplyUlTargetFps(iniFile, fps);
                    currentTargetFps = fps;
                    cog.SetSuccess(fps == 0 ? "이 게임의 목표 FPS를 껐습니다." : $"이 게임의 목표 FPS를 {fps}(으)로 설정했습니다.");
                    CrashReporter.Log($"[MainWindow.UlCogButton_Click] target_fps={fps} for '{card.GameName}'");
                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    cog.SetError($"목표 FPS를 저장할 수 없습니다: {ex.Message}");
                    CrashReporter.Log($"[MainWindow.UlCogButton_Click] target_fps write failed — {ex.Message}");
                    return false;
                }
            }

            targetFpsCombo.SelectionChanged += (s, ev) =>
            {
                if (suppressFpsChange) return;
                if (string.IsNullOrEmpty(card.InstallPath)) return;

                var selectedText = targetFpsCombo.SelectedItem as string ?? "";

                // "Custom..." shows the inline TextBox for manual entry
                if (selectedText == "Custom...")
                {
                    customFpsRow.Visibility = Visibility.Visible;
                    customFpsBox.Text = "";
                    customFpsBox.Focus(FocusState.Programmatic);
                    return;
                }

                customFpsRow.Visibility = Visibility.Collapsed;

                // Handle preset/Off selection
                int newFps;
                var idx = targetFpsCombo.SelectedIndex;
                if (idx == 0)
                    newFps = 0; // Off
                else if (idx > 0 && idx - 1 < vrrPresets.Length)
                    newFps = vrrPresets[idx - 1].Fps;
                else
                    return; // Custom label item — don't set

                WriteTargetFps(newFps);
            };

            void SetCustomFps()
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                if (!int.TryParse(customFpsBox.Text, out var customFps) || customFps < 20 || customFps > 1000)
                {
                    cog.SetError("20~1000 사이의 정수를 입력하세요.");
                    return;
                }
                if (WriteTargetFps(customFps))
                    RefreshFpsCombo(customFps);
            }
            customFpsBtn.Click += (s, ev) => SetCustomFps();
            customFpsBox.KeyDown += (s, ev) =>
            {
                if (ev.Key == Windows.System.VirtualKey.Enter)
                {
                    ev.Handled = true;
                    SetCustomFps();
                }
            };

            // ── Compatibility ─────────────────────────────────────────────────────
            cog.AddSection("호환성");
            var dlssHooksCombo = cog.AddCombo("DLSS Hooks",
                "ReLimiter 오버레이에 DLSS 버전 및 프리셋 정보를 표시합니다. 켰을 때 게임이 충돌하면 끄세요.",
                new[] { "꺼짐", "켜짐" }, currentDlssHooks ? 1 : 0, i =>
                {
                    var iniFile = CurrentUlIniFile();
                    if (iniFile == null || !File.Exists(iniFile))
                    {
                        cog.SetError("relimiter.ini를 찾을 수 없습니다. 먼저 배포하세요.");
                        return false;
                    }
                    try
                    {
                        AuxInstallService.ApplyUlDlssHooks(iniFile, i == 1);
                        cog.SetSuccess(i == 1 ? "이 게임의 DLSS Hooks를 켰습니다." : "이 게임의 DLSS Hooks를 껐습니다.");
                        CrashReporter.Log($"[MainWindow.UlCogButton_Click] dlss_info_hooks={(i == 1)} for '{card.GameName}'");
                        return true;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        cog.SetError($"DLSS Hooks를 저장할 수 없습니다: {ex.Message}");
                        CrashReporter.Log($"[MainWindow.UlCogButton_Click] dlss_info_hooks write failed — {ex.Message}");
                        return false;
                    }
                });

            // Greyed out until relimiter.ini exists (rows stay in place so the layout doesn't jump)
            if (!ulIniExists)
            {
                // AddCombo returns the ComboBox; its row is combo → label grid → row panel
                var dlssHooksRow = ((dlssHooksCombo.Parent as FrameworkElement)?.Parent as FrameworkElement) ?? dlssHooksCombo;
                foreach (var row in new[] { targetFpsRow, dlssHooksRow })
                {
                    row.Opacity = CogDialog.DisabledOpacity;
                    row.IsHitTestVisible = false;
                }
                targetFpsCombo.IsTabStop = false;
                dlssHooksCombo.IsTabStop = false;
            }

            // ── Files ─────────────────────────────────────────────────────────────
            cog.AddSection("파일");
            var openIniBtn = cog.MakeOpenFileButton("relimiter.ini 열기", ulIniFile);
            var deployBtn = cog.MakeButton("relimiter.ini 배포", CogButtonKind.Primary, () =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                AuxInstallService.CopyUlIni(card.InstallPath);
                openIniBtn.IsEnabled = ulIniFile != null && File.Exists(ulIniFile);
                cog.SetSuccess("relimiter.ini를 게임 폴더에 복사했습니다. 설정을 변경하려면 이 대화 상자를 닫았다가 다시 여세요.");
                CrashReporter.Log($"[MainWindow.UlCogButton_Click] Deployed relimiter.ini for '{card.GameName}'");
            }, "RHI의 relimiter.ini 템플릿을 게임 폴더에 복사하여 현재 파일을 교체합니다.");

            var openLogBtn = cog.MakeOpenFileButton("ReLimiter 로그 열기", logFile);

            // Copied as a file (not text) so Discord shows the log's real name
            var copyLogBtn = cog.MakeButton("ReLimiter 로그 복사", CogButtonKind.Secondary, async () =>
            {
                if (logFile == null || !File.Exists(logFile)) { cog.SetError("ReLimiter 로그를 찾을 수 없습니다."); return; }
                await CopyFileToClipboardAsFileAsync(logFile);
                cog.SetSuccess($"{Path.GetFileName(logFile)}을(를) 클립보드에 복사했습니다. Discord에 붙여넣어 첨부하세요.");
            }, "ReLimiter 로그를 파일 형태로 클립보드에 복사합니다.");
            copyLogBtn.IsEnabled = logFile != null;

            cog.AddButtons(deployBtn, openIniBtn, openLogBtn, copyLogBtn);

            // No default button: Enter in the key/number boxes must not close the dialog
            cog.Build().DefaultButton = ContentDialogButton.None;
            await cog.ShowAsync();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.UlCogButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

}
