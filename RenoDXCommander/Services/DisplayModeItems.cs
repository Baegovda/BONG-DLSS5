// DisplayModeItems.cs -- the things a Display mode can swap per game (peak brightness, frame rate caps, keys,
// DLSS settings, HDR / resolution on launch, ...). Each item knows whether it can be used for a game, how to read
// the game's current value and how to write a new one. Values are strings (see each item's comment).
// Read / Write touch game files or the NVIDIA profile, so they run on a background thread (DisplayModeService).
// "Launch" items aren't stored anywhere else: their value for the active mode is used when RHI launches the game.

using System.Globalization;
using RenoDXCommander.Models;

namespace RenoDXCommander.Services;

/// <summary>Item keys (saved in settings.json; never rename).</summary>
public static class DisplayModeItemKeys
{
    public const string RdxPeakNits = "RdxPeakNits";
    public const string RdxGameNits = "RdxGameNits";
    public const string RdxUiNits = "RdxUiNits";
    public const string RtxHdrPeak = "RtxHdrPeak";
    public const string UlTargetFps = "UlTargetFps";
    public const string DcFpsLimit = "DcFpsLimit";
    public const string DriverFpsCap = "DriverFpsCap";
    public const string MfgMode = "MfgMode";
    public const string DlssSrPreset = "DlssSrPreset";
    public const string DlssSrScale = "DlssSrScale";
    public const string DlssRrPreset = "DlssRrPreset";
    public const string DlssRrScale = "DlssRrScale";
    public const string RsPreset = "RsPreset";
    public const string RsOverlayKey = "RsOverlayKey";
    public const string RsScreenshotKey = "RsScreenshotKey";
    public const string RsGamepadNav = "RsGamepadNav";
    public const string UlOsdKey = "UlOsdKey";
    public const string Hdr = "Hdr";
    public const string HdrDisplay = "HdrDisplay";
    public const string Resolution = "Resolution";
    public const string LaunchArgs = "LaunchArgs";

    /// <summary>Items kept in reshade.ini (protected from ReShade installs / updates and the Settings "write to all" buttons).</summary>
    public static readonly IReadOnlyList<string> ReShadeIniItems = new[] { RdxPeakNits, RdxGameNits, RdxUiNits, RsPreset, RsOverlayKey, RsScreenshotKey, RsGamepadNav };
    /// <summary>Items kept in relimiter.ini.</summary>
    public static readonly IReadOnlyList<string> ReLimiterItems = new[] { UlTargetFps, UlOsdKey };
}

/// <summary>How an item's value is edited.</summary>
public enum DisplayModeValueKind { Number, Decimal, Choice, Text, ReShadeKey, ReLimiterKey }

/// <summary>What a mode item needs to know about one game. Built on the UI thread, used on a background thread.</summary>
public sealed class DisplayModeContext
{
    public string GameName { get; init; } = "";
    public string Store { get; init; } = "";
    public string InstallPath { get; init; } = "";
    public bool HasRenoDx { get; init; }
    public bool IsRtxHdrEnabled { get; init; }
    public DlssPresetService? Nvidia { get; init; }

    public string ReShadeIni => Path.Combine(InstallPath, "reshade.ini");

    private string? _addonDir;
    /// <summary>Where ReLimiter / Display Commander keep their ini (AddonPath in reshade.ini).</summary>
    public string AddonDir => _addonDir ??= string.IsNullOrEmpty(InstallPath) ? "" : ModInstallService.GetAddonDeployPath(InstallPath);
    public string ReLimiterIni => Path.Combine(AddonDir, "relimiter.ini");
    public string DcIni => Path.Combine(AddonDir, "DisplayCommander.ini");

    public string Key => GameKey.From(GameName, Store).ToKey();
    public bool NvidiaReady => Nvidia?.IsSupported == true;
}

