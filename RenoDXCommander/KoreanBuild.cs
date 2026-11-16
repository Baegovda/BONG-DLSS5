using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace RenoDXCommander;

/// <summary>Helpers for the unofficial Korean-translated build of RHI.</summary>
internal static class KoreanBuild
{
    /// <summary>
    /// Automatic app update checks are off: installing an official update would
    /// replace this translated build with the English release.
    /// </summary>
    public const bool DisableAppAutoUpdate = true;

    // Status texts and button labels stay English inside view models because the
    // app's logic compares them. Tr() turns them into Korean only when they are put
    // on screen; En() maps on-screen text back to the English original so those
    // comparisons keep working.

    private static readonly ConcurrentDictionary<string, string> Reverse = new();

    private static readonly Dictionary<string, string> Exact = new(StringComparer.Ordinal)
    {
        ["Ready"] = "설치 안 됨",
        ["Installed"] = "설치됨",
        ["Update"] = "업데이트",
        ["Update available"] = "업데이트 있음",
        ["Installing"] = "설치 중",
        ["Installing…"] = "설치 중…",
        ["Installing..."] = "설치 중...",
        ["Manual"] = "수동",
        ["Failed"] = "실패",
        ["Install failed"] = "설치 실패",
        ["❌ Failed"] = "❌ 실패",
        ["❌ Deploy failed"] = "❌ 배포 실패",
        ["Download failed — try again"] = "다운로드 실패 — 다시 시도",
        ["Uninstall MFG Unlock first"] = "MFG Unlock 먼저 제거",
        ["Install"] = "설치",
        ["Reinstall"] = "재설치",
        ["Uninstall"] = "제거",
        ["Download"] = "다운로드",
        ["Redownload"] = "다시 다운로드",
        ["Redeploy"] = "다시 배포",
        ["Repair"] = "복구",
        ["Installed via OptiScaler"] = "OptiScaler로 설치됨",
        ["Not installed"] = "설치 안 됨",
        ["WIP"] = "작업 중",
        ["Done"] = "완료",
    };

    private static readonly Dictionary<string, string> Verbs = new(StringComparer.Ordinal)
    {
        ["Install"] = "설치",
        ["Reinstall"] = "재설치",
        ["Update"] = "업데이트",
        ["Uninstall"] = "제거",
        ["Redeploy"] = "다시 배포",
        ["Repair"] = "복구",
        ["Download"] = "다운로드",
    };

    private static readonly Regex Lead = new(@"^([^\p{L}\p{N}]*)(.*?)(\s*)$", RegexOptions.Singleline);
    private static readonly Regex VerbObj = new(@"^(Install|Reinstall|Update|Uninstall|Redeploy|Repair|Download)\s+(.+)$", RegexOptions.Singleline);
    private static readonly Regex DlFrom = new(@"^(Re)?[Dd]ownload from (.+)$", RegexOptions.Singleline);
    private static readonly Regex Required = new(@"^(.+) required$", RegexOptions.Singleline);
    private static readonly Regex InstalledSuffix = new(@"^(.+) installed$", RegexOptions.Singleline);

    /// <summary>English status/button text → Korean for display. Unknown text is returned as is.</summary>
    public static string Tr(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        var m = Lead.Match(text);
        string prefix = m.Groups[1].Value, core = m.Groups[2].Value, tail = m.Groups[3].Value;
        string? ko = TrCore(core);
        if (ko == null) return text;
        var result = prefix + ko + tail;
        Reverse[result] = text;
        return result;
    }

    private static string? TrCore(string core)
    {
        if (core.Length == 0) return null;
        if (Exact.TryGetValue(core, out var e)) return e;
        Match x;
        if ((x = DlFrom.Match(core)).Success)
            return x.Groups[2].Value + (x.Groups[1].Success ? "에서 다시 다운로드" : "에서 다운로드");
        if ((x = VerbObj.Match(core)).Success)
            return x.Groups[2].Value + " " + Verbs[x.Groups[1].Value];
        if ((x = Required.Match(core)).Success)
            return x.Groups[1].Value + " 필요";
        if ((x = InstalledSuffix.Match(core)).Success)
            return x.Groups[1].Value + " 설치됨";
        return null;
    }

    /// <summary>On-screen text → the English text it was translated from (or the text itself).</summary>
    public static string En(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        return Reverse.TryGetValue(text, out var en) ? en : text;
    }

    /// <summary>Main status line (kept English in the view model because code checks it) → Korean for display.</summary>
    public static string Status(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        var s = Regex.Replace(text, @"(\d+) games detected", "게임 $1개 감지됨");
        s = Regex.Replace(s, @"(\d+) ReShade installs", "ReShade 설치 $1개");
        s = s.Replace("offline mode", "오프라인 모드").Replace("Error loading", "불러오기 오류");
        return s;
    }

    private static readonly string[] KoActionWords = { "설치", "재설치", "업데이트", "제거", "다운로드", "다시 다운로드", "다시 배포", "복구", "설치 중", "설치 중…", "설치 중...", "다운로드 중..." };

    /// <summary>
    /// For Korean labels written directly in Korean ("ReShade 재설치", "설치 중..."): the action word at
    /// the end, or null when the label doesn't end with one.
    /// </summary>
    public static string? KoreanActionWord(string text)
    {
        var t = text.Trim();
        foreach (var w in KoActionWords.OrderByDescending(w => w.Length))
            if (t.EndsWith(w, StringComparison.Ordinal)) return w;
        return null;
    }
}
