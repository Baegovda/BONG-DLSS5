using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Services;

/// <summary>
/// Centralizes the three-tier content resolution logic for per-addon Info buttons.
/// Stateless — all data is passed in via method parameters.
///
/// Tier 1: Per-addon manifest dictionary entry for the game name.
/// Tier 2: Wiki-scraped content (RenoDX wiki for RenoDX, OptiScaler wiki for OptiScaler,
///          Luma wiki/manifest for Luma).
/// Tier 3: Static generic fallback text describing the addon.
/// </summary>
public class AddonInfoResolver
{
    // ── Generic fallback text constants (Tier 3) ──────────────────────────────────

    public const string FallbackREFramework =
        "RE Framework는 RE Engine 게임용 모딩 프레임워크입니다. 게임의 렌더링 파이프라인에 후킹하여 ReShade 주입 및 기타 모드를 사용할 수 있게 합니다.";

    public const string FallbackReShade =
        "ReShade는 게임에 시각 효과를 추가하는 후처리 인젝터입니다. RenoDX가 HDR 톤 매핑과 색 보정을 적용하려면 필요합니다.";

    public const string FallbackRenoDX =
        "RenoDX는 ReShade를 사용해 SDR 게임을 HDR로 업그레이드하는 HDR 모드 프레임워크입니다. 게임별 톤 매핑과 색 공간 변환을 제공합니다.";

    public const string FallbackNativeHdr =
        "이 게임은 UE-Extended를 사용하도록 설정되어 있습니다. 네이티브 HDR을 사용하도록 자동 구성되며 설치 시 Engine.ini가 자동으로 배포됩니다. 게임 내 HDR 옵션이 있다면 그것도 켜세요.";

    public const string FallbackReLimiter =
        "ReLimiter는 ReShade와 함께 작동하여 게임의 입력 지연을 줄이고 프레임 페이싱을 개선하는 프레임 제한기입니다.";

    public const string FallbackDisplayCommander =
        "Display Commander는 ReShade와 함께 작동하여 입력 지연을 줄이고 프레임 페이싱을 개선하는 프레임 제한기입니다.";

    public const string FallbackOptiScaler =
        "OptiScaler는 GPU 제조사와 관계없이 FSR, XeSS, DLSS를 사용할 수 있게 하는 업스케일링 호환 레이어로, 하드웨어에 상관없이 원하는 업스케일러를 사용할 수 있습니다.";

    public const string FallbackLuma =
        "Luma는 지원 게임에 네이티브 HDR 출력을 제공하는 대체 HDR 모드 프레임워크로, ReShade 기반 주입이 필요하지 않습니다.";

    /// <summary>
    /// Returns the generic fallback text for the given addon type.
    /// </summary>
    public static string GetFallbackText(AddonType addon) => addon switch
    {
        AddonType.REFramework     => FallbackREFramework,
        AddonType.ReShade         => FallbackReShade,
        AddonType.RenoDX          => FallbackRenoDX,
        AddonType.ReLimiter       => FallbackReLimiter,
        AddonType.DisplayCommander => FallbackDisplayCommander,
        AddonType.OptiScaler      => FallbackOptiScaler,
        AddonType.Luma            => FallbackLuma,
        _ => ""
    };

