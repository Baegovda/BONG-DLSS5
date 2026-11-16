// MainWindow.Events.Components.cs — Per-component cog button (⚙️) dialog handlers (RS, RDX, UL, DC, OS, DXVK).

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
    // ── Component Cog Button Handlers ────────────────────────────────────────────

    private async void RsCogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
            if (string.IsNullOrEmpty(card.InstallPath)) return;

            // Read all file state on background thread to avoid blocking UI
            var installPath = card.InstallPath;
            bool rsPresetExists = false;
            bool reshadeIniExists = false;
            bool reshadeLogExists = false;
            string currentHotkey = ViewModel.Settings.OverlayHotkey;
            string currentScreenshotHotkey = ViewModel.Settings.ScreenshotHotkey;
            
            await Task.Run(() =>
            {
                rsPresetExists = File.Exists(AuxInstallService.RsPresetIniPath);
                reshadeIniExists = File.Exists(Path.Combine(installPath, "reshade.ini"));
                reshadeLogExists = File.Exists(Path.Combine(installPath, "ReShade.log"));
                
                // Read current keys from reshade.ini (game folder)
                var iniPath = Path.Combine(installPath, "reshade.ini");
                if (reshadeIniExists)
                {
                    try
                    {
                        var ini = AuxInstallService.ParseIni(File.ReadAllLines(iniPath));
                        if (ini.TryGetValue("INPUT", out var inputSection))
                        {
                            if (inputSection.TryGetValue("KeyOverlay", out var ko) && !string.IsNullOrWhiteSpace(ko))
                                currentHotkey = ko;
                            if (inputSection.TryGetValue("KeyScreenshot", out var ks2) && !string.IsNullOrWhiteSpace(ks2))
                                currentScreenshotHotkey = ks2;
                        }
                    }
                    catch (Exception ex) { CrashReporter.Log($"[MainWindow.RsCogButton_Click] Failed to read ReShade ini hotkeys, using fallback — {ex.Message}"); }
                }
            });

            var reshadeIniPath = Path.Combine(installPath, "reshade.ini");
            var reshadeLogPath = Path.Combine(installPath, "ReShade.log");
            var cog = new CogDialog("ReShade", Content.XamlRoot);

            // ── Hotkeys ───────────────────────────────────────────────────────────
            // Capture box + Apply per key: a key press only fills the box, Apply writes it.
            cog.AddSection("단축키");
            var hotkeyString = currentHotkey;
            var screenshotHotkeyString = currentScreenshotHotkey;

            TextBox MakeHotkeyBox(Func<string> getValue, Action<string> setValue)
            {
                var box = CogDialog.MakeTextBox(HotkeyManager.FormatHotkeyDisplay(getValue()), "클릭한 후 키를 누르세요...");
                box.IsReadOnly = true;
                box.GotFocus += (s, ev) => box.Text = "Press a key...";
                box.KeyDown += (s, ev) =>
                {
                    var vk = (int)ev.Key;
                    if (vk == 0 || vk == 16 || vk == 17 || vk == 18) return;
                    bool shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                    bool ctrl  = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                    bool alt   = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                    setValue(HotkeyManager.BuildScreenshotHotkeyString(vk, shift, ctrl, alt));
                    box.Text = HotkeyManager.FormatHotkeyDisplay(getValue());
                    ev.Handled = true;
                };
                box.LostFocus += (s, ev) => { if (box.Text == "Press a key...") box.Text = HotkeyManager.FormatHotkeyDisplay(getValue()); };
                return box;
            }

            // Writes the key to every reshade*.ini in the game folder.
            async Task ApplyHotkeyAsync(string keyLabel, string hotkey, Action<string, string> applyToIni)
            {
                var gamePath = card.InstallPath;
                if (string.IsNullOrEmpty(gamePath)) return;
                var (count, error) = await Task.Run(() =>
                {
                    try
                    {
                        var iniFiles = Directory.EnumerateFiles(gamePath, "reshade*.ini")
                            .Where(f => Path.GetExtension(f).Equals(".ini", StringComparison.OrdinalIgnoreCase)
                                     && Path.GetFileNameWithoutExtension(f).StartsWith("reshade", StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        foreach (var file in iniFiles)
                            applyToIni(file, hotkey);
                        return (iniFiles.Count, (string?)null);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        return (0, ex.Message);
                    }
                });
                if (error != null)
                {
                    cog.SetError($"{keyLabel}을(를) 설정할 수 없습니다: {error}");
                    _crashReporter.Log($"[RsCogButton_Click] Failed to apply {keyLabel.ToLowerInvariant()} for '{card.GameName}' — {error}");
                    return;
                }
                if (count == 0)
                {
                    cog.SetStatus("게임 폴더에 reshade.ini가 없어 아무것도 변경되지 않았습니다.");
                    return;
                }
                cog.SetSuccess($"ini 파일 {count}개에서 {keyLabel}을(를) {HotkeyManager.FormatHotkeyDisplay(hotkey)}(으)로 설정했습니다.");
                _crashReporter.Log($"[RsCogButton_Click] Applied {keyLabel.ToLowerInvariant()} '{hotkey}' to {count} ini file(s) for '{card.GameName}'");
            }

            var hotkeyBox = MakeHotkeyBox(() => hotkeyString, v => hotkeyString = v);
            var applyKeyBtn = CogDialog.StyledButton("적용", CogButtonKind.Secondary, compact: true);
            applyKeyBtn.Click += async (s, ev) =>
            {
                try { await ApplyHotkeyAsync("오버레이 키", hotkeyString, AuxInstallService.ApplyOverlayHotkey); }
                catch (Exception ex)
                {
                    cog.SetError($"오버레이 키를 설정할 수 없습니다: {ex.Message}");
                    _crashReporter.Log($"[RsCogButton_Click] Overlay key apply failed — {ex}");
                }
            };
            cog.AddRow("오버레이 키", CogDialog.Inline(hotkeyBox, applyKeyBtn),
                "게임에서 ReShade 오버레이를 엽니다. 상자를 클릭하고 키를 누른 다음 적용을 누르세요. 게임 폴더의 모든 reshade*.ini에 저장됩니다.");

            var screenshotHotkeyBox = MakeHotkeyBox(() => screenshotHotkeyString, v => screenshotHotkeyString = v);
            var applyScreenshotKeyBtn = CogDialog.StyledButton("적용", CogButtonKind.Secondary, compact: true);
            applyScreenshotKeyBtn.Click += async (s, ev) =>
            {
                try { await ApplyHotkeyAsync("스크린샷 키", screenshotHotkeyString, AuxInstallService.ApplyScreenshotHotkey); }
                catch (Exception ex)
                {
                    cog.SetError($"스크린샷 키를 설정할 수 없습니다: {ex.Message}");
                    _crashReporter.Log($"[RsCogButton_Click] Screenshot key apply failed — {ex}");
                }
            };
            cog.AddRow("스크린샷 키", CogDialog.Inline(screenshotHotkeyBox, applyScreenshotKeyBtn),
                "ReShade 스크린샷을 찍습니다. 상자를 클릭하고 키를 누른 다음(없음은 Backspace) 적용을 누르세요. 게임 폴더의 모든 reshade*.ini에 저장됩니다.");

            // ── Load ReShade as ASI (not for Vulkan: ReShade is a global layer there) ──
            if (!card.RequiresVulkanInstall)
            {
                cog.AddSection("로드");
                var asiName = AuxInstallService.RsAsiName(card.Is32Bit);
                ComboBox? asiCombo = null;
                bool asiReverting = false;
                asiCombo = cog.AddCombo("ReShade를 ASI로 로드",
                    $"켜짐으로 설정하면 ASI 로더(추가 기능의 ASI Loader 등)가 로드할 수 있도록 ReShade 이름을 {asiName}(으)로 바꿉니다. "
                    + "BONG DLSS5는 로더를 대신 설치하지 않습니다. 설치 및 업데이트 시 이름이 유지되며, 켜져 있는 동안 게임 재정의의 ReShade DLL 이름은 비활성화됩니다.",
                    new[] { "꺼짐", "켜짐" },
                    ViewModel.GetRsAsiMode(card.GameName, card.Source ?? "") ? 1 : 0,
                    i =>
                    {
                        if (asiReverting) return true;
                        bool on = i == 1;
                        _ = ApplyAsiModeAsync(on);
                        return true;

                        async Task ApplyAsiModeAsync(bool turnOn)
                        {
                            try
                            {
                                var error = await ViewModel.SetRsAsiModeAsync(card, turnOn);
                                if (error != null)
                                {
                                    cog.SetError(error);
                                    asiReverting = true;
                                    if (asiCombo != null) asiCombo.SelectedIndex = turnOn ? 0 : 1;
                                    asiReverting = false;
                                    return;
                                }
                                bool installed = card.RsRecord != null;
                                cog.SetSuccess(turnOn
                                    ? (installed ? $"ReShade가 이제 {asiName}입니다. 이 게임에 ASI 로더가 설치되어 있는지 확인하세요." : $"ReShade가 {asiName}(으)로 설치됩니다.")
                                    : (installed ? $"ReShade가 {card.RsInstalledFile}(으)로 되돌아갔습니다." : "ReShade가 일반 DLL 이름으로 설치됩니다."));
                                card.NotifyAll();
                                _detailPanelBuilder?.UpdateDetailComponentRows(card);
                                _detailPanelBuilder?.BuildOverridesPanel(card); // greys/ungreys the ReShade DLL name dropdown
                            }
                            catch (Exception ex)
                            {
                                _crashReporter.Log($"[RsCogButton_Click] ASI mode change failed — {ex}");
                                cog.SetError($"ReShade를 ASI로 로드 설정을 변경할 수 없습니다: {ex.Message}");
                            }
                        }
                    });
            }

            // ── Keep ReShade.ini Updated ──────────────────────────────────────────
            cog.AddSection("ReShade.ini");
            var capturedKeepGameName = card.GameName;
            var capturedKeepSource = card.Source ?? "";
            cog.AddCombo("ReShade.ini 최신 상태 유지",
                "아니요로 설정하면 ReShade 설치나 업데이트 시, 또는 모든 게임에 기록하는 설정 버튼을 사용할 때 BONG DLSS5가 이 게임의 reshade.ini를 변경하지 않습니다.",
                new[] { "예", "아니요" },
                ViewModel.GetKeepRsIniUpdated(capturedKeepGameName, capturedKeepSource) ? 0 : 1,
                i =>
                {
                    ViewModel.SetKeepRsIniUpdated(capturedKeepGameName, i == 0, capturedKeepSource);
                    cog.SetSuccess(i == 0
                        ? "BONG DLSS5가 이 게임의 reshade.ini를 최신 상태로 유지합니다."
                        : "BONG DLSS5가 이 게임의 reshade.ini를 변경하지 않습니다.");
                });

            // ── Files ─────────────────────────────────────────────────────────────
            cog.AddSection("파일");
            var openIniBtn = cog.MakeOpenFileButton("ReShade.ini 열기", reshadeIniPath);

            var deployIniBtn = cog.MakeButton("ReShade.ini 배포", CogButtonKind.Primary, () =>
            {
                var screenshotPath = BuildScreenshotSavePath(card.GameName);
                var overlayHotkey = ViewModel.Settings.OverlayHotkey;
                var screenshotHotkey = ViewModel.Settings.ScreenshotHotkey;
                if (card.RequiresVulkanInstall)
                {
                    AuxInstallService.MergeRsVulkanIni(card.InstallPath, card.GameName, screenshotPath, overlayHotkey, screenshotHotkey);
                    VulkanFootprintService.Create(card.InstallPath);
                    ViewModel.DeployShadersForCard(card.GameName);
                }
                else
                    AuxInstallService.MergeRsIni(card.InstallPath, screenshotPath, overlayHotkey, screenshotHotkey);

                if (card.UseUeExtended && card.Status == GameStatus.Installed)
                    AuxInstallService.ApplyRenoDxNativeHdrSettings(card.InstallPath);

                // Force-apply manifest [renodx] INI overrides on redeploy
                if (AuxInstallService.GlobalManifest?.RenodxIniOverrides != null
                    && AuxInstallService.GlobalManifest.RenodxIniOverrides.TryGetValue(card.GameName, out var cogIniOvr))
                    AuxInstallService.ApplyRenodxIniOverrides(card.InstallPath, cogIniOvr, forceOverwrite: true);

                openIniBtn.IsEnabled = File.Exists(reshadeIniPath);
                cog.SetSuccess("ReShade.ini를 배포했습니다.");
                _crashReporter.Log($"[RsCogButton_Click] Deployed ReShade.ini for '{card.GameName}'");
            }, "BONG DLSS5의 ReShade.ini 템플릿을 이 게임의 reshade.ini에 병합합니다. 템플릿 값이 기존 값을 대체하며 다른 설정은 유지됩니다.");

            var deployPresetBtn = cog.MakeButton("ReShadePreset.ini 배포", CogButtonKind.Primary, () =>
            {
                AuxInstallService.CopyRsPresetIniIfPresent(card.InstallPath);
                cog.SetSuccess("ReShadePreset.ini를 배포했습니다.");
                _crashReporter.Log($"[RsCogButton_Click] Deployed ReShadePreset.ini for '{card.GameName}'");
            }, rsPresetExists ? "BONG DLSS5 구성 폴더의 ReShadePreset.ini를 게임 폴더에 복사합니다." : "BONG DLSS5 구성 폴더에 ReShadePreset.ini가 없습니다");
            deployPresetBtn.IsEnabled = rsPresetExists;

            var openLogBtn = cog.MakeOpenFileButton("ReShade.log 열기", reshadeLogPath);

            // Copied as a file (not text) so Discord shows "ReShade.log"
            var copyLogBtn = cog.MakeButton("ReShade.log 복사", CogButtonKind.Secondary, async () =>
            {
                if (!File.Exists(reshadeLogPath)) { cog.SetError("ReShade.log를 찾을 수 없습니다."); return; }
                await CopyFileToClipboardAsFileAsync(reshadeLogPath);
                cog.SetSuccess("ReShade.log를 클립보드에 복사했습니다. Discord에 붙여 넣어 첨부하세요.");
            }, "ReShade.log를 파일로 클립보드에 복사합니다.");
            copyLogBtn.IsEnabled = reshadeLogExists;

            cog.AddButtons(deployIniBtn, deployPresetBtn, openIniBtn, openLogBtn, copyLogBtn);

            // No default button: Enter in the key/number boxes must not close the dialog
            cog.Build().DefaultButton = ContentDialogButton.None;
            await cog.ShowAsync();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.RsCogButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async void RefCogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
            if (string.IsNullOrEmpty(card.InstallPath)) return;

            var installPath = card.InstallPath;
            var storageDir = Path.Combine(installPath, "_storage_");
            bool storageExists = Directory.Exists(storageDir);

            var cog = new CogDialog("RE Framework", Content.XamlRoot);
            cog.AddSection("파일");
            cog.AddIntro("게임 폴더의 _storage_ 폴더에는 RE Framework와 스크립트가 저장한 데이터가 있습니다. 삭제하면 해당 데이터가 지워집니다.");
            var foundNote = cog.AddNote(storageExists ? $"찾음: {storageDir}" : "게임 폴더에 _storage_ 폴더가 없습니다.");

            const string deleteText = "_storage_ 폴더 삭제";
            Button? deleteStorageBtn = null;
            deleteStorageBtn = cog.MakeButton(deleteText, CogButtonKind.Danger, async () =>
            {
                var btn = deleteStorageBtn!;
                btn.IsEnabled = false;
                btn.Content = "삭제 중...";
                try
                {
                    await Task.Run(() => Directory.Delete(storageDir, recursive: true));
                    btn.Content = deleteText;
                    btn.Opacity = CogDialog.DisabledOpacity;
                    foundNote.Text = "게임 폴더에 _storage_ 폴더가 없습니다.";
                    cog.SetSuccess("_storage_ 폴더를 삭제했습니다.");
                    CrashReporter.Log($"[RefCogButton_Click] Deleted _storage_ folder: '{storageDir}'");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    btn.IsEnabled = true;
                    btn.Content = deleteText;
                    cog.SetError($"_storage_ 폴더를 삭제할 수 없습니다: {ex.Message}");
                    CrashReporter.Log($"[RefCogButton_Click] Failed to delete _storage_ folder: {ex.Message}");
                }
            });
            deleteStorageBtn.IsEnabled = storageExists;
            deleteStorageBtn.Opacity = storageExists ? 1.0 : CogDialog.DisabledOpacity;
            cog.AddButtons(deleteStorageBtn);

            await cog.ShowAsync();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.RefCogButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Copies a file (e.g. a log) to the clipboard as a file rather than text, so pasting it into
    /// Discord attaches it under its real name. Used by the ReShade and ReLimiter cog dialogs.
    /// </summary>
    private static async Task CopyFileToClipboardAsFileAsync(string path)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "RHI_clipboard");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, Path.GetFileName(path));
        File.Copy(path, tempFile, overwrite: true);

        var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(tempFile);
        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.SetStorageItems(new[] { storageFile });
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
    }

    private void SupportGuide_Click(object sender, RoutedEventArgs e)
    {
        _ = Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/RankFTW/RHI/blob/main/docs/DETAILED_GUIDE.md"));
    }

    private void SupportKofi_Click(object sender, RoutedEventArgs e)
    {
        _ = Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://ko-fi.com/rankftw"));
    }

}
