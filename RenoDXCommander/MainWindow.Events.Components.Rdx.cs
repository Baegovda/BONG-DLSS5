// MainWindow.Events.Components.Rdx.cs — RenoDX cog dialog handler (RdxCogButton_Click).
// Built with the shared CogDialog layout. RTX HDR / Auto HDR toggles are in
// MainWindow.Events.Components.Rdx.HdrToggles.cs.

using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    internal async void RdxCogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
            if (string.IsNullOrEmpty(card.InstallPath)) return;

            var installPath = card.InstallPath;
            var iniPath = Path.Combine(installPath, "reshade.ini");
            var presetPath = Path.Combine(installPath, "RHI-RenoDX-Preset.txt");

            // Read file state on background thread before building dialog
            bool iniExists = false;
            bool presetExists = false;
            AuxInstallService.OrderedDict? renodxSection = null;
            string currentNits = "";

            await Task.Run(() =>
            {
                iniExists = File.Exists(iniPath);
                presetExists = File.Exists(presetPath);

                if (iniExists)
                {
                    try
                    {
                        var ini = AuxInstallService.ParseIni(File.ReadAllLines(iniPath));

                        // Extract current nits value
                        var presetWithNits = ini.FirstOrDefault(kv =>
                            kv.Key.StartsWith("renodx-preset", StringComparison.OrdinalIgnoreCase)
                            && kv.Value.ContainsKey("ToneMapPeakNits"));
                        if (presetWithNits.Value != null && presetWithNits.Value.TryGetValue("ToneMapPeakNits", out var nv))
                            currentNits = double.TryParse(nv, out var dv) ? ((int)dv).ToString() : nv;

                        // Extract renodx section
                        if (ini.TryGetValue("renodx", out var section))
                            renodxSection = section;
                    }
                    catch (Exception ex) { CrashReporter.Log($"[MainWindow.RdxCogButton_Click] Failed to read RenoDX ini, using fallback — {ex.Message}"); }
                }
            });

            var cog = new CogDialog("RenoDX", Content.XamlRoot);
            bool hasRenoDxMod = !card.IsRtxHdrEnabled && (card.Mod?.SnapshotUrl != null || card.Status == GameStatus.Installed || card.Status == GameStatus.UpdateAvailable);

            // ── Main settings (no header) ──────────────────────────────────────────
            // Order: UE-Extended, Max Nits, Engine.ini HDR, LUT Update Every Frame, Upgrade Path
            Action? addNits = hasRenoDxMod && iniExists
                ? () => AddRdxNitsRow(cog, iniPath, currentNits)
                : null;
            Action? addUpgradePath = null;
            if (iniExists && renodxSection != null && renodxSection.ContainsKey("Set_Path"))
            {
                int.TryParse(renodxSection["Set_Path"], out var setPathVal);
                addUpgradePath = () => cog.AddCombo("업그레이드 경로", RdxUpgradePathHelp,
                    new List<string> { "HDR / 꺼짐", "SDR / 켜짐" },
                    setPathVal >= 0 && setPathVal < 2 ? setPathVal : 0,
                    idx => WriteRenodxIniValue(cog, iniPath, "Set_Path", idx.ToString(), "업그레이드 경로"));
            }
            AddRdxMainSettings(cog, card, addNits, addUpgradePath);

            // ── Compatibility settings from [renodx] section ──────────────────────
            AddRdxCompatibilitySection(cog, iniPath, iniExists, renodxSection);

            // ── Preset Export/Import ──────────────────────────────────────────────
            if (hasRenoDxMod)
                AddRdxPresetsSection(cog, card, iniPath, presetPath, iniExists, presetExists);

            // ── RTX HDR / Auto HDR ────────────────────────────────────────────────
            await AddRdxHdrToggleSectionsAsync(cog, card);

            await cog.ShowAsync();
            _detailPanelBuilder?.UpdateDetailComponentRows(card);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.RdxCogButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    private const string RdxUpgradePathHelp =
        "HDR은 게임 자체 HDR 출력을 사용합니다. SDR은 게임의 SDR 출력을 HDR로 업그레이드합니다. reshade.ini에 Set_Path로 저장됩니다.";

    // ── Max Nits ─────────────────────────────────────────────────────────────────

    /// <summary>[TextBox][Auto] row that writes ToneMapPeakNits to every [renodx-preset*] section.</summary>
    private static void AddRdxNitsRow(CogDialog cog, string iniPath, string currentNits)
    {
        var nitsBox = CogDialog.MakeTextBox(currentNits, "nits");

        // Helper: write nits value to all preset sections
        void ApplyNitsValue(string nitsValue)
        {
            if (!int.TryParse(nitsValue, out var val) || val <= 0)
            {
                cog.SetError("올바른 숫자를 입력하세요.");
                return;
            }
            try
            {
                var freshIni = AuxInstallService.ParseIni(File.ReadAllLines(iniPath));
                int updated = 0;
                foreach (var section in freshIni)
                {
                    if (section.Key.StartsWith("renodx-preset", StringComparison.OrdinalIgnoreCase))
                    {
                        section.Value["ToneMapPeakNits"] = val.ToString();
                        updated++;
                    }
                }
                if (updated == 0)
                {
                    freshIni["renodx-preset1"] = new AuxInstallService.OrderedDict { ["ToneMapPeakNits"] = val.ToString() };
                    updated = 1;
                }
                AuxInstallService.WriteIni(iniPath, freshIni);
                nitsBox.Text = val.ToString();
                cog.SetSuccess($"프리셋 {updated}개에서 ToneMapPeakNits를 {val}(으)로 설정했습니다.");
                CrashReporter.Log($"[MainWindow.AddRdxNitsRow] ToneMapPeakNits={val} in {updated} preset(s) of '{iniPath}'");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CrashReporter.Log($"[MainWindow.AddRdxNitsRow] Failed to write nits — {ex.Message}");
                cog.SetError($"최대 니트를 저장할 수 없습니다: {ex.Message}");
            }
        }

        // Enter key in TextBox applies the value and deselects
        nitsBox.KeyDown += (s, ev) =>
        {
            if (ev.Key == Windows.System.VirtualKey.Enter)
            {
                ApplyNitsValue(nitsBox.Text);
                nitsBox.IsEnabled = false;
                nitsBox.IsEnabled = true;
                ev.Handled = true;
            }
        };

        var autoBtn = CogDialog.StyledButton("자동", CogButtonKind.Primary, compact: true);
        ToolTipService.SetToolTip(autoBtn, "모니터의 최대 밝기를 자동으로 읽어옵니다.");
        autoBtn.Click += async (s, ev) =>
        {
            try
            {
                var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
                    Windows.Devices.Display.DisplayMonitor.GetDeviceSelector());
                if (devices.Count == 0) { cog.SetError("디스플레이를 찾을 수 없습니다."); return; }

                float maxNitsFound = 0;
                foreach (var device in devices)
                {
                    try
                    {
                        var mon = await Windows.Devices.Display.DisplayMonitor.FromInterfaceIdAsync(device.Id);
                        if (mon.MaxLuminanceInNits > maxNitsFound)
                            maxNitsFound = mon.MaxLuminanceInNits;
                    }
                    catch (Exception ex) { CrashReporter.Log($"[MainWindow.RdxCogButton_Click] Failed to read monitor peak luminance — {ex.Message}"); }
                }
                var peakNits = (int)maxNitsFound;
                if (peakNits <= 0) { cog.SetError("최대 밝기를 읽을 수 없습니다."); return; }

                ApplyNitsValue(peakNits.ToString());
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[MainWindow.AddRdxNitsRow] Auto nits failed — {ex.Message}");
                cog.SetError(ex.Message);
            }
        };

        cog.AddRow("최대 니트", CogDialog.Inline(nitsBox, autoBtn),
            "RenoDX 톤 매퍼의 최대 밝기로, reshade.ini의 모든 RenoDX 프리셋에 기록됩니다. " +
            "값을 입력하고 Enter를 누르거나, 자동을 클릭해 모니터에서 읽어오세요.");
    }

    // ── Unreal Engine: UE-Extended, Engine.ini HDR, LUT ──────────────────────────

    /// <summary>
    /// Top block of the RenoDX cog, no header, in this order: UE-Extended, Max Nits,
    /// Engine.ini HDR, LUT Update Every Frame, Upgrade Path. Rows that don't apply are skipped.
    /// </summary>
    private void AddRdxMainSettings(CogDialog cog, GameCardViewModel card, Action? addNits, Action? addUpgradePath)
    {
        bool showUeToggle = card.UeExtendedToggleVisibility == Visibility.Visible;
        bool showEngineHdr = card.UseUeExtended && card.Status == GameStatus.Installed;
        bool showLut = card.EngineHint?.Contains("Unreal") == true && card.Status == GameStatus.Installed;

        bool hasCustomEngineIniFile = AuxInstallService.GlobalManifest?.EngineIniFiles?.ContainsKey(card.GameName) == true;
        const string customEngineIniHelp = "이 게임의 사용자 지정 Engine.ini 파일에서 관리하므로 여기서 변경할 수 없습니다.";

        if (showUeToggle)
        {
            cog.AddCombo("UE-Extended",
                "게임 전용 모드나 일반 Unreal 모드 대신 UE-Extended RenoDX 모드를 사용합니다.",
                RdxOffOn(), card.UseUeExtended ? 1 : 0, idx =>
            {
                bool enable = idx == 1;
                if (enable != card.UseUeExtended)
                    ViewModel.ToggleUeExtended(card);
            });
        }

        addNits?.Invoke();

        if (showEngineHdr)
        {
            if (hasCustomEngineIniFile)
            {
                AddRdxDisabledCombo(cog, "Engine.ini HDR", customEngineIniHelp);
            }
            else
            {
                bool hdrActive = card.InstalledRecord?.EngineIniHdr ?? true;
                cog.AddCombo("Engine.ini HDR",
                    "게임 내 HDR 옵션이 없는 게임을 위해 Engine.ini에 HDR 설정을 추가합니다. SDR에서는 끄세요.",
                    RdxOffOn(), hdrActive ? 1 : 0, idx =>
                {
                    if (idx == 1)
                    {
                        AuxInstallService.ApplyEngineIniHdrSettings(card.InstallPath, card.EngineIniProjectOverride, card.GameName, card.Source);
                        if (card.InstalledRecord != null) card.InstalledRecord.EngineIniHdr = true;
                        cog.SetSuccess("Engine.ini HDR 설정을 배포했습니다.");
                    }
                    else
                    {
                        AuxInstallService.RemoveEngineIniHdrSettings(card.InstallPath, card.EngineIniProjectOverride, card.GameName, card.Source);
                        if (card.InstalledRecord != null) card.InstalledRecord.EngineIniHdr = false;
                        cog.SetSuccess("Engine.ini HDR 설정을 제거했습니다.");
                    }
                    if (card.InstalledRecord != null)
                        App.Services.GetRequiredService<IModInstallService>().SaveRecordPublic(card.InstalledRecord);
                });
            }
        }

        if (showLut)
        {
            if (hasCustomEngineIniFile)
            {
                AddRdxDisabledCombo(cog, "매 프레임 LUT 업데이트", customEngineIniHelp);
            }
            else
            {
                bool lutActive = card.InstalledRecord?.EngineIniLut ?? true;
                cog.AddCombo("매 프레임 LUT 업데이트",
                    "Engine.ini에 r.LUT.UpdateEveryFrame=1을 기록하여 게임이 매 프레임 색상 LUT를 다시 계산하도록 합니다. 정확한 HDR 색상을 위한 설정입니다.",
                    RdxOffOn(), lutActive ? 1 : 0, idx =>
                {
                    if (idx == 1)
                    {
                        AuxInstallService.ApplyEngineIniLutSetting(card.InstallPath, card.EngineIniProjectOverride, card.GameName, card.Source);
                        if (card.InstalledRecord != null) card.InstalledRecord.EngineIniLut = true;
                        cog.SetSuccess("Engine.ini에서 매 프레임 LUT 업데이트를 켰습니다.");
                    }
                    else
                    {
                        AuxInstallService.RemoveEngineIniLutSetting(card.InstallPath, card.EngineIniProjectOverride, card.GameName, card.Source);
                        if (card.InstalledRecord != null) card.InstalledRecord.EngineIniLut = false;
                        cog.SetSuccess("Engine.ini에서 매 프레임 LUT 업데이트를 제거했습니다.");
                    }
                    if (card.InstalledRecord != null)
                        App.Services.GetRequiredService<IModInstallService>().SaveRecordPublic(card.InstalledRecord);
                });
            }
        }

        addUpgradePath?.Invoke();
    }

    /// <summary>Greyed-out Off/On row for settings managed elsewhere.</summary>
    private static void AddRdxDisabledCombo(CogDialog cog, string label, string help)
    {
        var combo = CogDialog.MakeCombo(RdxOffOn(), 0, isEnabled: false);
        var row = cog.AddRow(label, combo, help);
        row.Opacity = CogDialog.DisabledOpacity;
    }

    // ── Compatibility ([renodx] section of reshade.ini) ──────────────────────────

    private static void AddRdxCompatibilitySection(CogDialog cog, string iniPath, bool iniExists, AuxInstallService.OrderedDict? renodxSection)
    {
        if (!iniExists)
        {
            cog.AddSection("호환성");
            cog.AddNote("게임 폴더에서 reshade.ini를 찾을 수 없습니다.");
            return;
        }
        if (renodxSection == null)
        {
            cog.AddSection("호환성");
            cog.AddNote("설정을 생성하려면 RenoDX를 설치한 상태로 게임을 한 번 실행하세요.");
            return;
        }

        var upgradeKeys = renodxSection
            .Where(kv => (kv.Key.StartsWith("Upgrade_", StringComparison.OrdinalIgnoreCase)
                          && !kv.Key.Equals("Upgrade_UseSCRGB", StringComparison.OrdinalIgnoreCase)
                          && !kv.Key.Equals("Upgrade_CopyDestinations", StringComparison.OrdinalIgnoreCase))
                      || kv.Key.Equals("DumpLUTShaders", StringComparison.OrdinalIgnoreCase)
                      || kv.Key.Equals("SettingsMode",           StringComparison.OrdinalIgnoreCase)
                      || kv.Key.Equals("Proxy_Revert_State",     StringComparison.OrdinalIgnoreCase)
                      // Unity engine settings
                      || kv.Key.Equals("Use_Swapchain_Proxy",    StringComparison.OrdinalIgnoreCase)
                      || kv.Key.Equals("Swapchain_Encoding",     StringComparison.OrdinalIgnoreCase)
                      || kv.Key.Equals("Force_Pipeline_Cloning", StringComparison.OrdinalIgnoreCase)
                      || kv.Key.Equals("ForceBorderless",        StringComparison.OrdinalIgnoreCase)
                      || kv.Key.Equals("PreventFullscreen",      StringComparison.OrdinalIgnoreCase)
                      || kv.Key.Equals("Blit_Copy_Hack",         StringComparison.OrdinalIgnoreCase)
                      || kv.Key.Equals("Use_Resource_Cloning",   StringComparison.OrdinalIgnoreCase)
                      || kv.Key.Equals("Upgrade_CopyDestinations", StringComparison.OrdinalIgnoreCase)
                      || kv.Key.Equals("Upgrade_UseSCRGB",       StringComparison.OrdinalIgnoreCase)
                      || kv.Key.Equals("Tonemap_Offset",         StringComparison.OrdinalIgnoreCase)
                      || kv.Key.Equals("Scaling_Offset",         StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key.Equals("DumpLUTShaders", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (upgradeKeys.Count == 0) return;

        cog.AddSection("호환성");
        cog.AddNote("이 값들은 게임 reshade.ini의 [renodx] 섹션에서 읽고 저장됩니다. " +
                    "모드 안내에서 지시한 경우에만 변경하세요. 자세한 내용은 설정 위에 마우스를 올려 확인하세요.");

        // Compact two-per-row grid (as before the cog redesign): these are many small toggles
        var cells = new List<RdxCompatCell>();

        foreach (var kv in upgradeKeys)
        {
            bool isSetPath = kv.Key.Equals("Set_Path", StringComparison.OrdinalIgnoreCase);
            bool isDumpLut = kv.Key.Equals("DumpLUTShaders", StringComparison.OrdinalIgnoreCase);
            bool isSwapchainProxy = kv.Key.Equals("Use_Swapchain_Proxy", StringComparison.OrdinalIgnoreCase);
            bool isSwapchainEncoding = kv.Key.Equals("Swapchain_Encoding", StringComparison.OrdinalIgnoreCase);
            bool isUnityBool = kv.Key.Equals("Force_Pipeline_Cloning", StringComparison.OrdinalIgnoreCase)
                            || kv.Key.Equals("ForceBorderless",        StringComparison.OrdinalIgnoreCase)
                            || kv.Key.Equals("PreventFullscreen",      StringComparison.OrdinalIgnoreCase)
                            || kv.Key.Equals("Use_Resource_Cloning",   StringComparison.OrdinalIgnoreCase);
            bool isOffsetRange = kv.Key.Equals("Tonemap_Offset",       StringComparison.OrdinalIgnoreCase)
                              || kv.Key.Equals("Scaling_Offset",       StringComparison.OrdinalIgnoreCase);
            bool isBlitCopyHack  = kv.Key.Equals("Blit_Copy_Hack",             StringComparison.OrdinalIgnoreCase);
            bool isCopyDest      = kv.Key.Equals("Upgrade_CopyDestinations",   StringComparison.OrdinalIgnoreCase);
            bool isUseScrGb      = kv.Key.Equals("Upgrade_UseSCRGB",           StringComparison.OrdinalIgnoreCase);
            bool isSettingsMode  = kv.Key.Equals("SettingsMode",               StringComparison.OrdinalIgnoreCase);
            bool isSwapChainCompat = kv.Key.Equals("Upgrade_SwapChainCompatibility", StringComparison.OrdinalIgnoreCase);
            bool isProxyRevert   = kv.Key.Equals("Proxy_Revert_State",         StringComparison.OrdinalIgnoreCase);
            bool isBinaryToggle = isSetPath || isDumpLut || isUnityBool;
            bool isResourceUpgrade = !(isBinaryToggle || isSwapchainProxy || isSwapchainEncoding || isOffsetRange || isBlitCopyHack
                                       || isCopyDest || isUseScrGb || isSettingsMode || isSwapChainCompat || isProxyRevert);

            // Label text
            string labelText = isSetPath ? "업그레이드 경로"
                : isDumpLut          ? "LUT 셰이더 덤프"
                : isSwapchainProxy   ? "스왑체인 프록시"
                : isSwapchainEncoding? "스왑체인 인코딩"
                : isUseScrGb         ? "스왑체인 형식"
                : isCopyDest         ? "복사 대상"
                : isSettingsMode     ? "고급 설정"
                : isSwapChainCompat  ? "스왑체인 호환"
                : isProxyRevert      ? "프록시 상태 복원"
                : kv.Key.StartsWith("Upgrade_", StringComparison.OrdinalIgnoreCase) ? kv.Key.Substring(8)
                : kv.Key.Replace('_', ' ');

            var items = new List<string>();
            if (isSetPath) { items.Add("HDR / 꺼짐"); items.Add("SDR / 켜짐"); }
            else if (isDumpLut) { items.Add("꺼짐"); items.Add("켜짐"); }
            else if (isSwapchainProxy) { items.Add("꺼짐"); items.Add("켜짐"); items.Add("켜짐 (호환)"); }
            else if (isSwapchainEncoding) { items.Add("선형"); items.Add("감마"); }
            else if (isBlitCopyHack) { items.Add("꺼짐"); items.Add("자동"); items.Add("켜짐"); items.Add("스케일링만"); }
            else if (isCopyDest) { items.Add("꺼짐"); items.Add("켜짐"); items.Add("자동 업그레이드"); }
            else if (isUseScrGb) { items.Add("HDR10"); items.Add("scRGB"); }
            else if (isSettingsMode || isSwapChainCompat || isProxyRevert) { items.Add("꺼짐"); items.Add("켜짐"); }
            else if (isOffsetRange) { for (int v = 0; v <= 5; v++) items.Add(v.ToString()); }
            else if (isUnityBool) { items.Add("꺼짐"); items.Add("켜짐"); }
            else { items.Add("꺼짐"); items.Add("출력 크기"); items.Add("출력 비율"); items.Add("모든 크기"); }

            int.TryParse(kv.Value, out var currentVal);
            int selected = !isResourceUpgrade
                ? (currentVal >= 0 && currentVal < items.Count ? currentVal : 0)
                : (currentVal >= 0 && currentVal <= 3 ? currentVal : 0);

            // Specific explanation where the setting's purpose is clear; always name the ini key.
            string? specific = isSetPath ? "HDR은 게임 자체 HDR 출력을 사용합니다. SDR은 게임의 SDR 출력을 HDR로 업그레이드합니다."
                : isDumpLut         ? "게임의 LUT 셰이더를 파일로 저장합니다. 모드 개발용이므로 일반 플레이 시에는 꺼 두세요."
                : isUseScrGb        ? "HDR 출력 형식: HDR10 또는 scRGB."
                : isSwapchainEncoding ? "게임 출력을 선형 또는 감마 인코딩으로 처리할지 여부입니다."
                : kv.Key.Equals("ForceBorderless", StringComparison.OrdinalIgnoreCase) ? "게임을 테두리 없는 창으로 강제 실행합니다."
                : kv.Key.Equals("PreventFullscreen", StringComparison.OrdinalIgnoreCase) ? "게임이 전용 전체 화면으로 전환되지 않도록 합니다."
                : isResourceUpgrade ? $"{labelText} 텍스처를 HDR 형식으로 업그레이드합니다. 출력 크기와 출력 비율은 화면 크기나 화면비가 일치하는 텍스처만 업그레이드하며, 모든 크기는 전부 업그레이드합니다."
                : null;
            string help = specific != null ? $"{specific} {kv.Key}(으)로 저장됩니다." : $"reshade.ini에 {kv.Key}(으)로 저장됩니다.";

            var capturedKey = kv.Key;
            var capturedLabel = labelText;
            cells.Add(new RdxCompatCell(labelText, help, items, selected,
                idx => WriteRenodxIniValue(cog, iniPath, capturedKey, idx.ToString(), capturedLabel)));
        }

        // ── Manifest-driven extra settings ──────────────────────────────────
        var extraSettings = AuxInstallService.GlobalManifest?.RenodxExtraSettings;
        if (extraSettings?.Count > 0)
        {
            foreach (var setting in extraSettings)
            {
                var options = setting.Options?.Count > 0
                    ? setting.Options
                    : new List<RenodxExtraOption> { new() { Value = "0", Name = "꺼짐" }, new() { Value = "1", Name = "켜짐" } };

                string currentExtraVal = setting.Default;
                if (renodxSection.TryGetValue(setting.Key, out var existingVal))
                    currentExtraVal = existingVal;
                var selectedIdx = options.FindIndex(o => o.Value == currentExtraVal);

                var capturedSetting = setting;
                var capturedOptions = options;
                var label = setting.Label ?? setting.Key;
                cells.Add(new RdxCompatCell(label, $"reshade.ini에 {setting.Key}(으)로 저장됩니다.",
                    options.Select(o => o.Name).ToList(), selectedIdx >= 0 ? selectedIdx : 0,
                    idx =>
                    {
                        if (idx < 0 || idx >= capturedOptions.Count) return true;
                        return WriteRenodxIniValue(cog, iniPath, capturedSetting.Key, capturedOptions[idx].Value, label);
                    }));
            }
        }

        AddRdxCompactGrid(cog, cells);
    }

    private sealed record RdxCompatCell(string Label, string Help, List<string> Items, int Selected, Func<int, bool> OnChanged);

    /// <summary>
    /// Lays compatibility settings out two per row (label | dropdown | label | dropdown), with the
    /// explanation as a tooltip. Picks that fail to save revert, as in CogDialog.AddCombo.
    /// </summary>
    private static void AddRdxCompactGrid(CogDialog cog, List<RdxCompatCell> cells)
    {
        if (cells.Count == 0) return;
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        for (int r = 0; r < (cells.Count + 1) / 2; r++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            int row = i / 2, col = (i % 2) * 2;

            var label = new TextBlock
            {
                Text = cell.Label,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            };
            var combo = CogDialog.MakeCombo(cell.Items, cell.Selected);
            ToolTipService.SetToolTip(label, cell.Help);
            ToolTipService.SetToolTip(combo, cell.Help);

            int last = combo.SelectedIndex;
            bool reverting = false;
            combo.SelectionChanged += (_, _) =>
            {
                if (reverting || combo.SelectedIndex < 0 || combo.SelectedIndex == last) return;
                if (cell.OnChanged(combo.SelectedIndex)) { last = combo.SelectedIndex; return; }
                reverting = true;
                combo.SelectedIndex = last;
                reverting = false;
            };

            Grid.SetRow(label, row); Grid.SetColumn(label, col);
            Grid.SetRow(combo, row); Grid.SetColumn(combo, col + 1);
            grid.Children.Add(label);
            grid.Children.Add(combo);
        }
        cog.AddContent(grid);
    }

    /// <summary>
    /// Writes key=value into the [renodx] section of reshade.ini (re-read fresh each time).
    /// Returns false (so the dropdown reverts) if the write failed or the section is missing.
    /// </summary>
    private static bool WriteRenodxIniValue(CogDialog cog, string iniPath, string key, string value, string label)
    {
        try
        {
            var iniForWrite = AuxInstallService.ParseIni(File.ReadAllLines(iniPath));
            if (!iniForWrite.TryGetValue("renodx", out var section))
            {
                cog.SetError($"{label}을(를) 저장할 수 없습니다: reshade.ini에 [renodx] 섹션이 없습니다.");
                return false;
            }
            section[key] = value;
            AuxInstallService.WriteIni(iniPath, iniForWrite);
            cog.SetSuccess($"{label} 저장됨.");
            CrashReporter.Log($"[MainWindow.WriteRenodxIniValue] [renodx] {key}={value} in '{iniPath}'");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashReporter.Log($"[MainWindow.WriteRenodxIniValue] Failed to write {key} — {ex.Message}");
            cog.SetError($"{label}을(를) 저장할 수 없습니다: {ex.Message}");
            return false;
        }
    }

    // ── Presets export / import ──────────────────────────────────────────────────

    private static void AddRdxPresetsSection(CogDialog cog, GameCardViewModel card, string iniPath, string presetPath,
        bool iniExists, bool presetExists)
    {
        cog.AddSection("프리셋");
        cog.AddNote("내보내기는 reshade.ini의 모든 RenoDX 프리셋을 게임 폴더의 RHI-RenoDX-Preset.txt에 저장하고 공유할 수 있도록 파일을 클립보드에 복사합니다. " +
                    "가져오기는 reshade.ini의 프리셋을 해당 파일의 프리셋으로 교체합니다.");

        var exportBtn = cog.MakeButton("프리셋 내보내기", CogButtonKind.Primary, async () =>
        {
            try
            {
                var lines = File.ReadAllLines(iniPath);
                var presetLines = new List<string>();
                bool inPreset = false;
                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("[renodx-preset", StringComparison.OrdinalIgnoreCase))
                    {
                        inPreset = true;
                        if (presetLines.Count > 0) presetLines.Add("");
                        presetLines.Add(line);
                    }
                    else if (line.TrimStart().StartsWith('[') && inPreset)
                    {
                        inPreset = false;
                    }
                    else if (inPreset)
                    {
                        presetLines.Add(line);
                    }
                }

                if (presetLines.Count == 0)
                {
                    cog.SetError("[renodx-preset*] 섹션을 찾을 수 없습니다.");
                    return;
                }

                // Add header comment
                presetLines.Insert(0, $"; RenoDX Preset exported from: {card.GameName}");
                presetLines.Insert(1, "; To import: place this file in the game folder and click 'Import Presets' in RHI,");
                presetLines.Insert(2, "; or paste the [renodx-preset*] sections into reshade.ini manually.");
                presetLines.Insert(3, "");

                File.WriteAllLines(presetPath, presetLines);
                // Copy as file to clipboard (shows as RHI-RenoDX-Preset.txt in Discord)
                try
                {
                    var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(presetPath);
                    var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dp.SetStorageItems(new[] { storageFile });
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                    Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
                }
                catch (Exception ex) { CrashReporter.Log($"[MainWindow.RdxCogButton_Click] Failed to copy preset file to clipboard — {ex.Message}"); }
                cog.SetSuccess($"프리셋 {presetLines.Count(l => l.StartsWith("["))}개를 내보내고 파일을 클립보드에 복사했습니다.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CrashReporter.Log($"[MainWindow.AddRdxPresetsSection] Export failed — {ex.Message}");
                cog.SetError($"내보내기 실패: {ex.Message}");
            }
        }, "모든 RenoDX 프리셋을 파일로 저장하고 공유할 수 있도록 클립보드에 복사합니다.");
        exportBtn.IsEnabled = iniExists;

        var importBtn = cog.MakeButton("프리셋 가져오기", CogButtonKind.Primary, () =>
        {
            try
            {
                // Read preset file, skip comment lines (header)
                var presetLines = File.ReadAllLines(presetPath)
                    .Where(l => !l.TrimStart().StartsWith(';'))
                    .ToArray();
                var iniLines = File.ReadAllLines(iniPath).ToList();

                // Collect preset section names from the backup file
                var presetSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in presetLines)
                {
                    if (line.TrimStart().StartsWith("[renodx-preset", StringComparison.OrdinalIgnoreCase))
                        presetSections.Add(line.Trim());
                }

                // Remove existing preset sections from reshade.ini
                var filtered = new List<string>();
                bool skipping = false;
                foreach (var line in iniLines)
                {
                    if (line.TrimStart().StartsWith("[renodx-preset", StringComparison.OrdinalIgnoreCase))
                    {
                        skipping = true;
                        continue;
                    }
                    if (line.TrimStart().StartsWith('[') && skipping)
                        skipping = false;
                    if (!skipping)
                        filtered.Add(line);
                }

                // Append imported presets at the end
                filtered.Add("");
                filtered.AddRange(presetLines);

                File.WriteAllLines(iniPath, filtered);
                cog.SetSuccess($"프리셋 {presetSections.Count}개를 가져왔습니다.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CrashReporter.Log($"[MainWindow.AddRdxPresetsSection] Import failed — {ex.Message}");
                cog.SetError($"가져오기 실패: {ex.Message}");
            }
        }, presetExists
            ? "내보낸 백업 파일의 프리셋을 reshade.ini로 복원합니다."
            : "RHI-RenoDX-Preset.txt 파일이 없습니다. 먼저 내보내기를 하세요.");
        importBtn.IsEnabled = presetExists && iniExists;

        cog.AddButtons(exportBtn, importBtn);
    }
}