    /// <summary>
    /// Resolves the info content for a game + addon combination using the three-tier priority system.
    /// </summary>
    public AddonInfoResult Resolve(
        GameCardViewModel card,
        AddonType addon,
        RemoteManifest? manifest,
        OptiScalerWikiData? osWikiData,
        Dictionary<string, string>? hdrDatabase = null,
        Dictionary<string, string>? nexusSummaries = null)
    {
        // ── Tier 1: Per-addon manifest dictionary ─────────────────────────────
        var manifestResult = TryResolveManifest(card.GameName, addon, manifest);
        if (manifestResult != null)
            return AttachExtras(manifestResult, card, addon, hdrDatabase);

        // ── Tier 2: Wiki-scraped content ──────────────────────────────────────
        // Skip wiki content for native HDR games on RenoDX — the wiki notes are
        // technical details that don't apply; the native HDR message is sufficient.
        if (!(addon == AddonType.RenoDX && card.IsNativeHdrGame))
        {
            var wikiResult = TryResolveWiki(card, addon, manifest, osWikiData);
            if (wikiResult != null)
            {
                // Append DB comment (card.Notes) if present — wiki content alone
                // may miss game-specific notes that live only in the RHI database.
                if (addon == AddonType.RenoDX && !string.IsNullOrWhiteSpace(card.Notes))
                {
                    var dbNote = card.Notes
                        .Replace("⚠ In-game HDR must be turned ON for UE-Extended to work correctly in this title.", "")
                        .Trim();
                    if (!string.IsNullOrWhiteSpace(dbNote))
                        wikiResult = new AddonInfoResult
                        {
                            Content = (wikiResult.Content ?? "") + "\n\n" + dbNote,
                            Url = wikiResult.Url,
                            UrlLabel = wikiResult.UrlLabel,
                            Source = wikiResult.Source,
                            WikiStatusLabel = wikiResult.WikiStatusLabel,
                            WikiStatusBadgeBg = wikiResult.WikiStatusBadgeBg,
                            WikiStatusBadgeFg = wikiResult.WikiStatusBadgeFg,
                            WikiStatusBadgeBorder = wikiResult.WikiStatusBadgeBorder,
                            OptiScalerCompat = wikiResult.OptiScalerCompat,
                            OptiScalerFsr4Compat = wikiResult.OptiScalerFsr4Compat,
                            HdrAnalysisUrl = wikiResult.HdrAnalysisUrl,
                        };
                }
                return AttachExtras(wikiResult, card, addon, hdrDatabase);
            }
        }

        // ── Tier 2b: Nexus mod summary (external-only games with Nexus URLs) ──
        if (addon == AddonType.RenoDX && nexusSummaries != null
            && nexusSummaries.TryGetValue(card.GameName, out var nexusSummary)
            && !string.IsNullOrWhiteSpace(nexusSummary))
        {
            return AttachExtras(new AddonInfoResult
            {
                Content = nexusSummary,
                Source = InfoSourceType.Wiki,
            }, card, addon, hdrDatabase);
        }

        // ── Tier 3: Generic fallback ──────────────────────────────────────────
        // Native HDR games using UE-Extended get a specific message instead of generic RenoDX text
        var fallbackText = (addon == AddonType.RenoDX && card.IsNativeHdrGame)
            ? FallbackNativeHdr
            : GetFallbackText(addon);

        // Append DB Comments from card.Notes for all games.
        // For NativeHDR games, card.Notes = "⚠ HDR warning..." + optional "\n\n{dbComment}"
        // — strip the warning to get just the comment portion.
        // For named mods, card.Notes = wiki note + optional "\n\n{dbComment}" (or just dbComment).
        if (addon == AddonType.RenoDX && !string.IsNullOrWhiteSpace(card.Notes))
        {
            var notesWithoutWarning = card.Notes
                .Replace("⚠ In-game HDR must be turned ON for UE-Extended to work correctly in this title.", "")
                .Trim();
            if (!string.IsNullOrWhiteSpace(notesWithoutWarning))
                fallbackText = fallbackText + "\n\n" + notesWithoutWarning;
        }

        // If the only content is generic fallback AND there's an HDR database entry,
        // upgrade the source type to Wiki since we have real per-game content.
        var hdrUrl = LookupHdrUrl(card.GameName, addon, hdrDatabase);
        // Native HDR games always get highlighted (they have per-game relevant info)
        var sourceType = (hdrUrl != null || (addon == AddonType.RenoDX && card.IsNativeHdrGame))
            ? InfoSourceType.Wiki
            : InfoSourceType.Fallback;
        return new AddonInfoResult
        {
            Content = fallbackText,
            Source = sourceType,
            HdrAnalysisUrl = hdrUrl,
            WikiStatusLabel = (sourceType == InfoSourceType.Wiki && addon == AddonType.RenoDX) ? card.WikiStatusLabel : null,
            WikiStatusBadgeBg = (sourceType == InfoSourceType.Wiki && addon == AddonType.RenoDX) ? card.WikiStatusBadgeBackground : null,
            WikiStatusBadgeFg = (sourceType == InfoSourceType.Wiki && addon == AddonType.RenoDX) ? card.WikiStatusBadgeForeground : null,
            WikiStatusBadgeBorder = (sourceType == InfoSourceType.Wiki && addon == AddonType.RenoDX) ? card.WikiStatusBadgeBorderBrush : null,
        };
    }

    /// <summary>
    /// Returns the tooltip text for the Info button based on the resolved source type.
    /// </summary>
    public string GetTooltip(
        GameCardViewModel card,
        AddonType addon,
        RemoteManifest? manifest,
        OptiScalerWikiData? osWikiData,
        Dictionary<string, string>? hdrDatabase = null)
    {
        var source = GetSourceType(card, addon, manifest, osWikiData, hdrDatabase);
        return source switch
        {
            InfoSourceType.Manifest => "게임별 노트 있음",
            InfoSourceType.Wiki     => "위키 정보 있음",
            InfoSourceType.Fallback => "일반 애드온 정보",
            _ => "일반 애드온 정보"
        };
    }