/// <summary>One swappable thing.</summary>
public sealed class DisplayModeItem
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Group { get; init; }
    public required string Help { get; init; }
    public DisplayModeValueKind Kind { get; init; }
    public string? Unit { get; init; }
    /// <summary>True: used when RHI launches the game (HDR, resolution, ...), nothing is written when switching.</summary>
    public bool AtLaunch { get; init; }
    /// <summary>(label, value) pairs for Choice items.</summary>
    public Func<IReadOnlyList<(string Label, string Value)>>? Choices { get; init; }
    /// <summary>Null when the item can be used for the game, else a short reason ("ReLimiter isn't installed").</summary>
    public Func<DisplayModeContext, string?> Unavailable { get; init; } = _ => null;
    /// <summary>The game's current value, or null when it has none / it can't be read.</summary>
    public Func<DisplayModeContext, string?> Read { get; init; } = _ => null;
    /// <summary>Writes a value; returns null on success, else an error message.</summary>
    public Func<DisplayModeContext, string, string?> Write { get; init; } = (_, _) => null;
    /// <summary>Lowest / highest accepted value for Number / Decimal items.</summary>
    public double Min { get; init; }
    public double Max { get; init; } = double.MaxValue;
    /// <summary>0 is accepted and means off / no limit.</summary>
    public bool AllowZero { get; init; }

    /// <summary>A value as shown to the user ("700 nits", "F12", "On", ...).</summary>
    public string Format(string? value)
    {
        if (value == null) return "설정 안 됨";
        switch (Kind)
        {
            case DisplayModeValueKind.Choice:
                var c = Choices?.Invoke().FirstOrDefault(x => x.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
                return c is { Label: not null } hit ? hit.Label : value;
            case DisplayModeValueKind.ReShadeKey:
                return HotkeyManager.FormatHotkeyDisplay(value);
            case DisplayModeValueKind.Text:
                return value.Length == 0 ? "없음" : value;
            case DisplayModeValueKind.Number:
            case DisplayModeValueKind.Decimal:
                if (Key is DisplayModeItemKeys.UlTargetFps or DisplayModeItemKeys.DcFpsLimit && IsZero(value)) return "Off";
                return Unit == null ? value : $"{value} {Unit}";
            default:
                return value;
        }
    }

    /// <summary>Checks and normalises a typed value; returns null when it isn't valid.</summary>
    public string? Normalize(string? text)
    {
        var t = (text ?? "").Trim();
        switch (Kind)
        {
            case DisplayModeValueKind.Number:
                if (!int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return null;
                if (i == 0) return AllowZero ? "0" : null;
                if (i < Min || i > Max) return null;
                return i.ToString(CultureInfo.InvariantCulture);
            case DisplayModeValueKind.Decimal:
                if (!double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return null;
                if (d == 0) return AllowZero ? "0" : null;
                if (d < Min || d > Max) return null;
                return Math.Round(d, 3).ToString("0.###", CultureInfo.InvariantCulture);
            default:
                return t;
        }
    }

    private static bool IsZero(string v) => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d == 0;
}

/// <summary>All items, in the order they're listed.</summary>
public static class DisplayModeItems
{
    public const string GroupHdr = "HDR 및 밝기";
    public const string GroupFrameRate = "프레임 속도";
    public const string GroupImage = "이미지";
    public const string GroupControls = "조작";
    public const string GroupLaunch = "RHI가 게임을 실행할 때";

    public static IReadOnlyList<string> Groups { get; } = new[] { GroupHdr, GroupFrameRate, GroupImage, GroupControls, GroupLaunch };

    private static IReadOnlyList<DisplayModeItem>? _all;
    public static IReadOnlyList<DisplayModeItem> All => _all ??= Build();

    public static DisplayModeItem? Get(string key) => All.FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    // ── Reasons ───────────────────────────────────────────────────────────────

    private const string NoReShadeIni = "ReShade가 설치되어 있지 않음 (reshade.ini 없음)";
    private const string NoRenoDx = "설치된 RenoDX 모드 없음";
    private const string NoReLimiter = "ReLimiter가 설치되어 있지 않음 (relimiter.ini 없음)";
    private const string NoDc = "Display Commander가 설치되어 있지 않음 (DisplayCommander.ini 없음)";
    private const string NoNvidia = "NVIDIA GPU 및 드라이버 필요";

    private static string? NeedReShade(DisplayModeContext c) => File.Exists(c.ReShadeIni) ? null : NoReShadeIni;
    private static string? NeedRenoDx(DisplayModeContext c) => !c.HasRenoDx ? NoRenoDx : NeedReShade(c);
    private static string? NeedReLimiter(DisplayModeContext c) => File.Exists(c.ReLimiterIni) ? null : NoReLimiter;
    private static string? NeedDc(DisplayModeContext c) => File.Exists(c.DcIni) ? null : NoDc;
    private static string? NeedNvidia(DisplayModeContext c) => c.NvidiaReady ? null : NoNvidia;

    private static IReadOnlyList<DisplayModeItem> Build() => new List<DisplayModeItem>
    {
        // ── HDR and brightness ────────────────────────────────────────────────
        RdxNits(DisplayModeItemKeys.RdxPeakNits, "toneMapPeakNits", "최대 밝기 (RenoDX)",
            "RenoDX의 최대 밝기입니다. 디스플레이의 최대 밝기로 설정하세요: TV와 모니터는 크게 다른 경우가 많습니다."),
        RdxNits(DisplayModeItemKeys.RdxGameNits, "toneMapGameNits", "게임 밝기 (RenoDX)",
            "RenoDX의 게임(종이 흰색) 밝기입니다. 어두운 방에서는 보통 더 낮은 값이 좋습니다."),
        RdxNits(DisplayModeItemKeys.RdxUiNits, "toneMapUINits", "UI 밝기 (RenoDX)",
            "메뉴와 HUD에 대한 RenoDX의 밝기입니다."),
        new()
        {
            Key = DisplayModeItemKeys.RtxHdrPeak, Label = "RTX HDR 최대 밝기", Group = GroupHdr, Kind = DisplayModeValueKind.Number,
            Unit = "nits", Min = 100, Max = 10000,
            Help = "NVIDIA RTX HDR의 최대 밝기입니다 (NVIDIA 드라이버 프로필). RenoDX ⚙에서 RTX HDR을 켠 게임에만 해당합니다.",
            Unavailable = c => !c.IsRtxHdrEnabled ? "이 게임에서 RTX HDR이 켜져 있지 않음" : NeedNvidia(c),
            Read = c =>
            {
                uint v = c.Nvidia!.GetRtxHdrPeakBrightness(c.GameName, c.InstallPath);
                return v > 0 ? v.ToString(CultureInfo.InvariantCulture) : null;
            },
            Write = (c, v) => uint.TryParse(v, out var n) && c.Nvidia!.SetRtxHdrPeakBrightness(c.GameName, c.InstallPath, n)
                ? null : "NVIDIA 프로필에 기록할 수 없음",
        },

        // ── Frame rate ────────────────────────────────────────────────────────
        new()
        {
            Key = DisplayModeItemKeys.UlTargetFps, Label = "ReLimiter 프레임 속도 상한", Group = GroupFrameRate, Kind = DisplayModeValueKind.Number,
            Unit = "FPS", Min = 20, Max = 1000, AllowZero = true,
            Help = "게임의 relimiter.ini에 있는 target_fps입니다. 0 = 꺼짐.",
            Unavailable = NeedReLimiter,
            // No relimiter.ini = no value (not "off"); a file without target_fps = off
            Read = c => !File.Exists(c.ReLimiterIni) ? null
                : Dlssg20_30Service.ReadIniValue(c.ReLimiterIni, "FrameLimiter", "target_fps") is { } v
                && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n.ToString(CultureInfo.InvariantCulture) : "0",
            Write = (c, v) =>
            {
                if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return "숫자가 아님";
                AuxInstallService.ApplyUlTargetFps(c.ReLimiterIni, n);
                foreach (var extra in ReLimiterExtraInis(c)) AuxInstallService.ApplyUlTargetFps(extra, n);
                return null;
            },
        },
        new()
        {
            Key = DisplayModeItemKeys.DcFpsLimit, Label = "Display Commander FPS 제한", Group = GroupFrameRate, Kind = DisplayModeValueKind.Decimal,
            Unit = "FPS", Min = 20, Max = 1000, AllowZero = true,
            Help = "게임의 DisplayCommander.ini에 있는 fps_limit입니다. 0 = 제한 없음. 소수 허용 (예: 117.5).",
            Unavailable = NeedDc,
            Read = c => !File.Exists(c.DcIni) ? null
                : Dlssg20_30Service.ReadIniValue(c.DcIni, "DisplayCommander", "fps_limit") is { } v
                && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? Math.Round(d, 3).ToString("0.###", CultureInfo.InvariantCulture) : "0",
            Write = (c, v) => { Dlssg20_30Service.WriteIniValue(c.DcIni, "DisplayCommander", "fps_limit", v); return null; },
        },
        new()
        {
            Key = DisplayModeItemKeys.DriverFpsCap, Label = "NVIDIA 드라이버 FPS 제한", Group = GroupFrameRate, Kind = DisplayModeValueKind.Choice,
            Help = "이 게임에 대한 드라이버의 최대 프레임 속도입니다 (NVIDIA 프로필). 전역 설정 따름 = 자체 값 없음.",
            Choices = DriverFpsChoices,
            Unavailable = NeedNvidia,
            Read = c => c.Nvidia!.GetPerGameFpsLimit(c.GameName, c.InstallPath) is uint v ? v.ToString(CultureInfo.InvariantCulture) : "global",
            Write = (c, v) =>
            {
                bool ok = v == "global"
                    ? c.Nvidia!.ClearPerGameFpsLimit(c.GameName, c.InstallPath)
                    : uint.TryParse(v, out var n) && c.Nvidia!.SetPerGameFpsLimit(c.GameName, c.InstallPath, n);
                return ok ? null : "NVIDIA 프로필에 기록할 수 없음";
            },
        },
        new()
        {
            Key = DisplayModeItemKeys.MfgMode, Label = "DLSS 프레임 생성", Group = GroupFrameRate, Kind = DisplayModeValueKind.Choice,
            Help = "이 게임에 대한 드라이버의 프레임 생성 재정의: 꺼짐, 고정 배수(예: 120 Hz TV는 2x, 240 Hz 모니터는 4x) 또는 동적.",
            Choices = () => MfgChoices,
            Unavailable = NeedNvidia,
            Read = c =>
            {
                uint mode = c.Nvidia!.GetMfgMode(c.GameName, c.InstallPath);
                if (mode == 2) return "fixed:" + c.Nvidia.GetMfgGenerationFactor(c.GameName, c.InstallPath).ToString(CultureInfo.InvariantCulture);
                if (mode == 4) return "dynamic";
                return "off";
            },
            Write = (c, v) =>
            {
                var n = c.Nvidia!;
                if (v == "dynamic")
                {
                    n.SetMfgMode(c.GameName, c.InstallPath, 4);
                    n.SetMfgGenerationFactor(c.GameName, c.InstallPath, 0);
                    return null;
                }
                if (v.StartsWith("fixed:", StringComparison.Ordinal) && uint.TryParse(v[6..], out var f) && f is >= 1 and <= 5)
                {
                    n.SetMfgMode(c.GameName, c.InstallPath, 2);
                    n.SetMfgGenerationFactor(c.GameName, c.InstallPath, f);
                    n.DeleteMfgDynamicMaxCount(c.GameName, c.InstallPath);
                    n.DeleteMfgDynamicTargetFps(c.GameName, c.InstallPath);
                    return null;
                }
                n.SetMfgMode(c.GameName, c.InstallPath, 0);
                n.SetMfgGenerationFactor(c.GameName, c.InstallPath, 0);
                n.DeleteMfgDynamicMaxCount(c.GameName, c.InstallPath);
                n.DeleteMfgDynamicTargetFps(c.GameName, c.InstallPath);
                return null;
            },
        },

        // ── Image ─────────────────────────────────────────────────────────────
        new()
        {
            Key = DisplayModeItemKeys.DlssSrPreset, Label = "DLSS 슈퍼 해상도 프리셋", Group = GroupImage, Kind = DisplayModeValueKind.Choice,
            Help = "이 게임에 대한 드라이버의 DLSS SR 프리셋 재정의입니다 (NVIDIA 프로필).",
            Choices = () => DlssPresetService.SrPresets.Select(p => (p.Name, p.Value.ToString(CultureInfo.InvariantCulture))).ToList(),
            Unavailable = NeedNvidia,
            Read = c => c.Nvidia!.GetSrPreset(c.GameName, c.InstallPath).ToString(CultureInfo.InvariantCulture),
            Write = (c, v) => uint.TryParse(v, out var p) && c.Nvidia!.SetSrPreset(c.GameName, c.InstallPath, p) ? null : "NVIDIA 프로필에 기록할 수 없음",
        },
        new()
        {
            Key = DisplayModeItemKeys.DlssSrScale, Label = "DLSS 슈퍼 해상도 렌더 배율", Group = GroupImage, Kind = DisplayModeValueKind.Choice,
            Help = "드라이버의 DLSS SR 렌더 배율 재정의입니다. 예: 4K TV에서는 Performance, 1440p 모니터에서는 Quality.",
            Choices = () => DlssPresetService.RenderScaleOptions.Where(o => o.Value != 0xFFFFFFFF)
                .Select(o => (o.Name, o.Value.ToString(CultureInfo.InvariantCulture))).ToList(),
            Unavailable = NeedNvidia,
            Read = c => c.Nvidia!.GetSrRenderScale(c.GameName, c.InstallPath).ToString(CultureInfo.InvariantCulture),
            Write = (c, v) => uint.TryParse(v, out var p) && c.Nvidia!.SetSrRenderScale(c.GameName, c.InstallPath, p) ? null : "NVIDIA 프로필에 기록할 수 없음",
        },
        new()
        {
            Key = DisplayModeItemKeys.DlssRrPreset, Label = "DLSS 레이 재구성 프리셋", Group = GroupImage, Kind = DisplayModeValueKind.Choice,
            Help = "이 게임에 대한 드라이버의 DLSS 레이 재구성 프리셋 재정의입니다 (NVIDIA 프로필). 레이 재구성을 사용하는 게임에 적용됩니다.",
            Choices = () => DlssPresetService.RrPresets.Select(p => (p.Name, p.Value.ToString(CultureInfo.InvariantCulture))).ToList(),
            Unavailable = NeedNvidia,
            Read = c => c.Nvidia!.GetRrPreset(c.GameName, c.InstallPath).ToString(CultureInfo.InvariantCulture),
            Write = (c, v) => uint.TryParse(v, out var p) && c.Nvidia!.SetRrPreset(c.GameName, c.InstallPath, p) ? null : "NVIDIA 프로필에 기록할 수 없음",
        },
        new()
        {
            Key = DisplayModeItemKeys.DlssRrScale, Label = "DLSS 레이 재구성 렌더 배율", Group = GroupImage, Kind = DisplayModeValueKind.Choice,
            Help = "드라이버의 DLSS 레이 재구성 렌더 배율 재정의입니다. 레이 재구성이 켜져 있는 동안 슈퍼 해상도 배율 대신 사용됩니다.",
            Choices = () => DlssPresetService.RenderScaleOptions.Where(o => o.Value != 0xFFFFFFFF)
                .Select(o => (o.Name, o.Value.ToString(CultureInfo.InvariantCulture))).ToList(),
            Unavailable = NeedNvidia,
            Read = c => c.Nvidia!.GetRrRenderScale(c.GameName, c.InstallPath).ToString(CultureInfo.InvariantCulture),
            Write = (c, v) => uint.TryParse(v, out var p) && c.Nvidia!.SetRrRenderScale(c.GameName, c.InstallPath, p) ? null : "NVIDIA 프로필에 기록할 수 없음",
        },
        new()
        {
            Key = DisplayModeItemKeys.RsPreset, Label = "ReShade 프리셋", Group = GroupImage, Kind = DisplayModeValueKind.Text,
            Help = "게임이 불러오는 ReShade 프리셋 파일입니다 (reshade.ini의 PresetPath). 예: TV용 더 선명한 프리셋. TV.ini 같은 이름은 게임 폴더 기준 상대 경로입니다.",
            Unavailable = NeedReShade,
            Read = c => Dlssg20_30Service.ReadIniValue(c.ReShadeIni, "GENERAL", "PresetPath"),
            Write = (c, v) =>
            {
                if (v.Length == 0) return "프리셋 파일 이름을 입력하세요";
                Dlssg20_30Service.WriteIniValue(c.ReShadeIni, "GENERAL", "PresetPath", v);
                return null;
            },
        },

        // ── Controls ──────────────────────────────────────────────────────────
        new()
        {
            Key = DisplayModeItemKeys.RsOverlayKey, Label = "ReShade 오버레이 키", Group = GroupControls, Kind = DisplayModeValueKind.ReShadeKey,
            Help = "ReShade 오버레이를 여는 키입니다. 게임 폴더의 모든 reshade*.ini에 저장됩니다.",
            Unavailable = NeedReShade,
            Read = c => Dlssg20_30Service.ReadIniValue(c.ReShadeIni, "INPUT", "KeyOverlay"),
            Write = (c, v) => { foreach (var f in ReShadeInis(c)) AuxInstallService.ApplyOverlayHotkey(f, v); return null; },
        },
        new()
        {
            Key = DisplayModeItemKeys.RsScreenshotKey, Label = "ReShade 스크린샷 키", Group = GroupControls, Kind = DisplayModeValueKind.ReShadeKey,
            Help = "ReShade 스크린샷 키입니다 (Backspace = 없음). 게임 폴더의 모든 reshade*.ini에 저장됩니다.",
            Unavailable = NeedReShade,
            Read = c => Dlssg20_30Service.ReadIniValue(c.ReShadeIni, "INPUT", "KeyScreenshot"),
            Write = (c, v) => { foreach (var f in ReShadeInis(c)) AuxInstallService.ApplyScreenshotHotkey(f, v); return null; },
        },
        new()
        {
            Key = DisplayModeItemKeys.RsGamepadNav, Label = "ReShade 컨트롤러 탐색", Group = GroupControls, Kind = DisplayModeValueKind.Choice,
            Help = "컨트롤러로 ReShade 오버레이를 탐색할 수 있게 합니다 (reshade.ini의 GamepadNavigation). TV에서 편리합니다.",
            Choices = () => OffOn01,
            Unavailable = NeedReShade,
            Read = c => Dlssg20_30Service.ReadIniValue(c.ReShadeIni, "INPUT", "GamepadNavigation") is { } v ? (v == "1" ? "1" : "0") : "0",
            Write = (c, v) => { foreach (var f in ReShadeInis(c)) Dlssg20_30Service.WriteIniValue(f, "INPUT", "GamepadNavigation", v == "1" ? "1" : "0"); return null; },
        },
        new()
        {
            Key = DisplayModeItemKeys.UlOsdKey, Label = "ReLimiter OSD 키", Group = GroupControls, Kind = DisplayModeValueKind.ReLimiterKey,
            Help = "ReLimiter의 화면 표시(OSD)를 보여 주는 키입니다 (relimiter.ini의 osd_toggle_key).",
            Unavailable = NeedReLimiter,
            Read = c => Dlssg20_30Service.ReadIniValue(c.ReLimiterIni, "FrameLimiter", "osd_toggle_key"),
            Write = (c, v) =>
            {
                AuxInstallService.ApplyUlOsdHotkey(c.ReLimiterIni, v);
                foreach (var extra in ReLimiterExtraInis(c)) AuxInstallService.ApplyUlOsdHotkey(extra, v);
                return null;
            },
        },

        // ── When RHI launches the game ────────────────────────────────────────
        new()
        {
            Key = DisplayModeItemKeys.Hdr, Label = "실행 시 Windows HDR", Group = GroupLaunch, Kind = DisplayModeValueKind.Choice, AtLaunch = true,
            Help = "RHI가 게임을 실행할 때 Windows HDR을 켭니다 (게임이 종료되면 다시 끔). 게임 페이지의 HDR 버튼은 현재 모드에 이 값을 설정합니다.",
            Choices = () => OffOn,
        },
        new()
        {
            Key = DisplayModeItemKeys.HdrDisplay, Label = "HDR 디스플레이", Group = GroupLaunch, Kind = DisplayModeValueKind.Choice, AtLaunch = true,
            Help = "HDR을 켤 디스플레이입니다. 예: TV 모드에서는 TV. 설정 = 설정 › 디스플레이에서 선택한 디스플레이.",
            Choices = HdrDisplayChoices,
        },
        new()
        {
            Key = DisplayModeItemKeys.Resolution, Label = "실행 시 해상도", Group = GroupLaunch, Kind = DisplayModeValueKind.Choice, AtLaunch = true,
            Help = "RHI가 게임을 실행할 때 바탕 화면 해상도를 전환합니다 (게임이 종료되면 복원). 켜짐 = 설정 › 디스플레이의 목표 해상도.",
            Choices = ResolutionChoices,
        },
        new()
        {
            Key = DisplayModeItemKeys.LaunchArgs, Label = "실행 인수", Group = GroupLaunch, Kind = DisplayModeValueKind.Text, AtLaunch = true,
            Help = "이 모드에서 RHI가 게임에 전달하는 인수입니다 (게임 재정의의 인수 대신 사용). 예: -fullscreen.",
        },
    };

    private static DisplayModeItem RdxNits(string key, string iniKey, string label, string help) => new()
    {
        Key = key, Label = label, Group = GroupHdr, Kind = DisplayModeValueKind.Number, Unit = "nits", Min = 1, Max = 10000,
        Help = help + $" reshade.ini의 모든 [renodx-preset] 섹션에 {iniKey}(으)로 저장됩니다.",
        Unavailable = NeedRenoDx,
        Read = c => ReadRdx(c.ReShadeIni, iniKey),
        Write = (c, v) => WriteRdx(c.ReShadeIni, iniKey, v),
    };

    // The spelling depends on the mod: Unity / generic RenoDX mods save "toneMapPeakNits", others (e.g. UE-Extended)
    // "ToneMapPeakNits". ReShade's ini is case-sensitive, and RHI's global peak brightness always wrote the capital-T
    // spelling, so a section can hold both: then the lower-case one came from the mod and is read first. Writes go to
    // every spelling present (line by line, nothing else in the file is touched); a section with neither gets both.

    /// <summary>The value from the first [renodx-preset*] section that has it (as a whole number). RenoDX's own spelling wins.</summary>
    internal static string? ReadRdx(string iniPath, string iniKey)
    {
        if (!File.Exists(iniPath)) return null;
        string? exact = null, other = null;
        bool inPreset = false;
        foreach (var raw in File.ReadAllLines(iniPath))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inPreset = line.StartsWith("[renodx-preset", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inPreset || line.StartsWith(';')) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var k = line[..eq].Trim();
            var v = line[(eq + 1)..].Trim();
            if (exact == null && k.Equals(iniKey, StringComparison.Ordinal)) exact = v;
            else if (other == null && k.Equals(iniKey, StringComparison.OrdinalIgnoreCase)) other = v;
            if (exact != null) break;
        }
        var value = exact ?? other;
        if (value == null) return null;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? ((int)Math.Round(d)).ToString(CultureInfo.InvariantCulture) : value;
    }

    /// <summary>
    /// Writes the value to every [renodx-preset*] section (all spellings of the key; RenoDX's spelling is added to a
    /// section that has none, and [renodx-preset1] is created when there's no preset section), like the RenoDX ⚙.
    /// </summary>
    internal static string? WriteRdx(string iniPath, string iniKey, string value)
    {
        if (!File.Exists(iniPath)) return "reshade.ini를 찾을 수 없음";
        var text = File.ReadAllText(iniPath);
        var eol = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        bool endsWithNewline = text.EndsWith('\n');
        if (endsWithNewline && lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);

        int presets = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Trim().StartsWith("[renodx-preset", StringComparison.OrdinalIgnoreCase)) continue;
            presets++;
            int end = i + 1;
            while (end < lines.Count && !lines[end].Trim().StartsWith('[')) end++;
            bool found = false;
            int lastKey = i;
            for (int j = i + 1; j < end; j++)
            {
                var t = lines[j].Trim();
                if (t.Length == 0 || t.StartsWith(';')) continue;
                lastKey = j;
                int eq = t.IndexOf('=');
                if (eq > 0 && t[..eq].Trim().Equals(iniKey, StringComparison.OrdinalIgnoreCase))
                {
                    lines[j] = t[..eq].Trim() + "=" + value;
                    found = true;
                }
            }
            if (!found)
            {
                lines.InsertRange(lastKey + 1, Spellings(iniKey).Select(k => k + "=" + value));
                end += Spellings(iniKey).Count;
            }
            i = end - 1;
        }
        if (presets == 0)
        {
            if (lines.Count > 0 && lines[^1].Trim().Length > 0) lines.Add("");
            lines.Add("[renodx-preset1]");
            lines.AddRange(Spellings(iniKey).Select(k => k + "=" + value));
        }
        File.WriteAllText(iniPath, string.Join(eol, lines) + (endsWithNewline || presets == 0 ? eol : ""));
        return null;
    }

    /// <summary>Both spellings RenoDX mods use for a key: "toneMapPeakNits" and "ToneMapPeakNits".</summary>
    private static IReadOnlyList<string> Spellings(string iniKey)
    {
        var lower = char.ToLowerInvariant(iniKey[0]) + iniKey[1..];
        var upper = char.ToUpperInvariant(iniKey[0]) + iniKey[1..];
        return lower == upper ? new[] { iniKey } : new[] { lower, upper };
    }

    /// <summary>Every reshade*.ini in the game folder (keys go in all of them, like the ReShade ⚙).</summary>
    private static IEnumerable<string> ReShadeInis(DisplayModeContext c)
    {
        if (!Directory.Exists(c.InstallPath)) return Array.Empty<string>();
        return Directory.EnumerateFiles(c.InstallPath, "reshade*.ini")
            .Where(f => Path.GetExtension(f).Equals(".ini", StringComparison.OrdinalIgnoreCase)
                     && Path.GetFileNameWithoutExtension(f).StartsWith("reshade", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>RE Framework games also keep a relimiter.ini in _storage_ (same as the Settings "write to all" button).</summary>
    private static IEnumerable<string> ReLimiterExtraInis(DisplayModeContext c)
    {
        if (string.IsNullOrEmpty(c.InstallPath)) yield break;
        var storage = Path.Combine(c.InstallPath, "_storage_", "relimiter.ini");
        if (File.Exists(storage)) yield return storage;
    }

    // ── Choice lists ──────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<(string, string)> OffOn = new[] { ("꺼짐", "Off"), ("켜짐", "On") };
    private static readonly IReadOnlyList<(string, string)> OffOn01 = new[] { ("꺼짐", "0"), ("켜짐", "1") };

    private static readonly IReadOnlyList<(string, string)> MfgChoices = new[]
    {
        ("꺼짐", "off"), ("2x", "fixed:1"), ("3x", "fixed:2"), ("4x", "fixed:3"), ("5x", "fixed:4"), ("6x", "fixed:5"), ("동적", "dynamic"),
    };

    private static IReadOnlyList<(string, string)> DriverFpsChoices()
    {
        var list = new List<(string, string)> { ("전역 설정 따름", "global"), ("꺼짐", "0") };
        foreach (var fps in new uint[] { 30, 40, 48, 50, 58, 60, 72, 90, 97, 100, 116, 117, 120, 138, 141, 144, 157, 160, 165, 175, 196, 200, 224, 225, 240, 280, 300, 360, 480 })
            list.Add(($"{fps} FPS", fps.ToString(CultureInfo.InvariantCulture)));
        return list;
    }

    private static IReadOnlyList<(string, string)>? _resolutions;
    private static IReadOnlyList<(string, string)> ResolutionChoices()
    {
        if (_resolutions != null) return _resolutions;
        var list = new List<(string, string)> { ("꺼짐", "Off"), ("켜짐 (설정의 목표값)", "On") };
        try
        {
            foreach (var r in ResolutionToggleService.GetSupportedResolutions().OrderByDescending(r => r.Width).ThenByDescending(r => r.Height).ThenByDescending(r => r.RefreshRate))
                list.Add((r.Label, r.Key));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or DllNotFoundException)
        {
            CrashReporter.Log($"[DisplayModeItems.ResolutionChoices] {ex.Message}");
        }
        return _resolutions = list;
    }

    private static IReadOnlyList<(string, string)> HdrDisplayChoices()
    {
        var list = new List<(string, string)> { ("설정", "default") };
        try
        {
            foreach (var d in HdrToggleService.GetAllDisplays().Where(d => d.HdrSupported))
                list.Add((d.Name, d.TargetId.ToString(CultureInfo.InvariantCulture)));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or DllNotFoundException)
        {
            CrashReporter.Log($"[DisplayModeItems.HdrDisplayChoices] {ex.Message}");
        }
        return list;
    }
}
