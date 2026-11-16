// DetailPanelBuilder.Extras.FrameGen.Dlssg2030Settings.cs — ⚙ dialog for 20/30 FG Unlock.
// Edits the game's dlssg_sm86.ini. The mod treats a mistyped value as "turn frame generation
// off", so every setting is a dropdown of valid values only.

using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private async Task ShowDlssg2030SettingsAsync(string installPath, bool isInstalled)
    {
        var iniPath = string.IsNullOrEmpty(installPath) ? "" : Path.Combine(installPath, Dlssg20_30Service.IniFileName);
        var logPath = string.IsNullOrEmpty(installPath) ? "" : Path.Combine(installPath, Dlssg20_30Service.LogRelPath);
        bool hasIni = isInstalled && File.Exists(iniPath);

        var cog = new CogDialog("20/30 FG Unlock", _window.Content.XamlRoot);
        cog.AddIntro("이 설정은 게임의 dlssg_sm86.ini에 저장됩니다. 변경 사항을 적용하려면 게임을 완전히 종료한 후 다시 시작하세요. " +
                     "NVIDIA 드라이버 R580 이상이 필요하며, 이전 드라이버에서는 모드가 스스로 꺼집니다.");
        if (!hasIni)
            cog.AddWarning("이 설정을 변경하려면 이 게임에 20/30 FG Unlock을 설치하세요.");

        // One labelled dropdown bound to [section] key. Options are (label, ini value).
        ComboBox AddSetting(string label, string help, string section, string key, string fallback,
            (string Label, string Value)[] options, Func<string, bool>? beforeWrite = null)
        {
            var current = hasIni ? Dlssg20_30Service.ReadIniValue(iniPath, section, key) : null;
            var idx = Array.FindIndex(options, o => o.Value.Equals(current ?? fallback, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = Array.FindIndex(options, o => o.Value == fallback);
            return cog.AddCombo(label, help, options.Select(o => o.Label).ToList(), idx, i =>
            {
                var value = options[i].Value;
                if (beforeWrite != null && !beforeWrite(value)) return false;
                try
                {
                    Dlssg20_30Service.WriteIniValue(iniPath, section, key, value);
                    cog.SetSuccess($"{label} 저장됨. 적용하려면 게임을 다시 시작하세요.");
                    CrashReporter.Log($"[Dlssg2030Settings] {section}.{key}={value} in '{installPath}'");
                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    cog.SetError($"{label}을(를) 저장할 수 없습니다: {ex.Message}");
                    CrashReporter.Log($"[Dlssg2030Settings] Write failed — {ex.Message}");
                    return false;
                }
            }, isEnabled: hasIni);
        }

        AddSetting("프레임 생성",
            "꺼짐으로 설정하면 제거하지 않고도 게임 자체의 DLSS 프레임 생성을 그대로 둡니다 (RTX 20/30에서는 FG 없음).",
            "General", "Enabled", "1",
            new[] { ("켜짐", "1"), ("꺼짐", "0") });

        ComboBox? framesCombo = null;
        bool engineIsXess = hasIni && "XeSS".Equals(Dlssg20_30Service.ReadIniValue(iniPath, "FrameGeneration", "Engine"), StringComparison.OrdinalIgnoreCase);
        AddSetting("엔진",
            "DLSS는 NVIDIA의 프레임 생성 모델을 사용합니다. XeSS는 Intel의 모델을 사용하며 최대 4X까지 지원하고, HDR(16비트) 출력에서는 작동하지 않습니다.",
            "FrameGeneration", "Engine", "DLSS",
            new[] { ("DLSS (기본값)", "DLSS"), ("XeSS", "XeSS") },
            beforeWrite: value =>
            {
                engineIsXess = value == "XeSS";
                // XeSS can't do 5X/6X: the mod would turn FG off, so drop to 4X
                if (engineIsXess && framesCombo != null && framesCombo.SelectedIndex is 3 or 4)
                    framesCombo.SelectedIndex = 2;
                return true;
            });

        framesCombo = AddSetting("최대 프레임",
            "게임이 제공할 수 있는 최대 배수입니다. 게임 자체 메뉴에서 더 낮은 값을 선택할 수 있습니다. 6X는 여기서 선택해야 합니다.",
            "FrameGeneration", "MaxGeneratedFrames", "3",
            new[] { ("2X", "1"), ("3X", "2"), ("4X (기본값)", "3"), ("5X", "4"), ("6X", "5"), ("게임이 결정", "0") },
            beforeWrite: value =>
            {
                if ((value == "4" || value == "5") && engineIsXess)
                {
                    cog.SetError("XeSS는 최대 4X까지 지원합니다. 5X 또는 6X를 사용하려면 엔진을 DLSS로 전환하세요.");
                    return false;
                }
                return true;
            });

        AddSetting("프리셋",
            "A가 기본값입니다. B는 생성된 프레임의 HUD를 정리하지만 게임이 추가 이미지를 제공할 때만 작동합니다 (대부분 제공하지 않음). C는 FG Lite로, 훨씬 가볍지만 품질이 약간 낮습니다.",
            "DLSSG", "Preset", "A",
            new[] { ("A (기본값)", "A"), ("B: HUD 재구성", "B"), ("C: FG Lite", "C") });

        AddSetting("속도 단계",
            "이미지가 NVIDIA 공식 출력과 얼마나 달라도 되는지 정합니다. 정확 단계는 비트 단위로 일치하며, 빠름과 가장 빠름은 미세한 차이로 몇 퍼센트 더 빠릅니다.",
            "DLSSG", "Optimized", "0",
            new[] { ("정확", "0"), ("정확, 최적화 (기본값)", "1"), ("빠름", "2"), ("가장 빠름", "3") });

        AddSetting("슈퍼 해상도",
            "RTX 20/30 전용. DLSS 프리셋 M 또는 L을 고정합니다. INT8은 모드의 더 빠른 커널을 사용하여 Performance 및 Ultra Performance에서는 더 빠르고, Quality, Balanced, DLAA에서는 게임 기본값보다 느립니다. 레이 재구성이 켜져 있으면 효과가 없습니다.",
            "SuperResolution", "Mode", "Off",
            new[] { ("꺼짐 (기본값)", "Off"), ("M-INT8", "M-INT8"), ("L-INT8", "L-INT8"), ("M-FP16", "M-FP16"), ("L-FP16", "L-FP16") });

        cog.AddSection("파일");
        cog.AddButtons(cog.MakeOpenFileButton("dlssg_sm86.ini 열기", iniPath), cog.MakeOpenFileButton("로그 열기", logPath));

        await cog.ShowAsync();
    }
}
