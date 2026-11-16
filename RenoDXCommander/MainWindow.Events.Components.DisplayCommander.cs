// MainWindow.Events.Components.DisplayCommander.cs — Display Commander (DC) cog dialog.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private const string DcSection = "DisplayCommander";

    /// <summary>
    /// Display Commander ⚙: the [DisplayCommander] settings worth choosing before launch, written to the
    /// game's DisplayCommander.ini. Labels match Display Commander's own menu. Defaults are the mod's own
    /// (settings/main_tab_settings.cpp and advanced_tab_settings.cpp in pmnoxx/display-commander).
    /// </summary>
    private async void DcCogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;

            // DisplayCommander.ini lives in the addon deploy path (honours AddonPath in reshade.ini), same as CopyDcIni.
            // Re-resolved on every write, as AddonPath may change while the dialog is open.
            string? CurrentIni() => string.IsNullOrEmpty(card.InstallPath)
                ? null
                : Path.Combine(ModInstallService.GetAddonDeployPath(card.InstallPath), "DisplayCommander.ini");
            var dcIniFile = CurrentIni();

            string? Read(string key)
            {
                try { return dcIniFile != null ? Dlssg20_30Service.ReadIniValue(dcIniFile, DcSection, key) : null; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    CrashReporter.Log($"[MainWindow.DcCogButton_Click] Couldn't read {key} — {ex.Message}");
                    return null;
                }
            }
            int ReadInt(string key, int fallback) => int.TryParse(Read(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
            bool ReadBool(string key) => ReadInt(key, 0) != 0;
            double fpsLimit = double.TryParse(Read("fps_limit"), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f > 0 ? f : 0;

            var cog = new CogDialog("Display Commander", Content.XamlRoot);

            // Writes one key; shows the result in the dialog. Returns false (pick reverted) on failure.
            bool Write(string key, string value, string label, string shown)
            {
                var iniFile = CurrentIni();
                if (iniFile == null || !Directory.Exists(Path.GetDirectoryName(iniFile)))
                {
                    cog.SetError("게임 폴더를 찾을 수 없습니다.");
                    return false;
                }
                try
                {
                    Dlssg20_30Service.WriteIniValue(iniFile, DcSection, key, value);
                    cog.SetSuccess($"{label}: {shown}.");
                    CrashReporter.Log($"[MainWindow.DcCogButton_Click] {key}={value} for '{card.GameName}'");
                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    cog.SetError($"{label}을(를) 저장할 수 없습니다: {ex.Message}");
                    CrashReporter.Log($"[MainWindow.DcCogButton_Click] {key} write failed — {ex.Message}");
                    return false;
                }
            }
            CogDialog.CompactCell OnOff(string key, string label, string help) =>
                cog.CompactCombo(label, help, new[] { "꺼짐", "켜짐" }, ReadBool(key) ? 1 : 0,
                    i => Write(key, i == 1 ? "1" : "0", label, i == 1 ? "켜짐" : "꺼짐"));

            cog.AddIntro("이 게임의 DisplayCommander.ini에 저장됩니다. Display Commander는 게임 실행 중에 설정을 저장하므로 " +
                         "게임을 종료한 상태에서 변경하세요. 나머지 설정은 게임 내 메뉴에 있습니다.");
            if (card.IsRunning)
                cog.AddWarning("게임이 실행 중입니다. 지금 변경하면 Display Commander가 덮어쓸 수 있으니 먼저 게임을 종료하세요.");

            // ── Frame rate ─────────────────────────────────────────────────────────
            cog.AddSection("프레임 속도");
            AddDcFpsLimitRow(cog, fpsLimit, fps => Write("fps_limit",
                fps.ToString("0.###", CultureInfo.InvariantCulture), "FPS 제한",
                fps == 0 ? "제한 없음" : $"{fps.ToString("0.###", CultureInfo.InvariantCulture)} FPS"));

            var limiterModes = new[] { "DC FPS 제한기", "NVIDIA Reflex" };
            var windowModes = new[] { "변경 안 함", "테두리 없는 전체 화면", "테두리 없는 창 모드", "전용 전체 화면 사용 안 함" };
            int windowMode = Math.Clamp(ReadInt("WindowMode", 0), 0, windowModes.Length - 1);
            cog.AddCompactGrid(
                cog.CompactCombo("FPS 제한기 모드",
                    "Display Commander가 프레임 속도를 제한하는 방식입니다. NVIDIA Reflex는 DX11 및 DX12 게임에서 작동하며, Vulkan 게임은 게임 자체 Reflex가 필요합니다.",
                    limiterModes, Math.Clamp(ReadInt("fps_limiter_mode", 0), 0, 1),
                    i => Write("fps_limiter_mode", i.ToString(CultureInfo.InvariantCulture), "FPS 제한기 모드", limiterModes[i])),
                OnOff("inject_reflex", "Reflex 주입",
                    "게임에 자체 Reflex가 없으면 Display Commander의 Reflex(슬립 및 지연 마커)를 사용해 지연을 줄입니다."),
                cog.CompactCombo("창 모드",
                    "변경 안 함: 창을 그대로 둡니다. 테두리 없는 전체 화면 / 창 모드: 게임 창 크기를 조정합니다. " +
                    "전용 전체 화면 사용 안 함: 크기 조정 없이 게임이 전용 전체 화면으로 전환되지 않게 합니다.",
                    windowModes, windowMode,
                    i => Write("WindowMode", i.ToString(CultureInfo.InvariantCulture), "창 모드", windowModes[i])),
                null);

            // ── Reflex (Advanced tab, "NVIDIA Reflex (Minimal)") ──────────────────
            cog.AddSection("Reflex");
            cog.AddCompactGrid(
                OnOff("ReflexAutoConfigure", "Reflex 자동 구성", "게임 시작 시 Reflex를 자동으로 구성합니다."),
                OnOff("ReflexEnableSleep", "Reflex 슬립 모드 사용", "Display Commander가 Reflex 슬립 호출을 하도록 허용합니다. 안전을 위해 기본값은 꺼짐입니다."),
                OnOff("ReflexUseMarkers", "Reflex 마커 사용", "NVIDIA Reflex가 지연 마커를 사용하도록 합니다."),
                OnOff("ReflexGenerateMarkers", "Reflex 마커 생성", "지연 측정을 위해 프레임 타임라인에 마커를 추가합니다."));

            // ── Other ─────────────────────────────────────────────────────────────
            cog.AddSection("기타");
            var graceSeconds = new[] { 0, 1, 2, 5, 10, 61 };
            var graceLabels = new[] { "꺼짐", "1초", "2초", "5초", "10초", "항상" };
            int grace = ReadInt("WinUpGraceSeconds", 1);
            int graceIdx = Array.IndexOf(graceSeconds, grace);
            var graceItems = graceLabels.ToList();
            if (graceIdx < 0) { graceItems.Add($"{grace}초"); graceIdx = graceItems.Count - 1; }
            cog.AddCompactGrid(
                cog.CompactCombo("Win+Up 유예 시간",
                    "테두리 없는 게임에서 전환한 후 Win+Up으로 창을 복원할 수 있는 시간입니다. " +
                    "Display Commander 메뉴에는 없으며 ini에서만 설정됩니다.",
                    graceItems, graceIdx, i =>
                    {
                        if (i >= graceSeconds.Length) return true; // existing custom value
                        return Write("WinUpGraceSeconds", graceSeconds[i].ToString(CultureInfo.InvariantCulture), "Win+Up 유예 시간", graceLabels[i]);
                    }),
                null);
            cog.AddNote("대상 디스플레이와 Display Commander의 ReShade 옵션(전역 셰이더 경로, 스크린샷 경로)은 게임 내 메뉴에서 설정하세요. " +
                        "RHI는 ReShade의 셰이더 및 스크린샷 폴더를 직접 관리합니다.");

            // ── Files ─────────────────────────────────────────────────────────────
            cog.AddSection("파일");
            var openIniBtn = cog.MakeOpenFileButton("DisplayCommander.ini 열기", dcIniFile);
            var deployBtn = cog.MakeButton("DisplayCommander.ini 배포", CogButtonKind.Primary, () =>
            {
                if (string.IsNullOrEmpty(card.InstallPath)) return;
                AuxInstallService.CopyDcIni(card.InstallPath);
                openIniBtn.IsEnabled = dcIniFile != null && File.Exists(dcIniFile);
                cog.SetSuccess("DisplayCommander.ini를 게임 폴더에 복사했습니다. 설정을 보려면 이 대화 상자를 닫았다가 다시 여세요.");
                CrashReporter.Log($"[MainWindow.DcCogButton_Click] Deployed DisplayCommander.ini for '{card.GameName}'");
            }, "RHI의 DisplayCommander.ini 템플릿을 게임 폴더에 복사해 현재 파일을 대체합니다.");
            cog.AddButtons(deployBtn, openIniBtn);

            // No default button: Enter in the FPS box must not close the dialog
            cog.Build().DefaultButton = ContentDialogButton.None;
            await cog.ShowAsync();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.DcCogButton_Click] Unhandled error — {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// FPS Limit dropdown (No limit, VRR presets, Custom...) with an inline box for a custom value.
    /// Display Commander stores a decimal (e.g. 239.99), so custom values may have decimals.
    /// </summary>
    private static void AddDcFpsLimitRow(CogDialog cog, double current, Func<double, bool> write)
    {
        var presets = VrrFpsPresets;
        string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        List<string> Items(double fps)
        {
            var items = new List<string> { "제한 없음" };
            items.AddRange(presets.Select(p => p.Label));
            if (fps > 0 && !presets.Any(p => p.Fps == fps)) items.Add($"{Fmt(fps)} (사용자 지정)");
            items.Add("Custom...");
            return items;
        }
        int Index(List<string> items, double fps)
        {
            if (fps <= 0) return 0;
            int i = Array.FindIndex(presets, p => p.Fps == fps);
            return i >= 0 ? i + 1 : items.Count - 2;
        }

        var items0 = Items(current);
        var combo = CogDialog.MakeCombo(items0, Index(items0, current));
        cog.AddRow("FPS 제한", combo,
            "이 게임의 프레임 속도 상한입니다. 각 VRR 프리셋은 해당 주사율보다 약간 낮게 설정됩니다. Custom...을 선택하면 직접 값을 입력할 수 있습니다.");

        var box = CogDialog.MakeTextBox("", "20-1000, 소수 허용");
        var setBtn = CogDialog.StyledButton("설정", CogButtonKind.Secondary, compact: true);
        var customRow = cog.AddRow("사용자 지정 FPS", CogDialog.Inline(box, setBtn), "20에서 1000 사이의 프레임 속도를 입력한 후 설정 또는 Enter를 누르세요.");
        customRow.Visibility = Visibility.Collapsed;

        bool suppress = false;
        void Refresh(double fps)
        {
            suppress = true;
            current = fps;
            var items = Items(fps);
            combo.ItemsSource = items;
            combo.SelectedIndex = Index(items, fps);
            customRow.Visibility = Visibility.Collapsed;
            suppress = false;
        }

        combo.SelectionChanged += (_, _) =>
        {
            if (suppress) return;
            if (combo.SelectedItem as string == "Custom...")
            {
                customRow.Visibility = Visibility.Visible;
                box.Text = "";
                box.Focus(FocusState.Programmatic);
                return;
            }
            customRow.Visibility = Visibility.Collapsed;
            int idx = combo.SelectedIndex;
            double fps;
            if (idx == 0) fps = 0;
            else if (idx > 0 && idx - 1 < presets.Length) fps = presets[idx - 1].Fps;
            else return; // the "(Custom)" item: already the current value
            if (write(fps)) current = fps;
            else Refresh(current);
        };

        void SetCustom()
        {
            var text = box.Text.Trim().Replace(',', '.');
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fps) || fps < 20 || fps > 1000)
            {
                cog.SetError("20에서 1000 사이의 프레임 속도를 입력하세요.");
                return;
            }
            fps = Math.Round(fps, 3);
            if (write(fps)) Refresh(fps);
        }
        setBtn.Click += (_, _) => SetCustom();
        box.KeyDown += (_, ev) =>
        {
            if (ev.Key != Windows.System.VirtualKey.Enter) return;
            ev.Handled = true;
            SetCustom();
        };
    }
}