    /// <summary>
    /// Returns the InfoSourceType for the given game + addon combination,
    /// used for styling decisions (highlighted vs muted).
    /// </summary>
    public InfoSourceType GetSourceType(
        GameCardViewModel card,
        AddonType addon,
        RemoteManifest? manifest,
        OptiScalerWikiData? osWikiData,
        Dictionary<string, string>? hdrDatabase = null)
    {
        // Tier 1: manifest
        if (HasManifestEntry(card.GameName, addon, manifest))
            return InfoSourceType.Manifest;

        // Tier 2: wiki
        if (HasWikiContent(card, addon, manifest, osWikiData))
            return InfoSourceType.Wiki;

        // Check HDR database — if present for RenoDX/Luma, upgrade to Wiki
        if (LookupHdrUrl(card.GameName, addon, hdrDatabase) != null)
            return InfoSourceType.Wiki;

        // Native HDR games using UE-Extended get highlighted (per-game relevant info)
        if (addon == AddonType.RenoDX && card.IsNativeHdrGame)
            return InfoSourceType.Wiki;

        // Tier 3: fallback
        return InfoSourceType.Fallback;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the per-addon manifest dictionary for the given addon type, or null.
    /// </summary>
    private static Dictionary<string, GameNoteEntry>? GetManifestDict(AddonType addon, RemoteManifest? manifest)
    {
        if (manifest == null) return null;
        return addon switch
        {
            AddonType.REFramework      => manifest.ReframeworkGameInfo,
            AddonType.ReShade          => manifest.ReshadeGameInfo,
            AddonType.RenoDX           => manifest.GameNotes,
            AddonType.ReLimiter        => manifest.RelimiterGameInfo,
            AddonType.DisplayCommander => manifest.DisplayCommanderGameInfo,
            AddonType.OptiScaler       => manifest.OptiScalerGameInfo,
            AddonType.Luma             => manifest.LumaGameInfo,
            _ => null
        };
    }

    /// <summary>
    /// Checks whether a per-addon manifest entry exists for the game.
    /// </summary>
    private static bool HasManifestEntry(string gameName, AddonType addon, RemoteManifest? manifest)
    {
        var dict = GetManifestDict(addon, manifest);
        return dict != null
            && dict.TryGetValue(gameName, out var entry)
            && !string.IsNullOrWhiteSpace(entry.Notes);
    }

    /// <summary>
    /// Attempts to resolve content from the per-addon manifest dictionary (Tier 1).
    /// </summary>
    private static AddonInfoResult? TryResolveManifest(string gameName, AddonType addon, RemoteManifest? manifest)
    {
        var dict = GetManifestDict(addon, manifest);
        if (dict == null) return null;

        if (!dict.TryGetValue(gameName, out var entry)) return null;
        if (string.IsNullOrWhiteSpace(entry.Notes)) return null;

        return new AddonInfoResult
        {
            Content = entry.Notes,
            Url = entry.NotesUrl,
            UrlLabel = entry.NotesUrlLabel,
            Source = InfoSourceType.Manifest
        };
    }

    /// <summary>
    /// Checks whether wiki content is available for the game + addon combination.
    /// </summary>
    private static bool HasWikiContent(
        GameCardViewModel card,
        AddonType addon,
        RemoteManifest? manifest,
        OptiScalerWikiData? osWikiData)
    {
        return addon switch
        {
            AddonType.RenoDX    => HasRenoDXWikiContent(card),
            AddonType.OptiScaler => HasOptiScalerWikiContent(card.GameName, manifest, osWikiData),
            AddonType.Luma      => HasLumaWikiContent(card, manifest),
            _ => false
        };
    }

    /// <summary>
    /// Attempts to resolve content from wiki sources (Tier 2).
    /// </summary>
    private static AddonInfoResult? TryResolveWiki(
        GameCardViewModel card,
        AddonType addon,
        RemoteManifest? manifest,
        OptiScalerWikiData? osWikiData)
    {
        return addon switch
        {
            AddonType.RenoDX    => TryResolveRenoDXWiki(card),
            AddonType.OptiScaler => TryResolveOptiScalerWiki(card.GameName, manifest, osWikiData),
            AddonType.Luma      => TryResolveLumaWiki(card, manifest),
            _ => null
        };
    }

    // ── RenoDX wiki (Tier 2a) ─────────────────────────────────────────────────

    private static bool HasRenoDXWikiContent(GameCardViewModel card)
    {
        // RenoDX wiki data is stored on the card via the Mod property (wiki-matched mod)
        if (card.Mod != null && !string.IsNullOrWhiteSpace(card.Mod.Notes))
            return true;
        // Also check the NameUrl (wiki page link)
        if (!string.IsNullOrEmpty(card.NameUrl))
            return true;
        // Also check if we have a valid wiki status (not the default "—")
        if (!string.IsNullOrEmpty(card.WikiStatus) && card.WikiStatus != "—")
            return true;
        return false;
    }

    private static AddonInfoResult? TryResolveRenoDXWiki(GameCardViewModel card)
    {
        if (!HasRenoDXWikiContent(card)) return null;

        var content = card.Mod?.Notes ?? "";
        // If no notes text, use the fallback description so the dialog isn't empty
        if (string.IsNullOrWhiteSpace(content))
            content = FallbackRenoDX;

        return new AddonInfoResult
        {
            Content = content,
            Url = card.NameUrl,
            UrlLabel = !string.IsNullOrEmpty(card.NameUrl) ? "위키 페이지 보기" : null,
            Source = InfoSourceType.Wiki,
            WikiStatusLabel = card.WikiStatusLabel,
            WikiStatusBadgeBg = card.WikiStatusBadgeBackground,
            WikiStatusBadgeFg = card.WikiStatusBadgeForeground,
            WikiStatusBadgeBorder = card.WikiStatusBadgeBorderBrush
        };
    }

    // ── OptiScaler wiki (Tier 2b) ─────────────────────────────────────────────

    private static bool HasOptiScalerWikiContent(
        string gameName,
        RemoteManifest? manifest,
        OptiScalerWikiData? osWikiData)
    {
        if (osWikiData == null) return false;
        var wikiName = ResolveOptiScalerWikiName(gameName, manifest);
        return osWikiData.StandardCompat.ContainsKey(wikiName)
            || osWikiData.Fsr4Compat.ContainsKey(wikiName);
    }

    private static AddonInfoResult? TryResolveOptiScalerWiki(
        string gameName,
        RemoteManifest? manifest,
        OptiScalerWikiData? osWikiData)
    {
        if (osWikiData == null) return null;
        var wikiName = ResolveOptiScalerWikiName(gameName, manifest);

        osWikiData.StandardCompat.TryGetValue(wikiName, out var stdEntry);
        osWikiData.Fsr4Compat.TryGetValue(wikiName, out var fsr4Entry);

        if (stdEntry == null && fsr4Entry == null) return null;

        // Build content from available entries
        var content = FormatOptiScalerContent(stdEntry, fsr4Entry);
        var url = stdEntry?.DetailPageUrl ?? fsr4Entry?.DetailPageUrl;

        return new AddonInfoResult
        {
            Content = content,
            Url = url,
            UrlLabel = url != null ? "위키 페이지 보기" : null,
            Source = InfoSourceType.Wiki,
            OptiScalerCompat = stdEntry,
            OptiScalerFsr4Compat = fsr4Entry
        };
    }

    /// <summary>
    /// Resolves the wiki name for OptiScaler lookup, using manifest overrides if available.
    /// </summary>
    internal static string ResolveOptiScalerWikiName(string gameName, RemoteManifest? manifest)
    {
        if (manifest?.OptiScalerWikiNames != null
            && manifest.OptiScalerWikiNames.TryGetValue(gameName, out var wikiName)
            && !string.IsNullOrWhiteSpace(wikiName))
        {
            return wikiName;
        }
        // Strip trademark symbols so "Borderlands® 4" matches wiki "Borderlands 4"
        return StripTrademarks(gameName);
    }

    /// <summary>
    /// Strips ™ ® © symbols from a game name so Steam names match wiki names.
    /// </summary>
    private static string StripTrademarks(string name) =>
        name.Replace("™", "").Replace("®", "").Replace("©", "").Trim();

    private static string FormatOptiScalerContent(
        OptiScalerCompatEntry? stdEntry,
        OptiScalerCompatEntry? fsr4Entry)
    {
        var parts = new List<string>();

        if (stdEntry != null)
        {
            var upscalers = stdEntry.Upscalers.Count > 0
                ? string.Join(", ", stdEntry.Upscalers)
                : "목록 없음";
            parts.Add($"OptiScaler 호환성: {stdEntry.Status}\n업스케일러: {upscalers}");
            if (!string.IsNullOrWhiteSpace(stdEntry.Notes))
                parts.Add(stdEntry.Notes);
        }

        if (fsr4Entry != null)
        {
            var upscalers = fsr4Entry.Upscalers.Count > 0
                ? string.Join(", ", fsr4Entry.Upscalers)
                : "목록 없음";
            parts.Add($"FSR4 호환성: {fsr4Entry.Status}\n업스케일러: {upscalers}");
            if (!string.IsNullOrWhiteSpace(fsr4Entry.Notes))
                parts.Add(fsr4Entry.Notes);
        }

        return string.Join("\n\n", parts);
    }

    // ── Luma wiki (Tier 2c) ───────────────────────────────────────────────────

    private static bool HasLumaWikiContent(GameCardViewModel card, RemoteManifest? manifest)
    {
        // Check LumaMod wiki data on the card
        if (card.LumaMod != null &&
            (!string.IsNullOrWhiteSpace(card.LumaMod.SpecialNotes) ||
             !string.IsNullOrWhiteSpace(card.LumaMod.FeatureNotes)))
            return true;

        // Check lumaGameNotes manifest section
        if (manifest?.LumaGameNotes != null
            && manifest.LumaGameNotes.TryGetValue(card.GameName, out var lumaNote)
            && !string.IsNullOrWhiteSpace(lumaNote.Notes))
            return true;

        return false;
    }

    private static AddonInfoResult? TryResolveLumaWiki(GameCardViewModel card, RemoteManifest? manifest)
    {
        if (!HasLumaWikiContent(card, manifest)) return null;

        var parts = new List<string>();
        string? url = null;
        string? urlLabel = null;

        // LumaMod wiki data
        if (card.LumaMod != null)
        {
            if (!string.IsNullOrWhiteSpace(card.LumaMod.SpecialNotes))
                parts.Add(card.LumaMod.SpecialNotes);
            if (!string.IsNullOrWhiteSpace(card.LumaMod.FeatureNotes))
                parts.Add(card.LumaMod.FeatureNotes);
        }

        // lumaGameNotes manifest section
        if (manifest?.LumaGameNotes != null
            && manifest.LumaGameNotes.TryGetValue(card.GameName, out var lumaNote)
            && !string.IsNullOrWhiteSpace(lumaNote.Notes))
        {
            parts.Add(lumaNote.Notes);
            url = lumaNote.NotesUrl;
            urlLabel = lumaNote.NotesUrlLabel;
        }

        if (parts.Count == 0) return null;

        return new AddonInfoResult
        {
            Content = string.Join("\n\n", parts),
            Url = url,
            UrlLabel = urlLabel,
            Source = InfoSourceType.Wiki
        };
    }

    // ── HDR Gaming Database helpers ───────────────────────────────────────────

    /// <summary>
    /// Looks up the game name in the HDR Gaming Database, but only for RenoDX and Luma addon types.
    /// Returns the discussion URL if found, null otherwise.
    /// </summary>
    private static string? LookupHdrUrl(string gameName, AddonType addon, Dictionary<string, string>? hdrDatabase)
    {
        if (hdrDatabase == null || hdrDatabase.Count == 0)
            return null;

        // Only RenoDX and Luma get HDR database links
        if (addon is not (AddonType.RenoDX or AddonType.Luma))
            return null;

        var strippedName = StripTrademarks(gameName);
        hdrDatabase.TryGetValue(strippedName, out var url);
        return url;
    }

    /// <summary>
    /// Attaches the HDR analysis URL and prepends the native HDR message to an existing result.
    /// </summary>
    private static AddonInfoResult AttachExtras(
        AddonInfoResult result,
        GameCardViewModel card,
        AddonType addon,
        Dictionary<string, string>? hdrDatabase)
    {
        var hdrUrl = LookupHdrUrl(card.GameName, addon, hdrDatabase);
        if (hdrUrl == null) return result;

        return new AddonInfoResult
        {
            Content = result.Content,
            Url = result.Url,
            UrlLabel = result.UrlLabel,
            Source = result.Source,
            WikiStatusLabel = result.WikiStatusLabel,
            WikiStatusBadgeBg = result.WikiStatusBadgeBg,
            WikiStatusBadgeFg = result.WikiStatusBadgeFg,
            WikiStatusBadgeBorder = result.WikiStatusBadgeBorder,
            OptiScalerCompat = result.OptiScalerCompat,
            OptiScalerFsr4Compat = result.OptiScalerFsr4Compat,
            HdrAnalysisUrl = hdrUrl
        };
    }
}
