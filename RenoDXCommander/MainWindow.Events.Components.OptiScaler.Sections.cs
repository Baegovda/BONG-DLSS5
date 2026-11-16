// MainWindow.Events.Components.OptiScaler.Sections.cs — OptiScaler cog dialog sections shown for the
// Nightly, DLSS NR and F5 variants: Frame Generation, Additional Settings, Engine.ini (Unreal only)
// and Neural Rendering (DLSS NR / F5 only). Every setting applies as soon as it changes.
// Each section builds its cells first (OsComboCell), then lays them out two per row (AddOsGrid).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    // ── Frame Generation ─────────────────────────────────────────────────────────

    private void BuildOsFrameGenSection(OsCogContext ctx)
    {
        var card = ctx.Card;
        var cog = ctx.Cog;
        var store = ctx.Store;

        cog.AddSection("프레임 생성");

        // FG Enabled — master on/off
        var fgEnabledItems = new[] { "Auto (false)", "True" };
        var currentFgEnabled = ReadOsIniSectionValue(card.InstallPath, "FrameGen", "Enabled", "");
        var fgEnabledCell = OsComboCell(cog, "FG 사용",
            "OptiScaler 프레임 생성을 켭니다. Auto는 꺼 둡니다.",
            fgEnabledItems, currentFgEnabled.Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "FrameGen", "Enabled", fgEnabledItems[i] == "True" ? "true" : "false");
            });

        // Streamline/DLSS Enabler (combined) + Streamline version
        var dlssStreamlineSvc = App.Services.GetRequiredService<IDlssStreamlineService>();
        var slVersions = dlssStreamlineSvc.StreamlineVersions;
        var persistedSlVersion = ViewModel.GetOsStreamlineVersion(card.GameName, store);
        string slVersionDefault;
        if (!string.IsNullOrEmpty(persistedSlVersion) && slVersions.Contains(persistedSlVersion))
            slVersionDefault = persistedSlVersion;
        else if (slVersions.Contains("2.12.0"))
            slVersionDefault = "2.12.0";
        else
            slVersionDefault = slVersions.Count > 0 ? slVersions[0] : "2.12.0";
        var slVersionItems = slVersions.Count > 0 ? slVersions.ToList() : new List<string> { slVersionDefault };

        bool combinedOn = ViewModel.GetOsDeployStreamline(card.GameName, store);
        ComboBox? slVersionCombo = null;
        var yesNo = new[] { "No", "Yes" };
        var combinedCell = OsComboCell(cog, "Streamline/DLSS Enabler",
            "Yes는 Streamline과 DLSS Enabler를 게임의 OptiScaler 폴더에 복사합니다. OptiScaler를 통한 DLSS 프레임 생성에 필요합니다. No는 제거합니다.",
            yesNo, combinedOn ? 1 : 0,
            i =>
            {
                bool on = yesNo[i] == "Yes";
                ViewModel.SetOsDeployStreamline(card.GameName, on, store);
                ViewModel.SetOsDeployDlssEnabler(card.GameName, on, store);
                if (slVersionCombo != null) slVersionCombo.IsEnabled = on;
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                try
                {
                    var osDir = Path.Combine(card.InstallPath, "OptiScaler");
                    if (on)
                    {
                        _optiScalerService.DeployStreamlineToGame(card.InstallPath);
                        _ = _dlssEnablerService.InstallAsync(osDir);
                        // Swap to the selected version immediately so the combo reflects reality
                        var selVer = slVersionCombo?.SelectedItem as string;
                        if (!string.IsNullOrEmpty(selVer))
                        {
                            var slDir = Path.Combine(card.InstallPath, "OptiScaler", "Streamline");
                            dlssStreamlineSvc.SwapStreamlineAsync(slDir, selVer).SafeFireAndForget("OsCog.SwapStreamline");
                        }
                    }
                    else
                    {
                        _optiScalerService.RemoveStreamlineFromGame(card.InstallPath);
                        _dlssEnablerService.Uninstall(osDir);
                    }
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[OsCog] Streamline/DLSS Enabler — {ex.Message}");
                    cog.SetError($"Streamline/DLSS Enabler를 업데이트하지 못했습니다: {ex.Message}");
                }
            });

        var slVersionCell = OsComboCell(cog, "Streamline 버전",
            "Streamline/DLSS Enabler가 Yes일 때 사용하는 Streamline 버전입니다. 게임에서 바로 교체됩니다.",
            slVersionItems, OsIndexOf(slVersionItems, slVersionDefault),
            i =>
            {
                var selectedVer = slVersionItems[i];
                ViewModel.SetOsStreamlineVersion(card.GameName, selectedVer, store);
                if (ViewModel.GetOsDeployStreamline(card.GameName, store) && !string.IsNullOrEmpty(card.InstallPath))
                {
                    var slFolder = Path.Combine(card.InstallPath, "OptiScaler", "Streamline");
                    dlssStreamlineSvc.SwapStreamlineAsync(slFolder, selectedVer).SafeFireAndForget("OsCog.SwapStreamline");
                }
            }, isEnabled: combinedOn);
        slVersionCombo = slVersionCell.Combo;

        // FG Nvngx Override is used with FG Output DLSSG or FG Input "DLSSG via Nvngx"
        ComboBox? fgNvngxCombo = null;
        void SetNvngxUsable(bool usable)
        {
            if (fgNvngxCombo == null) return;
            fgNvngxCombo.Opacity = usable ? 1.0 : CogDialog.DisabledOpacity;
            fgNvngxCombo.IsHitTestVisible = usable;
            fgNvngxCombo.IsEnabled = usable;
        }
        bool NvngxApplies() => OptiScalerService.UsesNvngxReplacement(
            ViewModel.GetOsFgInput(card.GameName, store), ViewModel.GetOsFgOutput(card.GameName, store));

        // FG Input
        var fgInputCurrent = OsIniToFgInput(ViewModel.GetOsFgInput(card.GameName, store));
        var fgInputItems = OsWithValue(new[] { "Auto (Default)", "OptiFG (Upscaler)", "DLSSG via Streamline", "DLSSG via Nvngx", "FSR 3.1 FG", "FSR 3.0 FG", "XeFG" }, fgInputCurrent);
        var fgInputCell = OsComboCell(cog, "FG 입력",
            "OptiScaler가 후킹하는 프레임 생성 입력 (FGInput). Auto는 OptiScaler가 결정합니다.",
            fgInputItems, OsIndexOf(fgInputItems, fgInputCurrent),
            i =>
            {
                var v = OsFgInputToIni(fgInputItems[i]);
                ViewModel.SetOsFgInput(card.GameName, v, store);
                if (!string.IsNullOrEmpty(card.InstallPath)) OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "FrameGen", "FGInput", v);
                SetNvngxUsable(NvngxApplies());
            });

        // FG Output

        var fgOutputCurrent = OsIniToFgOutput(ViewModel.GetOsFgOutput(card.GameName, store));
        var fgOutputItems = OsWithValue(new[] { "Auto (Default)", "FSR FG", "DLSSG", "XeFG" }, fgOutputCurrent);
        var fgOutputCell = OsComboCell(cog, "FG 출력",
            "OptiScaler가 실행하는 프레임 생성 (FGOutput). Auto는 OptiScaler가 결정합니다.",
            fgOutputItems, OsIndexOf(fgOutputItems, fgOutputCurrent),
            i =>
            {
                var sel = fgOutputItems[i];
                var v = OsFgOutputToIni(sel);
                ViewModel.SetOsFgOutput(card.GameName, v, store);
                if (!string.IsNullOrEmpty(card.InstallPath)) OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "FrameGen", "FGOutput", v);
                SetNvngxUsable(NvngxApplies());
            });

        // Built-in replacements, then custom providers found in OptiScaler's custom folder (e.g. ANR FG)
        var nvngxCurrent = OsIniToFgNvngx(ViewModel.GetOsFgNvngxReplacement(card.GameName, store));
        var nvngxBase = new[] { "None (Real DLSSG)", "Nukem's", "Enabler", "FSR 3/4 FG", "Combo" };
        var nvngxItems = OsWithValue(nvngxBase
            .Concat(OptiScalerService.FindCustomFgProviders(card.InstallPath)
                .Where(n => !nvngxBase.Contains(OsIniToFgNvngx(n))))
            .ToArray(), nvngxCurrent);
        var fgNvngxCell = OsComboCell(cog, "FG Nvngx 재정의",
            "FG 출력이 DLSSG이거나 FG 입력이 DLSSG via Nvngx일 때 사용: NVIDIA DLSSG를 대신할 항목입니다. Enabler는 Streamline/DLSS Enabler가 Yes여야 합니다. OptiScaler 사용자 지정 폴더의 사용자 지정 제공자(예: ANR FG)는 이름으로 표시됩니다.",
            nvngxItems, OsIndexOf(nvngxItems, nvngxCurrent, 0),
            i =>
            {
                var v = OsFgNvngxToIni(nvngxItems[i]);
                ViewModel.SetOsFgNvngxReplacement(card.GameName, v, store);
                if (!string.IsNullOrEmpty(card.InstallPath) && NvngxApplies())
                    OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "FrameGen", "FGNvngxReplacement", v);
            });
        fgNvngxCombo = fgNvngxCell.Combo;
        ctx.FgOutputCombo = fgOutputCell.Combo;
        ctx.FgNvngxCombo = fgNvngxCombo;
        SetNvngxUsable(NvngxApplies());

        // HUD Fix — [OptiFG] HUDFix
        var hudFixCurrent = ReadOsIniSectionValue(card.InstallPath, "OptiFG", "HUDFix", "auto");
        var hudFixItems = new[] { "Default", "On", "Off" };
        var hudFixSelected = hudFixCurrent.Equals("true", StringComparison.OrdinalIgnoreCase) ? "On"
                           : hudFixCurrent.Equals("false", StringComparison.OrdinalIgnoreCase) ? "Off"
                           : "Default";
        var hudFixCell = OsComboCell(cog, "HUD 수정",
            "프레임 생성을 위한 HUD 없는 리소스 추적 ([OptiFG]의 HUDFix). Default는 OptiScaler에 맡깁니다.",
            hudFixItems, OsIndexOf(hudFixItems, hudFixSelected),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                var sel = hudFixItems[i];
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "OptiFG", "HUDFix", sel == "On" ? "true" : sel == "Off" ? "false" : "auto");
            });

        // Force Reflex — [fakenvapi] ForceReflex
        var forceReflexItems = new[] { "Auto (0)", "Force Disable (1)", "Force Enable (2)" };
        var currentForceReflex = ReadOsIniSectionValue(card.InstallPath, "fakenvapi", "ForceReflex", "");
        var forceReflexCell = OsComboCell(cog, "Reflex 강제",
            "DLSS FG 사용 시 Reflex 상태 ([fakenvapi]의 ForceReflex). Auto는 게임 내 설정을 따릅니다.",
            forceReflexItems, currentForceReflex switch { "1" => 1, "2" => 2, _ => 0 },
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "fakenvapi", "ForceReflex", i switch { 1 => "1", 2 => "2", _ => "0" });
            });

        // Use Games Reflex Markers — [DLSSG] UseGamesReflexMarkers
        var markersItems = new[] { "True", "False" };
        var currentReflexMarkers = ReadOsIniSectionValue(card.InstallPath, "DLSSG", "UseGamesReflexMarkers", "");
        var markersCell = OsComboCell(cog, "게임 Reflex 마커 사용",
            "게임 자체 Reflex 마커로 프레임 생성 타이밍을 맞춥니다. 기본값은 True입니다.",
            markersItems, currentReflexMarkers.Equals("false", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DLSSG", "UseGamesReflexMarkers", markersItems[i] == "False" ? "false" : "true");
            });

        ctx.FgEnabledCombo = fgEnabledCell.Combo;
        ctx.CombinedCombo = combinedCell.Combo;
        ctx.FgInputCombo = fgInputCell.Combo;
        ctx.HudFixCombo = hudFixCell.Combo;
        ctx.ForceReflexCombo = forceReflexCell.Combo;
        ctx.ReflexMarkersCombo = markersCell.Combo;

        // FG Nvngx Override sits under FG Output, the setting it depends on.
        AddOsGrid(ctx,
            fgEnabledCell, hudFixCell,
            combinedCell, slVersionCell,
            fgInputCell, fgOutputCell,
            null, fgNvngxCell,
            forceReflexCell, markersCell);
    }

    // ── Additional Settings ──────────────────────────────────────────────────────

    private void BuildOsAdditionalSection(OsCogContext ctx)
    {
        var card = ctx.Card;
        var cog = ctx.Cog;

        cog.AddSection("추가 설정");

        // DLSS SR preset
        var srItems = OsSrPresetMap.Select(p => p.Label).ToArray();
        var srCurrent = ReadOsIniSectionValue(card.InstallPath, "DLSS", "RenderPresetForAll", "auto");
        var srCell = OsComboCell(cog, "DLSS SR 프리셋",
            "DLSS 슈퍼 해상도 렌더 프리셋. J~M이 권장되는 최신 프리셋입니다. Default는 DLSS에 맡깁니다.",
            srItems, Math.Max(0, Array.FindIndex(OsSrPresetMap, p => p.Value == srCurrent)),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                var val = OsSrPresetMap[i].Value;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DLSS", "RenderPresetOverride", "true");
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DLSS", "RenderPresetForAll", val == "auto" ? "0" : val);
            });

        // DLSS RR preset
        var rrItems = OsRrPresetMap.Select(p => p.Label).ToArray();
        var rrCurrent = ReadOsIniSectionValue(card.InstallPath, "DLSSD", "RenderPresetForAll", "auto");
        var rrCell = OsComboCell(cog, "DLSS RR 프리셋",
            "DLSS 레이 재구성 렌더 프리셋. Default는 DLSS에 맡깁니다.",
            rrItems, Math.Max(0, Array.FindIndex(OsRrPresetMap, p => p.Value == rrCurrent)),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                var val = OsRrPresetMap[i].Value;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DLSSD", "RenderPresetOverride", "true");
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DLSSD", "RenderPresetForAll", val == "auto" ? "0" : val);
            });

        // Render Scale — UpscaleRatioOverride
        var rsItems = OsRenderScaleMap.Select(p => p.Label).ToArray();
        var rsCurrentEnabled = ReadOsIniSectionValue(card.InstallPath, "UpscaleRatio", "UpscaleRatioOverrideEnabled", "auto");
        var rsCurrentValue   = ReadOsIniSectionValue(card.InstallPath, "UpscaleRatio", "UpscaleRatioOverrideValue", "auto");
        string rsSelected = "Off";
        if (rsCurrentEnabled.Equals("true", StringComparison.OrdinalIgnoreCase)
            && float.TryParse(rsCurrentValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rsVal))
        {
            var match = OsRenderScaleMap.Skip(1).FirstOrDefault(p => Math.Abs(p.Ratio - rsVal) < 0.01f);
            if (match.Label != null) rsSelected = match.Label;
        }
        var rsCell = OsComboCell(cog, "렌더 배율",
            "내부 렌더 해상도를 출력 해상도 대비 백분율로 재정의합니다. Off는 게임의 품질 모드를 그대로 사용합니다.",
            rsItems, OsIndexOf(rsItems, rsSelected),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                var entry = OsRenderScaleMap[i];
                if (entry.Label == "Off")
                {
                    OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "UpscaleRatio", "UpscaleRatioOverrideEnabled", "false");
                }
                else
                {
                    OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "UpscaleRatio", "UpscaleRatioOverrideEnabled", "true");
                    OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "UpscaleRatio", "UpscaleRatioOverrideValue", FormatOsFloat(entry.Ratio));
                }
            });

        // Disable Flip Metering — [NvApi] DisableFlipMetering
        var flipItems = new[] { "Default", "On" };
        var flipCurrent = ReadOsIniSectionValue(card.InstallPath, "NvApi", "DisableFlipMetering", "auto");
        var flipCell = OsComboCell(cog, "Flip Metering 끄기",
            "On은 DisableFlipMetering=true로 설정합니다. Nukem's FG와 fakenvapi 사용 시 프레임 타임 그래프가 두꺼워지는 문제를 해결합니다.",
            flipItems, flipCurrent == "true" ? 1 : 0,
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "NvApi", "DisableFlipMetering", flipItems[i] == "On" ? "true" : "false");
            });

        ctx.SrPresetCombo = srCell.Combo;
        ctx.RrPresetCombo = rrCell.Combo;
        ctx.RsCombo = rsCell.Combo;
        ctx.FlipCombo = flipCell.Combo;
        AddOsGrid(ctx,
            srCell, rrCell,
            rsCell, flipCell);
    }

    // ── Engine.ini (Unreal Engine only) ──────────────────────────────────────────

    private void BuildOsEngineIniSection(OsCogContext ctx)
    {
        var card = ctx.Card;
        var cog = ctx.Cog;
        var store = ctx.Store;

        cog.AddSection("Engine.ini");

        void ApplyOrRemove(bool apply, (string, string, string)[] keys, string what)
        {
            if (string.IsNullOrEmpty(card.InstallPath)) return;
            try
            {
                if (apply) AuxInstallService.ApplyEngineIniCustomKeys(card.InstallPath, keys, card.EngineIniProjectOverride, card.GameName, card.Source);
                else AuxInstallService.RemoveEngineIniCustomKeys(card.InstallPath, keys.Select(k => k.Item2), card.EngineIniProjectOverride, card.GameName, card.Source);
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[MainWindow.OsCogButton_Click] Failed to update {what} Engine.ini keys — {ex.Message}");
                cog.SetError($"Engine.ini를 업데이트하지 못했습니다: {ex.Message}");
            }
        }

        // Dilated Motion Vectors
        var dmvItems = new[] { "Default", "Off" };
        var dmvCell = OsComboCell(cog, "확장 모션 벡터",
            "Off는 Engine.ini에 r.NGX.DLSS.DilateMotionVectors=0과 r.Streamline.DilateMotionVectors=0을 추가합니다. Default는 제거합니다.",
            dmvItems, ViewModel.GetOsDilatedMotionVectorsOff(card.GameName, store) ? 1 : 0,
            i =>
            {
                bool off = dmvItems[i] == "Off";
                ViewModel.SetOsDilatedMotionVectorsOff(card.GameName, off, store);
                ApplyOrRemove(off, new (string, string, string)[]
                {
                    ("SystemSettings", "r.NGX.DLSS.DilateMotionVectors", "0"),
                    ("SystemSettings", "r.Streamline.DilateMotionVectors", "0"),
                }, "DilateMotionVectors");
            });

        // FSR Crash Fix
        var fsrItems = new[] { "None", "FSR2", "FSR3", "FSR3.1" };
        var fsrFixCell = OsComboCell(cog, "FSR 충돌 수정",
            "선택한 FSR 버전의 네이티브 DX12 키를 Engine.ini에 추가합니다 (FSR3.1은 r.FidelityFX.FSR3.UseRHI=0도 설정). None은 제거합니다.",
            fsrItems, OsIndexOf(fsrItems, ViewModel.GetOsFsrCrashFix(card.GameName, store)),
            i =>
            {
                var sel = fsrItems[i];
                var allK = new[] { "r.FidelityFX.FSR2.UseNativeDX12", "r.FidelityFX.FSR3.UseNativeDX12", "r.FidelityFX.FSR3.UseRHI" };
                if (!string.IsNullOrEmpty(card.InstallPath))
                {
                    try { AuxInstallService.RemoveEngineIniCustomKeys(card.InstallPath, allK, card.EngineIniProjectOverride, card.GameName, card.Source); }
                    catch (Exception ex)
                    {
                        CrashReporter.Log($"[MainWindow.OsCogButton_Click] Failed to remove FSR crash-fix Engine.ini keys — {ex.Message}");
                        cog.SetError($"Engine.ini를 업데이트하지 못했습니다: {ex.Message}");
                    }
                    if (sel != "None")
                    {
                        var k = sel switch
                        {
                            "FSR2" => new (string, string, string)[] { ("SystemSettings", "r.FidelityFX.FSR2.UseNativeDX12", "1") },
                            "FSR3" => new (string, string, string)[] { ("SystemSettings", "r.FidelityFX.FSR3.UseNativeDX12", "1") },
                            _      => new (string, string, string)[] { ("SystemSettings", "r.FidelityFX.FSR3.UseNativeDX12", "1"), ("SystemSettings", "r.FidelityFX.FSR3.UseRHI", "0") },
                        };
                        try { AuxInstallService.ApplyEngineIniCustomKeys(card.InstallPath, k, card.EngineIniProjectOverride, card.GameName, card.Source); }
                        catch (Exception ex)
                        {
                            CrashReporter.Log($"[MainWindow.OsCogButton_Click] Failed to apply FSR crash-fix Engine.ini keys — {ex.Message}");
                            cog.SetError($"Engine.ini를 업데이트하지 못했습니다: {ex.Message}");
                        }
                    }
                }
                ViewModel.SetOsFsrCrashFix(card.GameName, sel == "None" ? null : sel, store);
            });

        // FSR-FG Swapchain
        var onItems = new[] { "Default", "On" };
        var swapchainCell = OsComboCell(cog, "FSR-FG 스왑체인",
            "On은 Engine.ini에 r.FidelityFX.FI.OverrideSwapChainDX12=1을 추가합니다. Default는 제거합니다.",
            onItems, ViewModel.GetOsFsrFgSwapchain(card.GameName, store) ? 1 : 0,
            i =>
            {
                bool on = onItems[i] == "On";
                ViewModel.SetOsFsrFgSwapchain(card.GameName, on, store);
                ApplyOrRemove(on, new (string, string, string)[] { ("SystemSettings", "r.FidelityFX.FI.OverrideSwapChainDX12", "1") }, "FSR-FG swapchain");
            });

        // Upscaler Plugin
        var pluginCell = OsComboCell(cog, "업스케일러 플러그인",
            "On은 Engine.ini에 r.AntiAliasingMethod=4와 r.TemporalAA.Upscaler=1을 추가합니다. Default는 제거합니다.",
            onItems, ViewModel.GetOsUpscalerPlugin(card.GameName, store) ? 1 : 0,
            i =>
            {
                bool on = onItems[i] == "On";
                ViewModel.SetOsUpscalerPlugin(card.GameName, on, store);
                ApplyOrRemove(on, new (string, string, string)[]
                {
                    ("SystemSettings", "r.AntiAliasingMethod", "4"),
                    ("SystemSettings", "r.TemporalAA.Upscaler", "1"),
                }, "upscaler plugin");
            });

        // The two FSR fixes share a row.
        AddOsGrid(ctx,
            pluginCell, dmvCell,
            fsrFixCell, swapchainCell);
    }

    // ── Neural Rendering (DLSS NR and F5 variants) ───────────────────────────────

    private void BuildOsNeuralRenderingSection(OsCogContext ctx)
    {
        var card = ctx.Card;
        var cog = ctx.Cog;
        var store = ctx.Store;

        cog.AddSection("Neural Rendering");

        string ReadNrIni(string key) => ReadOsIniSectionValue(card.InstallPath, "DlssNr", key, "auto");
        var defaultOnOff = new[] { "Default", "On", "Off" };
        int DefaultOnOffIndex(string raw) =>
            raw.Equals("true", StringComparison.OrdinalIgnoreCase) ? 1
            : raw.Equals("false", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        string DefaultOnOffToIni(int i) => i == 1 ? "true" : i == 2 ? "false" : "auto";

        // NR Runtime
        var dlssStreamlineSvc = App.Services.GetRequiredService<IDlssStreamlineService>();
        var nrVersions = dlssStreamlineSvc.DlssnrVersions.ToList();
        var currentNrRuntime = ViewModel.GetOsNrRuntime(card.GameName, store);
        // Default to newest (first manifest entry) when no override is set
        if (string.IsNullOrEmpty(currentNrRuntime) && nrVersions.Count > 0)
            currentNrRuntime = nrVersions[0];
        if (!string.IsNullOrEmpty(currentNrRuntime) && !nrVersions.Contains(currentNrRuntime))
            nrVersions.Insert(0, currentNrRuntime);
        var nrRuntimeCell = OsComboCell(cog, "NR 런타임",
            "nvngx_dlssnr.dll 버전. 310.8.2는 NVIDIA 원본 (RTX 50), 310.8.SF-v2는 ShortFuse의 크로스 세대 빌드 (RTX 20/30/40)입니다. 게임 폴더에서 바로 교체됩니다.",
            nrVersions, OsIndexOf(nrVersions, currentNrRuntime),
            i =>
            {
                var selectedVer = nrVersions[i];
                if (string.IsNullOrEmpty(selectedVer)) return;
                ViewModel.SetOsNrRuntime(card.GameName, selectedVer, store);
                // Immediately swap the nvngx_dlssnr.dll in the game folder if installed
                if (card.IsOsInstalled && !string.IsNullOrEmpty(card.InstallPath))
                {
                    var gameNrPath = Path.Combine(card.InstallPath, "nvngx_dlssnr.dll");
                    var installPath = card.InstallPath;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var cachedDll = await dlssStreamlineSvc.EnsureSpecificDlssnrCachedAsync(selectedVer).ConfigureAwait(false);
                            if (cachedDll != null)
                            {
                                // Use SentinelBackup to preserve the existing sentinel state,
                                // then overwrite with the new version
                                AuxInstallService.SentinelBackup(gameNrPath);
                                File.Copy(cachedDll, gameNrPath, overwrite: true);
                                CrashReporter.Log($"[OsCog] Swapped nvngx_dlssnr.dll to {selectedVer} in '{installPath}'");
                            }
                            else
                            {
                                CrashReporter.Log($"[OsCog] nvngx_dlssnr.dll {selectedVer} not available — swap skipped");
                            }
                        }
                        catch (Exception ex) { CrashReporter.Log($"[OsCog] NR runtime swap failed — {ex.Message}"); }
                    });
                }
            });

        // NR Enabled
        var nrEnabledCell = OsComboCell(cog, "NR 사용",
            "DLSS Neural Rendering을 켜거나 끕니다. 게임 폴더에 nvngx_dlssnr.dll과 nvngx.dll_dlssnr.dll이 필요합니다.",
            defaultOnOff, DefaultOnOffIndex(ReadNrIni("Enabled")),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DlssNr", "Enabled", DefaultOnOffToIni(i));
            });

        // Run Before SR
        var nrRunBeforeSrCell = OsComboCell(cog, "SR 이전에 실행",
            "On은 업스케일링(슈퍼 해상도) 전에 NR을 실행합니다. Off는 이후에 실행합니다.",
            defaultOnOff, DefaultOnOffIndex(ReadNrIni("RunBeforeSR")),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DlssNr", "RunBeforeSR", DefaultOnOffToIni(i));
            });

        // Passes
        var passesItems = new[] { "Default", "1", "2", "3" };
        var nrPassesRaw = ReadNrIni("Passes");
        var nrPassesCell = OsComboCell(cog, "패스 수",
            "NR 모델 패스 횟수. 패스가 하나 늘 때마다 모델 시간이 약 두 배 들고 품질이 향상됩니다.",
            passesItems, OsIndexOf(passesItems, nrPassesRaw is "1" or "2" or "3" ? nrPassesRaw : "Default"),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                var sel = passesItems[i];
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DlssNr", "Passes", sel == "Default" ? "auto" : sel);
            });

        // Working Scale
        var scaleItems = OsNrScaleMap.Select(p => p.Label).ToArray();
        var nrScaleRaw = ReadNrIni("WorkingScale");
        var nrScaleCell = OsComboCell(cog, "작업 배율",
            "NR 모델이 작동하는 해상도 (프레임 크기 대비 비율). 낮을수록 빠르며, 1.0 초과는 슈퍼샘플링합니다.",
            scaleItems, Math.Max(0, Array.FindIndex(OsNrScaleMap, p => p.Value == nrScaleRaw)),
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DlssNr", "WorkingScale", OsNrScaleMap[i].Value);
            });

        // Apply to Finished Picture
        var finishedItems = new[] { "Default", "On" };
        var nrFinishedCell = OsComboCell(cog, "완성된 화면에 적용",
            "게임의 모든 조명과 효과가 적용된 완성 화면에 NR을 적용합니다. 녹색 노이즈 완화에 도움이 됩니다. 네이티브 DX12 전용.",
            finishedItems, ReadNrIni("FinishedPicture").Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            i =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                OptiScalerService.SetOptiScalerIniValue(card.InstallPath, "DlssNr", "FinishedPicture", finishedItems[i] == "On" ? "true" : "false");
            });

        ctx.NrRuntimeCombo = nrRuntimeCell.Combo;
        ctx.NrEnabledCombo = nrEnabledCell.Combo;
        ctx.NrRunBeforeSrCombo = nrRunBeforeSrCell.Combo;
        ctx.NrPassesCombo = nrPassesCell.Combo;
        ctx.NrWorkingScaleCombo = nrScaleCell.Combo;
        ctx.NrFinishedPicCombo = nrFinishedCell.Combo;
        AddOsGrid(ctx,
            nrEnabledCell, nrRuntimeCell,
            nrRunBeforeSrCell, nrPassesCell,
            nrScaleCell, nrFinishedCell);
    }
}
