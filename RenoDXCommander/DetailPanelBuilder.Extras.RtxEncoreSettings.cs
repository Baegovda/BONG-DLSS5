// DetailPanelBuilder.Extras.RtxEncoreSettings.cs -- RTX Encore ⚙: settings worth choosing before launch
// (frame generation, XeFG, HUD/UI, upscaling, Smooth Motion, Neural Rendering core options, compatibility),
// the Neural Rendering DLL and the Intel XeSS files status. Settings are edited in place in
// rtx-encore.jsonc (RtxEncoreConfig), beside the mod. The overlay, NR styles/per-pass tuning, shortcuts
// and diagnostics stay in RTX Encore's in-game menu (Insert).

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private static readonly string[] RtxOffOn = { "꺼짐", "켜짐" };
    private static readonly string[] RtxOffOnValues = { "false", "true" };

    /// <summary>⚙ dialog for RTX Encore. Every change is saved as soon as it's made.</summary>
    private async Task ShowRtxEncoreSettingsAsync(GameCardViewModel card, RtxEncoreService svc,
        string installPath, bool isInstalled, bool nrDeployed)
    {
        var gameName = card.GameName;
        var store    = card.Source ?? "";
        var installedAs = _window.ViewModel.GetRtxEncoreInstalledAs(gameName, store);
        string? modDir  = isInstalled && !string.IsNullOrEmpty(installedAs) ? svc.ModDir(installPath, installedAs) : null;
        string? cfgPath = modDir != null ? Path.Combine(modDir, RtxEncoreConfig.FileName) : null;
        bool canEdit = cfgPath != null;

        var cog = new CogDialog("RTX Encore", _window.Content.XamlRoot);
        cog.AddIntro("플레이 전에 설정하세요. 대부분 게임을 다시 시작해야 적용됩니다(도구 설명에 표시됨). " +
                     "오버레이, Neural Rendering 스타일, 단축키, 진단은 RTX Encore의 게임 내 메뉴(Insert)에 있습니다.");
        if (!canEdit)
            cog.AddNote("이 설정을 변경하려면 RTX Encore를 설치하세요.");
        else if (!File.Exists(cfgPath))
            cog.AddNote("이 게임에는 아직 rtx-encore.jsonc가 없습니다. 설정을 변경하면 해당 설정만 담긴 파일이 생성되고, " +
                        "나머지는 게임을 처음 시작할 때 RTX Encore가 기본값으로 추가합니다.");
        if (card.IsRunning)
            cog.AddWarning("게임이 실행 중입니다. 변경 사항은 다음 실행 시 적용되며, 게임 내 RTX Encore 메뉴가 열려 있으면 덮어쓸 수 있습니다.");

        Button? openCfgBtn = null;

        // One dropdown bound to one setting in rtx-encore.jsonc. values are the file's values ("true", "4", "xefg").
        CogDialog.CompactCell Choice(string label, string help, string section, string key,
            string[] displays, string[] values, string defaultValue, Action<int>? after = null)
        {
            var current = canEdit ? RtxEncoreConfig.Get(cfgPath!, section, key) : null;
            int idx = Array.FindIndex(values, v => string.Equals(v, current ?? defaultValue, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = Array.FindIndex(values, v => string.Equals(v, defaultValue, StringComparison.OrdinalIgnoreCase));
            return cog.CompactCombo(label, help, displays, idx, i =>
            {
                if (!canEdit) return false;
                var v = values[i];
                bool raw = v is "true" or "false" || int.TryParse(v, out _);
                RtxEncoreConfig.Set(cfgPath!, section, key, raw ? v : "\"" + v + "\"");
                CrashReporter.Log($"[RtxEncoreCog] '{gameName}': {section}.{key} = {v}");
                if (openCfgBtn != null) openCfgBtn.IsEnabled = true;
                cog.SetSuccess("저장했습니다. 게임이 실행 중이면 다시 시작하세요.");
                after?.Invoke(i);
                return true;
            }, isEnabled: canEdit);
        }

        void MarkSaved() { if (openCfgBtn != null) openCfgBtn.IsEnabled = true; }

        // A number box bound to one setting in rtx-encore.jsonc
        CogDialog.CompactCell Number(string label, string help, string section, string key, int min, int max, int defaultValue) =>
            new(label, help, BuildRtxEncoreNumberBox(cog, cfgPath, gameName, section, key, label, min, max, defaultValue, MarkSaved));

        // ── Frame generation ────────────────────────────────────────────────────
        cog.AddSection("프레임 생성");
        var xefgControls = new List<Control>();
        void SetXefgEnabled(bool on) { foreach (var c in xefgControls) c.IsEnabled = canEdit && on; }
        ComboBox? multiplierCombo = null;
        var dynamicControls = new List<Control>();

        var engineCell = Choice("엔진",
            "게임: 게임 자체 프레임 생성을 유지합니다. XeFG: 게임의 DLSS 프레임 생성을 Intel XeSS 프레임 생성으로 대체합니다. " +
            "DLSS-FG: 게임의 Intel 프레임 생성을 DLSS 프레임 생성으로 대체하며 고정 모드를 사용합니다(게임 결정은 2x로 실행, 동적은 사용 불가). " +
            "어느 것도 프레임 생성이 없는 게임에 프레임 생성을 추가하지는 않습니다. XeFG를 Neural Rendering 또는 Smooth Motion과 함께 쓰지 마세요. 적용하려면 게임을 다시 시작하세요.",
            "renderingEngines", "frameGenerationEngine",
            new[] { "게임", "DLSS-FG", "XeFG" }, new[] { "game", "dlss-fg", "xefg" }, "game",
            i => SetXefgEnabled(i == 2));
        var modeCell = Choice("모드",
            "게임 결정: 게임(또는 NVIDIA Profile Inspector)이 배수를 정합니다. 고정: 옆에 지정한 배수를 사용합니다. " +
            "동적: 목표 프레임 속도(기본값은 모니터 주사율)에 맞춰 프레임 생성이 조절됩니다.",
            "frameGeneration", "mode",
            new[] { "게임 결정", "고정", "동적" }, new[] { "game", "fixed", "dynamic" }, "game",
            i =>
            {
                if (multiplierCombo != null) multiplierCombo.IsEnabled = canEdit && i == 1;
                foreach (var c in dynamicControls) c.IsEnabled = canEdit && i == 2;
            });
        var multiplierCell = Choice("배수",
            "고정 모드에서 사용됩니다. 5x와 6x는 실험적입니다. 8 GB 그래픽 카드에서는 2x 또는 3x로 시작하는 것이 안전합니다.",
            "frameGeneration", "multiplier",
            new[] { "2x", "3x", "4x", "5x", "6x" }, new[] { "2", "3", "4", "5", "6" }, "4");
        multiplierCombo = (ComboBox)multiplierCell.Control;
        multiplierCombo.IsEnabled = canEdit && ((ComboBox)modeCell.Control).SelectedIndex == 1;
        var presetCell = Choice("DLSS FG 프리셋",
            "게임: 재정의 안 함. A와 B는 일반 프레임 생성을 사용하고, C는 더 빠른 Lite 모드(일부 화질 저하 있음)를 사용하며 DLSS FG 310.9.16이 필요합니다. " +
            "Lite는 프레임 보호, 보호 튜닝, HUD/UI 옵션을 사용하지 않습니다. B는 게임의 HUD 없는 입력이 필요합니다. 적용하려면 게임을 다시 시작하세요.",
            "dlssPresets", "dlssFgPreset",
            new[] { "게임", "A", "B", "C" }, new[] { "game", "a", "b", "c" }, "game");
        var flipCell = Choice("플립 미터링 우회",
            "NVIDIA 플립 미터링 대신 Streamline이 생성된 프레임의 간격을 직접 조절합니다. 2x는 부드러운데 3x나 4x가 끊겨 보일 때 사용해 보세요. 적용하려면 게임을 다시 시작하세요.",
            "frameGeneration", "flipMeteringBypass", RtxOffOn, RtxOffOnValues, "false");
        var fpsCell = Number("프레임 제한 (FPS)",
            "1~1000 FPS, 0이면 RTX Encore 제한 없음. Reflex를 통해 적용되며, XeFG 사용 시에는 XeLL을 통해 적용됩니다(이 경우 다시 시작 필요). 동적 모드에서는 일시 중지됩니다. 생성된 프레임이 제한에 포함될 수 있습니다. Enter를 누르거나 다른 곳을 클릭하면 저장됩니다.",
            "nativeMenuFeatures", "reflexFrameLimit", 0, 1000, 0);
        var targetCell = Number("동적 목표 FPS",
            "동적 모드에서 프레임 생성이 맞춰 갈 프레임 속도입니다. 0이면 게임이 표시되는 모니터의 주사율을 따릅니다. Enter를 누르거나 다른 곳을 클릭하면 저장됩니다.",
            "frameGeneration", "dynamicTargetFrameRate", 0, 1000, 0);
        var exp56Cell = Choice("동적 최대 6x",
            "동적 모드가 5x와 6x까지 올라갈 수 있게 합니다(VRAM이 넉넉해야 함). 꺼짐: 동적 모드는 4x 이하로 유지됩니다.",
            "frameGeneration", "dynamicExperimental56", RtxOffOn, RtxOffOnValues, "false");
        dynamicControls.AddRange(new[] { targetCell.Control, exp56Cell.Control }.OfType<Control>());
        bool isDynamic = ((ComboBox)modeCell.Control).SelectedIndex == 2;
        foreach (var c in dynamicControls) c.IsEnabled = canEdit && isDynamic;
        cog.AddCompactGrid(engineCell, modeCell, multiplierCell, presetCell, targetCell, exp56Cell, flipCell, fpsCell);

        // ── XeFG ────────────────────────────────────────────────────────────────
        cog.AddSection("XeFG 엔진");
        var tensorCell = Choice("NVIDIA Tensor 코어 (실험적)",
            "XeFG를 NVIDIA Tensor 코어에서 실행합니다. DirectX 12, RTX 30/40/50, NVIDIA 드라이버 621.01 또는 621.12 핫픽스 필요. 게임에서는 RTX 4090에서만 테스트되었습니다. " +
            "사용할 수 없으면 일반 XeFG가 대신 실행되며, 실행 중 실패하면 게임을 다시 시작하세요.",
            "xefg", "xefgTensorCores", RtxOffOn, RtxOffOnValues, "false");
        var pacingCell = Choice("2x 초과 가변 페이싱",
            "2x 초과 배수에서 게임의 실제 프레임 타이밍으로 XeFG 간격을 조절합니다. 끄면 2x 초과 배수를 사용하지 못할 수 있습니다. 적용하려면 게임을 다시 시작하세요.",
            "xefg", "xefgMfg4VariablePacing", RtxOffOn, RtxOffOnValues, "true");
        var uiCell = Choice("HUD/UI 처리",
            "보간: HUD가 장면과 함께 생성됩니다. 게임 레이어: 게임이 제공하는 경우 HUD 없는 이미지와 UI 이미지를 따로 사용합니다. 적용하려면 게임을 다시 시작하세요.",
            "xefg", "xefgUiHandling", new[] { "보간", "게임 레이어" }, new[] { "interpolate", "game" }, "interpolate");
        var alphaCell = Choice("UI 알파",
            "게임 레이어용으로, 게임 UI 레이어가 투명도를 저장하는 방식입니다. 모르면 알 수 없음으로 두세요. 알 수 없음은 추측하지 않으며 UI 레이어를 사용하지 않습니다. 적용하려면 게임을 다시 시작하세요.",
            "xefg", "xefgUiAlpha", new[] { "알 수 없음", "Straight", "Premultiplied" }, new[] { "unknown", "straight", "premultiplied" }, "unknown");
        xefgControls.AddRange(new[] { tensorCell.Control, pacingCell.Control, uiCell.Control, alphaCell.Control }.OfType<Control>());
        SetXefgEnabled(((ComboBox)engineCell.Control).SelectedIndex == 2);
        cog.AddCompactGrid(tensorCell, pacingCell, uiCell, alphaCell);
        cog.AddNote("엔진이 XeFG일 때만 적용됩니다.");

        // ── Image quality ───────────────────────────────────────────────────────
        cog.AddSection("생성 프레임 품질");
        cog.AddCompactGrid(
            Choice("고배수 품질",
                "3x~6x에서 생성 프레임을 깨끗하게 유지합니다. 켜 두세요. 적용하려면 게임을 다시 시작하세요.",
                "imageQuality", "highMultiplierQuality", RtxOffOn, RtxOffOnValues, "true"),
            Choice("프레임 보호",
                "얇은 지오메트리(울타리, 전선, 나뭇잎)의 찢어짐과 움직이는 그림자의 고스팅으로부터 생성 프레임을 보호합니다. Lite 프리셋(C)에서는 효과가 없습니다. 적용하려면 게임을 다시 시작하세요.",
                "imageQuality", "frameProtection", RtxOffOn, RtxOffOnValues, "true"),
            Choice("빠른 프레임 생성",
                "같은 이미지로 생성 프레임당 GPU 비용을 줄입니다. 적용하려면 게임을 다시 시작하세요.",
                "imageQuality", "fastFrameGeneration", RtxOffOn, RtxOffOnValues, "true"),
            Choice("보호 튜닝",
                "Refined(기본값) 또는 Classic. Lite 프리셋(C)에서는 효과가 없습니다. 적용하려면 게임을 다시 시작하세요.",
                "imageQuality", "protectionTuning", new[] { "Refined", "Classic" }, new[] { "refined", "classic" }, "refined"));

        // ── HUD and UI ──────────────────────────────────────────────────────────
        cog.AddSection("HUD 및 UI (DLSS FG)");
        cog.AddCompactGrid(
            Choice("자동 UI 재합성",
                "게임이 HUD 없는 이미지를 제공하면 게임이 요청하지 않아도 DLSS FG의 UI 재합성을 켭니다. 꺼짐은 게임 설정을 따릅니다. " +
                "No Man's Sky에서는 끄세요(생성 프레임 중복).",
                "hudUi", "autoUiRecomposition", RtxOffOn, RtxOffOnValues, "true"),
            Choice("UI 보조",
                "DirectX 12: 게임이 제공하지 않을 때 HUD 없는 장면을 캡처하고 UI 레이어를 직접 만듭니다.",
                "hudUi", "uiAssist", RtxOffOn, RtxOffOnValues, "true"),
            Choice("UI 재합성 강제",
                "게임이 요청하지 않아도 DLSS FG가 HUD를 따로 재합성하도록 합니다.",
                "hudUi", "forceUiRecomposition", RtxOffOn, RtxOffOnValues, "false"));

        // ── Upscaling ───────────────────────────────────────────────────────────
        cog.AddSection("업스케일링");
        var customScaleCell = Number("사용자 지정 렌더 배율 (%)",
            "DLSS 렌더 배율이 사용자 지정일 때의 렌더 배율(50~100%). Enter를 누르거나 다른 곳을 클릭하면 저장됩니다.",
            "dlssSuperResolution", "dlssCustomScale", 50, 100, 67);
        var renderScaleCell = Choice("DLSS 렌더 배율",
            "DLSS 렌더 해상도를 강제합니다. 게임: 재정의 안 함. 게임이 이미 DLSS를 사용해야 합니다.",
            "dlssSuperResolution", "dlssRenderScale",
            new[] { "게임", "DLAA", "품질", "균형", "성능", "울트라 성능", "사용자 지정" },
            new[] { "game", "dlaa", "quality", "balanced", "performance", "ultra-performance", "custom" }, "game",
            i => ((Control)customScaleCell.Control).IsEnabled = canEdit && i == 6);
        ((Control)customScaleCell.Control).IsEnabled = canEdit && ((ComboBox)renderScaleCell.Control).SelectedIndex == 6;
        var xessTensorCell = Choice("Tensor 코어에서 XeSS (실험적)",
            "XeSS 슈퍼 해상도를 NVIDIA Tensor 코어에서 실행합니다(RTX 30/40/50, DirectX 12, 드라이버 621.01 또는 621.12, RTX 4090에서 테스트됨). " +
            "업스케일러가 XeSS일 때만 적용됩니다. 사용할 수 없으면 일반 XeSS가 대신 실행되며, 실행 중 실패하면 게임을 다시 시작하세요. 즉시 적용됩니다.",
            "xess", "xessTensorCores", RtxOffOn, RtxOffOnValues, "false");
        var upscalerCell = Choice("업스케일러",
            "게임: 게임의 DLSS 슈퍼 해상도를 유지합니다. XeSS: 호환되는 DLSS 슈퍼 해상도를 Intel XeSS로 대체합니다. " +
            "DirectX에서는 즉시 적용되며, Vulkan은 게임 시작 전에 XeSS를 설정해야 합니다.",
            "renderingEngines", "reconstructionEngine", new[] { "게임", "XeSS" }, new[] { "game", "xess" }, "game",
            i => ((Control)xessTensorCell.Control).IsEnabled = canEdit && i == 1);
        ((Control)xessTensorCell.Control).IsEnabled = canEdit && ((ComboBox)upscalerCell.Control).SelectedIndex == 1;
        cog.AddCompactGrid(
            upscalerCell,
            xessTensorCell,
            renderScaleCell,
            customScaleCell,
            Choice("빠른 슈퍼 해상도",
                "RTX 30 전용, DirectX 12, DLSS 310.9.x: 프리셋 L과 M용 더 빠른 DLSS 슈퍼 해상도. 레이 재구성이나 DLAA에서는 효과가 없습니다. 이미지는 NVIDIA와 매우 비슷하지만 동일하지는 않습니다. 적용하려면 게임을 다시 시작하세요.",
                "dlssSuperResolution", "fastSuperResolution", RtxOffOn, RtxOffOnValues, "false"),
            Choice("DLSS SR 프리셋",
                "게임: 재정의 안 함. 모든 품질 모드에 적용되며, 작동하는 프리셋은 게임의 DLSS DLL에 따라 다릅니다. A~F는 레거시입니다. 즉시 적용됩니다.",
                "dlssPresets", "dlssSrPreset",
                new[] { "게임", "기본값", "A", "B", "C", "D", "E", "F", "J", "K", "L", "M" },
                new[] { "game", "default", "a", "b", "c", "d", "e", "f", "j", "k", "l", "m" }, "game"),
            Choice("DLSS RR 프리셋",
                "레이 재구성 프리셋. 게임: 재정의 안 함. A~C는 레거시이며, F는 nvngx_dlssd 310.9.1 이상이 필요합니다. RR을 켜지는 않습니다. 즉시 적용됩니다.",
                "dlssPresets", "dlssRrPreset",
                new[] { "게임", "기본값", "A", "B", "C", "D", "E", "F" },
                new[] { "game", "default", "a", "b", "c", "d", "e", "f" }, "game"),
            Choice("SR HDR 입력",
                "업스케일러의 색상 입력 모드. 게임: 게임 설정을 따름. 켜짐: HDR 선형 입력. 꺼짐: SDR 입력. 디스플레이 HDR을 켜지는 않습니다. 적용하려면 게임을 다시 시작하세요.",
                "superResolutionInputs", "srHdr", new[] { "게임", "켜짐", "꺼짐" }, new[] { "game", "on", "off" }, "game"),
            Choice("SR 자동 노출",
                "업스케일러 자동 노출. 게임: 게임 설정을 따름. 일부 DLSS 프리셋(L)은 이를 강제합니다. XeSS 자동 노출은 GPU 시간을 소모하며 SDR에는 권장하지 않습니다. 적용하려면 게임을 다시 시작하세요.",
                "superResolutionInputs", "srAutoExposure", new[] { "게임", "켜짐", "꺼짐" }, new[] { "game", "on", "off" }, "game"));

        // ── Smooth Motion ───────────────────────────────────────────────────────
        cog.AddSection("Smooth Motion");
        var gameApiValue = card.GraphicsApi switch
        {
            GraphicsApiType.DirectX11 => "d3d11",
            GraphicsApiType.Vulkan    => "vulkan",
            _                         => "d3d12",
        };
        var apiDisplays = new[] { "DirectX 12", "DirectX 11", "Vulkan" };
        var apiValues   = new[] { "d3d12", "d3d11", "vulkan" };
        var smCell = Choice("Smooth Motion",
            "NVIDIA 드라이버의 프레임 보간으로, 프레임 생성이 없는 게임에서도 작동합니다. RTX 40/50: NVIDIA 자체 Smooth Motion을 켭니다. RTX 30: DirectX 11, 12, Vulkan. " +
            "RTX 20(실험적): DirectX 12, 드라이버 621.01. RTX Encore가 명시한 드라이버 중 하나가 필요합니다. 적용하려면 게임을 다시 시작하세요.",
            "smoothMotion", "smoothMotionEnabled", RtxOffOn, RtxOffOnValues, "false",
            i =>
            {
                // Turning it on in a file without an API yet: use the game's own API, not RTX Encore's DirectX 12 default
                if (i == 1 && canEdit && RtxEncoreConfig.Get(cfgPath!, "smoothMotion", "smoothMotionApi") == null)
                    RtxEncoreConfig.SetString(cfgPath!, "smoothMotion", "smoothMotionApi", gameApiValue);
            });
        var smApiCell = Choice("그래픽 API",
            "Smooth Motion이 알아야 하는 게임의 그래픽 API입니다. 적용하려면 게임을 다시 시작하세요.",
            "smoothMotion", "smoothMotionApi", apiDisplays, apiValues, gameApiValue);
        cog.AddCompactGrid(smCell, smApiCell);

        // ── Neural Rendering DLL ────────────────────────────────────────────────
        cog.AddSection("Neural Rendering");
        AddRtxEncoreNrSetting(cog, card, svc, installPath, isInstalled, nrDeployed);
        cog.AddCompactGrid(
            Choice("Neural Rendering",
                "DLSS Neural Rendering을 켭니다. 위의 NR DLL이 필요합니다. 첫 실행 시 백그라운드에서 준비됩니다. 즉시 적용됩니다.",
                "neuralRendering/core", "nrEnabled", RtxOffOn, RtxOffOnValues, "false"),
            Choice("NR 모델",
                "B(기본값): 더 작고 훨씬 빠름. A: NR 310.8.0의 더 큰 네트워크. 자동: DLL이 선택(B). 즉시 적용됩니다.",
                "dlssPresets", "dlssNrPreset", new[] { "B", "A", "자동" }, new[] { "b", "a", "auto" }, "b"),
            Choice("NR 정밀도",
                "RTX 20/30: 정확은 NVIDIA와 동일한 출력을, 빠름은 약간의 이미지 차이로 더 빠른 처리를 제공합니다. RTX 40/50에서는 효과가 없습니다. 적용하려면 게임을 다시 시작하세요.",
                "neuralRendering/optimizations", "nrPrecision", new[] { "정확", "빠름" }, new[] { "exact", "fast" }, "exact"),
            Choice("NR 빠른 처리",
                "RTX 20/30: 출력은 완전히 동일하면서 Neural Rendering 처리가 더 빨라집니다. 적용하려면 게임을 다시 시작하세요.",
                "neuralRendering/optimizations", "nrFastProcessing", RtxOffOn, RtxOffOnValues, "true"));

        // ── Compatibility ───────────────────────────────────────────────────────
        cog.AddSection("호환성");
        cog.AddCompactGrid(
            Choice("GPU 시리즈",
                "RTX Encore가 GPU를 잘못 감지하는 경우가 아니면 자동으로 두세요. 적용하려면 게임을 다시 시작하세요.",
                "general", "gpuSeries", new[] { "자동", "RTX 40", "RTX 30", "RTX 20" }, new[] { "auto", "rtx40", "rtx30", "rtx20" }, "auto"),
            Choice("NVIDIA 앱 FG 모델",
                "NVIDIA 앱이 다운로드한 프레임 생성 모델을 불러옵니다. 적용하려면 게임을 다시 시작하세요.",
                "compatibility", "forceOTA", RtxOffOn, RtxOffOnValues, "false"),
            Choice("게임 내 메뉴 사용 안 함",
                "RTX Encore의 게임 내 메뉴를 거부하는 게임용입니다(Star Wars Outlaws). 프레임 생성은 계속 작동하며, 설정은 여기서 변경하세요. 적용하려면 게임을 다시 시작하세요.",
                "compatibility", "disableInGameMenu", RtxOffOn, RtxOffOnValues, "false"),
            Choice("메뉴에서 FG",
                "메뉴와 로딩 화면에서도 프레임 생성을 유지합니다. 꺼 두세요. 그곳에서 1x로 대기하면 장치 멈춤 충돌을 피할 수 있습니다.",
                "compatibility", "disableMenuDetection", RtxOffOn, RtxOffOnValues, "false"),
            Choice("레이 트레이싱 HairWorks",
                "검증된 프로필이 있는 게임에서 레이 트레이싱 HairWorks 변환을 준비합니다. 머리카락 설정은 여전히 게임 설정을 따릅니다. 적용하려면 게임을 다시 시작하세요.",
                "nativeMenuFeatures", "hairEnabled", RtxOffOn, RtxOffOnValues, "true"));

        // ── Intel XeSS files ────────────────────────────────────────────────────
        if (modDir != null)
        {
            var (intelComplete, inGameFolders) = Rtx40MfgService.GetEncoreIntelState(modDir);
            if (intelComplete)
                cog.AddNote("XeSS 업스케일러와 XeFG 엔진용 Intel XeSS SDK 파일이 설치되었습니다" +
                    (inGameFolders > 0 ? $" ({inGameFolders}개는 게임 자체 파일을 대체하며, RTX Encore를 제거하면 복원됩니다)." : "."));
            else
                cog.AddWarning("XeSS 업스케일러와 XeFG 엔진용 Intel XeSS SDK 파일(libxess.dll, libxess_fg.dll, libxell.dll)이 모두 설치되지 않았습니다. 추가하려면 RTX Encore를 재설치하세요.");
        }

        openCfgBtn = cog.MakeOpenFileButton("rtx-encore.jsonc 열기", cfgPath);
        cog.AddButtons(openCfgBtn,
            cog.MakeButton("GitHub 페이지 열기", CogButtonKind.Secondary,
                async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri(RtxEncoreService.RepoUrl)); }));

        await cog.ShowAsync();
    }

    /// <summary>Whole-number box for one rtx-encore.jsonc setting: saves on Enter and when focus leaves.</summary>
    private static TextBox BuildRtxEncoreNumberBox(CogDialog cog, string? cfgPath, string gameName,
        string section, string key, string label, int min, int max, int defaultValue, Action onSaved)
    {
        var fallback = defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var current = cfgPath != null ? RtxEncoreConfig.Get(cfgPath, section, key) : null;
        var box = CogDialog.MakeTextBox(current ?? fallback, fallback);
        box.IsEnabled = cfgPath != null;
        string saved = box.Text;
        void Save()
        {
            if (cfgPath == null) return;
            var text = box.Text.Trim();
            if (text == saved) return;
            if (!int.TryParse(text, out var value) || value < min || value > max)
            {
                cog.SetError($"{label}은(는) {min}~{max} 사이의 숫자여야 합니다.");
                box.Text = saved;
                return;
            }
            try
            {
                RtxEncoreConfig.SetInt(cfgPath, section, key, value);
                saved = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                box.Text = saved;
                CrashReporter.Log($"[RtxEncoreCog] '{gameName}': {section}.{key} = {value}");
                onSaved();
                cog.SetSuccess("저장했습니다. 게임이 실행 중이면 다시 시작하세요.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                CrashReporter.Log($"[RtxEncoreCog] {section}.{key} save failed — {ex.Message}");
                cog.SetError($"{label}을(를) 저장할 수 없습니다: {ex.Message}");
                box.Text = saved;
            }
        }
        box.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) { Save(); e.Handled = true; } };
        box.LostFocus += (_, _) => Save();
        return box;
    }

    /// <summary>The "Deploy NR DLL" setting: copies nvngx_dlssnr.dll 310.9.16 beside RTX Encore, or removes it.</summary>
    private void AddRtxEncoreNrSetting(CogDialog cog, GameCardViewModel card, RtxEncoreService svc,
        string installPath, bool isInstalled, bool nrDeployed)
    {
        var gameName = card.GameName;
        var store    = card.Source ?? "";
        bool nrOn = nrDeployed;
        bool suppress = false;
        ComboBox? nrCombo = null;

        void Revert()
        {
            if (nrCombo == null) return;
            suppress = true;
            nrCombo.SelectedIndex = nrOn ? 1 : 0;
            suppress = false;
        }

        async Task ApplyAsync(bool enable)
        {
            if (enable == nrOn) return;
            _window.ViewModel.SetRtxEncoreNrDllDeployed(gameName, enable, store);
            if (!isInstalled)
            {
                nrOn = enable;
                cog.SetSuccess(enable ? "저장했습니다. RTX Encore를 설치하면 nvngx_dlssnr.dll이 배포됩니다." : "저장했습니다.");
                return;
            }
            if (enable)
            {
                if (nrCombo != null) nrCombo.IsEnabled = false;
                try
                {
                    if (svc.GetNrDllCachedPath() == null)
                        cog.SetStatus($"nvngx_dlssnr.dll {RtxEncoreService.NrRequiredVer} 다운로드 중...");
                    var cached = await svc.EnsureNrDllCachedAsync(App.Services.GetRequiredService<IDlssStreamlineService>());
                    if (cached == null)
                    {
                        CrashReporter.Log($"[RtxEncoreCog] NR {RtxEncoreService.NrRequiredVer} not available — not deployed");
                        _window.ViewModel.SetRtxEncoreNrDllDeployed(gameName, false, store);
                        Revert();
                        cog.SetError($"nvngx_dlssnr.dll {RtxEncoreService.NrRequiredVer}을(를) 다운로드할 수 없습니다. 아무것도 배포되지 않았습니다.");
                        return;
                    }
                    var installedAs = _window.ViewModel.GetRtxEncoreInstalledAs(gameName, store);
                    if (await Task.Run(() => svc.DeployNrDll(installPath, installedAs)))
                    {
                        nrOn = true;
                        cog.SetSuccess($"nvngx_dlssnr.dll {RtxEncoreService.NrRequiredVer}을(를) RTX Encore 옆에 배포했습니다.");
                    }
                    else
                    {
                        _window.ViewModel.SetRtxEncoreNrDllDeployed(gameName, false, store);
                        Revert();
                        cog.SetError("nvngx_dlssnr.dll을 RTX Encore 옆에 복사할 수 없습니다.");
                    }
                    RequestExtrasRebuild(card);
                }
                finally
                {
                    if (nrCombo != null) nrCombo.IsEnabled = true;
                }
            }
            else
            {
                svc.RemoveNrDll(installPath, _window.ViewModel.GetRtxEncoreInstalledAs(gameName, store));
                nrOn = false;
                cog.SetSuccess("nvngx_dlssnr.dll을 제거했습니다.");
                RequestExtrasRebuild(card);
            }
        }

        nrCombo = cog.AddCombo("NR DLL 배포",
            $"Neural Rendering을 위해 NVIDIA의 Neural Rendering DLL(nvngx_dlssnr.dll {RtxEncoreService.NrRequiredVer})을 RTX Encore 옆(.asi의 경우 plugins\\)에 복사합니다. " +
            "그런 다음 아래 또는 RTX Encore의 게임 내 메뉴에서 Neural Rendering을 켜세요. RTX Encore가 아직 설치되지 않았다면 선택이 저장되었다가 설치 시 적용됩니다.",
            new[] { "아니요", "예" }, nrOn ? 1 : 0, i =>
            {
                if (suppress) return true;
                _ = ApplySafeAsync(i == 1);
                return true;
            });

        async Task ApplySafeAsync(bool enable)
        {
            try { await ApplyAsync(enable); }
            catch (Exception ex)
            {
                CrashReporter.Log($"[RtxEncoreCog] NR DLL change failed — {ex.Message}");
                cog.SetError($"NR DLL을 변경할 수 없습니다: {ex.Message}");
            }
        }
    }
}
