// ShaderPackService.cs — Class declaration, constructor, path constants, pack definitions, enums, and ShaderPack record

using RenoDXCommander.Models;

namespace RenoDXCommander.Services;

/// <summary>
/// Downloads, extracts and deploys HDR ReShade shader packs from multiple sources.
///
/// All packs are merged into a single shared staging tree:
///   %LocalAppData%\RenoDXCommander\reshade\Shaders\
///   %LocalAppData%\RenoDXCommander\reshade\Textures\
///
/// Each pack's extracted files are tracked individually. If a pack's cache zip is
/// deleted — or extracted files are missing from the staging folder — the pack is
/// re-downloaded and re-extracted on the next launch.
///
/// Source types:
///   GhRelease — GitHub Releases API, picks first matching asset extension
///   DirectUrl — Any static URL; versioned by ETag / Last-Modified header
/// </summary>
public partial class ShaderPackService : IShaderPackService
{
    private readonly HttpClient _http;
    private readonly GitHubETagCache _etagCache;

    public ShaderPackService(HttpClient http, GitHubETagCache etagCache)
    {
        _http = http;
        _etagCache = etagCache;
    }
    // ── Public path constants (used by AuxInstallService) ─────────────────────────
    public static readonly string ShadersDir = Path.Combine(AuxInstallService.RsStagingDir, "Shaders");
    public static readonly string TexturesDir = Path.Combine(AuxInstallService.RsStagingDir, "Textures");

    // User-defined custom shaders — placed by the user, never auto-downloaded
    public const string CustomShaderSentinel = "__custom__";
    /// <summary>Virtual pack ID for individual custom shader file selection in the picker.</summary>
    public const string CustomFilePackId = "__custom_files__";
    public static readonly string CustomDir = Path.Combine(AuxInstallService.RsStagingDir, "Custom");
    public static readonly string CustomShadersDir = Path.Combine(CustomDir, "Shaders");
    public static readonly string CustomTexturesDir = Path.Combine(CustomDir, "Textures");

    /// <summary>
    /// Returns all shader and texture files from the Custom folder as relative paths
    /// suitable for display in the picker (e.g. "Shaders/MyShader.fx", "Textures/LUT.png").
    /// Returns an empty list if the Custom folder doesn't exist or is empty.
    /// </summary>
    public static IReadOnlyList<string> GetCustomPackFiles()
    {
        var files = new List<string>();
        try
        {
            if (Directory.Exists(CustomShadersDir))
                foreach (var f in Directory.EnumerateFiles(CustomShadersDir, "*", SearchOption.AllDirectories))
                    files.Add(Path.Combine("Shaders", Path.GetRelativePath(CustomShadersDir, f)));
            if (Directory.Exists(CustomTexturesDir))
                foreach (var f in Directory.EnumerateFiles(CustomTexturesDir, "*", SearchOption.AllDirectories))
                    files.Add(Path.Combine("Textures", Path.GetRelativePath(CustomTexturesDir, f)));
        }
        catch (Exception ex) { CrashReporter.Log($"[ShaderPackService.GetCustomPackFiles] Failed — {ex.Message}"); }
        return files;
    }

    /// <summary>Returns true when the Custom shader folder contains at least one file.</summary>
    public static bool CustomPackHasFiles() => GetCustomPackFiles().Count > 0;

    public const string GameReShadeShaders = "reshade-shaders";
    public const string GameReShadeOriginal = "reshade-shaders-original";
    internal const string ManagedMarkerFileName = "Managed by RDXC.txt";
    private const string ManagedMarkerContent = "This folder is managed by RenoDXCommander. Do not edit manually.\n"
                                                  + "Deleting this file will cause RDXC to treat the folder as user-managed.";

    // ── Pack definitions ──────────────────────────────────────────────────────────

    private enum SourceKind { GhRelease, DirectUrl }

    /// <summary>
    /// UI grouping for the shader picker dialog.
    /// Essential — always deployed, shown at the top.
    /// Recommended — suggested packs shown in the second group.
    /// Custom — user-placed files, shown between Recommended and Extra.
    /// Extra — everything else.
    /// </summary>
    public enum PackCategory { Essential, Recommended, Custom, Extra }

    /// <summary>
    /// Shader files that fail to compile and should never be extracted or deployed.
    /// Matched against the filename (leaf) of each archive entry, case-insensitive.
    /// </summary>
    private static readonly HashSet<string> ExcludedShaderFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "BX_XIV_ChromakeyPlus.fx",
        "GrainSpread.fx",
        "NTSCCustom.fx",
        "NTSC_XOT.fx",
    };

    private record ShaderPack(
        string Id,           // unique key — used in settings.json and cache filenames
        string DisplayName,  // shown in progress messages and logs
        SourceKind Kind,
        string Url,          // API url (GhRelease) or direct download url (DirectUrl)
        bool IsMinimum,    // true for packs included in the Lilium/minimum set
        string? AssetExt = null,  // GhRelease: required file extension of the release asset
        string? Description = null, // short description shown in the shader picker dialog
        PackCategory Category = PackCategory.Extra, // UI grouping
        string[]? Requires = null // IDs of packs that must also be selected when this pack is enabled
    );

    // Packs in order of download. IsMinimum=true → included in Minimum mode.
    private static readonly ShaderPack[] DefaultPacks =
    {
        new(
            Id          : "Lilium",
            DisplayName : "Lilium HDR Shaders",
            Kind        : SourceKind.GhRelease,
            Url         : "https://api.github.com/repos/EndlesslyFlowering/ReShade_HDR_shaders/releases/latest",
            IsMinimum   : true,
            AssetExt    : ".7z",
            Description : "HDR 톤 매핑 및 역 톤 매핑 셰이더",
            Category    : PackCategory.Essential
        ),
        new(
            Id          : "CrosireMaster",
            DisplayName : "crosire reshade-shaders (master)",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/crosire/reshade-shaders/archive/refs/heads/master.zip",
            IsMinimum   : true,
            Description : "공식 ReShade 표준 효과 — master 브랜치 전체",
            Category    : PackCategory.Recommended
        ),
        new(
            Id          : "CrosireLegacy",
            DisplayName : "crosire reshade-shaders (legacy)",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/crosire/reshade-shaders/archive/refs/heads/legacy.zip",
            IsMinimum   : false,
            Description : "레거시 ReShade 효과 (master에서 제거된 이전 버전)",
            Category    : PackCategory.Extra
        ),
        new(
            Id          : "PumboAutoHDR",
            DisplayName : "PumboAutoHDR",
            Kind        : SourceKind.GhRelease,
            Url         : "https://api.github.com/repos/Filoppi/PumboAutoHDR/releases/latest",
            IsMinimum   : false,
            AssetExt    : ".zip",
            Description : "SDR 게임용 자동 HDR 변환",
            Category    : PackCategory.Recommended
        ),
        new(
            Id          : "SmolbbsoopShaders",
            DisplayName : "smolbbsoop shaders",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/smolbbsoop/smolbbsoopshaders/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "HDR 유틸리티 셰이더 및 효과",
            Category    : PackCategory.Extra
        ),
        new(
            Id          : "MaxG2DSimpleHDR",
            DisplayName : "MaxG2D Simple HDR Shaders",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/MaxG2D/ReshadeSimpleHDRShaders/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "간단한 HDR 블룸, 렌즈 플레어, 톤 매핑",
            Category    : PackCategory.Recommended
        ),
        new(
            Id          : "ClshortfuseShaders",
            DisplayName : "clshortfuse ReShade shaders",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/clshortfuse/reshade-shaders/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "RenoDX용 HDR 및 색 보정 셰이더",
            Category    : PackCategory.Recommended
        ),
        new(
            Id          : "PotatoFX",
            DisplayName : "potatoFX (CreepySasquatch)",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/CreepySasquatch/potatoFX/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "저사양 하드웨어용 경량 후처리 효과",
            Category    : PackCategory.Extra
        ),
        new(
            Id          : "Azen",
            DisplayName : "Azen by Zenteon",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/Zenteon/Azen/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "Zenteon의 캐주얼 셰이더 모음 — 실험적 효과",
            Requires    : new[] { "SmolbbsoopShaders" }
        ),
        new(
            Id          : "SweetFX",
            DisplayName : "SweetFX by CeeJay.dk",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/CeeJayDK/SweetFX/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "클래식 색 보정, 샤프닝, 블룸 효과"
        ),
        new(
            Id          : "OtisFX",
            DisplayName : "OtisFX by Otis_Inf",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/FransBouma/OtisFX/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "시네마틱 피사계 심도, 빛줄기, 카메라 효과"
        ),
        new(
            Id          : "Depth3D",
            DisplayName : "Depth3D by BlueSkyDefender",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/BlueSkyDefender/Depth3D/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "입체 3D 및 깊이 기반 시각 효과"
        ),
        new(
            Id          : "DaodanShaders",
            DisplayName : "reshade-shaders by Daodan",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/Daodan317081/reshade-shaders/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "만화, 크로스해치, 예술적 스타일 효과"
        ),
        new(
            Id          : "BrussellShaders",
            DisplayName : "Shaders by brussell",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/brussell1/Shaders/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "하프톤, 스케치, 스타일화 렌더링 효과"
        ),
        new(
            Id          : "FubaxShaders",
            DisplayName : "fubax-shaders by Fubaxiusz",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/Fubaxiusz/fubax-shaders/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "VR 친화적 렌즈 왜곡 및 색수차"
        ),
        new(
            Id          : "qUINT",
            DisplayName : "qUINT by Marty McFly",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/martymcmodding/qUINT/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "MXAO, ADOF, 라이트룸, 화면 공간 반사"
        ),
        new(
            Id          : "AlucardDH",
            DisplayName : "dh-reshade-shaders by AlucardDH",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/AlucardDH/dh-reshade-shaders/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "앰비언트 오클루전, 디더 제거, 색상 향상"
        ),
        new(
            Id          : "WarpFX",
            DisplayName : "Warp-FX by Radegast",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/Radegast-FFXIV/Warp-FX/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "화면 워프, 소용돌이, 왜곡 효과"
        ),
        new(
            Id          : "Prod80",
            DisplayName : "Color effects by prod80",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/prod80/prod80-ReShade-Repository/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "전문가용 색 보정, 커브, 톤 도구"
        ),
        new(
            Id          : "CorgiFX",
            DisplayName : "CorgiFX by originalnicodr",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/originalnicodr/CorgiFX/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "스크린샷 및 가상 사진 도구"
        ),
        new(
            Id          : "InsaneShaders",
            DisplayName : "Insane-Shaders by Lord of Lunacy",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/LordOfLunacy/Insane-Shaders/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "고급 디더링, 안개 제거, 윤곽선 검출"
        ),
        new(
            Id          : "CobraFX",
            DisplayName : "CobraFX by SirCobra",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/LordKobra/CobraFX/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "중력, 자동 초점, 실시간 레이 트레이싱 효과"
        ),
        new(
            Id          : "AstrayFX",
            DisplayName : "AstrayFX by BlueSkyDefender",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/BlueSkyDefender/AstrayFX/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "깊이 기반 안개, 연무, 대기 효과"
        ),
        new(
            Id          : "CRTRoyale",
            DisplayName : "CRT-Royale-ReShade by akgunter",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/akgunter/crt-royale-reshade/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "형광체 및 주사선 에뮬레이션을 포함한 CRT 모니터 시뮬레이션"
        ),
        new(
            Id          : "RSRetroArch",
            DisplayName : "RSRetroArch by Matsilagi",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/Matsilagi/RSRetroArch/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "RetroArch 셰이더 이식판 — CRT, LCD, 레트로 필터"
        ),
        new(
            Id          : "VRToolkit",
            DisplayName : "VRToolkit by retroluxfilm",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/retroluxfilm/reshade-vrtoolkit/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "VR 헤드셋에 최적화된 샤프닝 및 선명도 도구"
        ),
        new(
            Id          : "FGFX",
            DisplayName : "FGFX by AlexTuduran",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/AlexTuduran/FGFX/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "필름 그레인, 멀티 LUT, 시네마틱 후처리"
        ),
        new(
            Id          : "CShade",
            DisplayName : "CShade by papadanku",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/papadanku/CShade/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "옵티컬 플로우, 모션 블러, 컨볼루션 효과"
        ),
        new(
            Id          : "iMMERSE",
            DisplayName : "iMMERSE by Marty McFly",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/martymcmodding/iMMERSE/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "차세대 RTGI, MXAO, 안티앨리어싱 모음"
        ),
        new(
            Id          : "VortShaders",
            DisplayName : "vort_Shaders by vortigern11",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/vortigern11/vort_Shaders/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "샤프닝, 색 보정, 깊이 효과"
        ),
        new(
            Id          : "BXShade",
            DisplayName : "BX-Shade by BarricadeMKXX",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/liuxd17thu/BX-Shade/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "블룸, 노출, 색상 향상 효과"
        ),
        new(
            Id          : "SHADERDECK",
            DisplayName : "SHADERDECK by TreyM",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/IAmTreyM/SHADERDECK/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "엄선된 색상 및 조명 효과 모음"
        ),
        new(
            Id          : "METEOR",
            DisplayName : "METEOR by Marty McFly",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/martymcmodding/METEOR/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "고급 디노이저 및 이미지 재구성"
        ),
        new(
            Id          : "AnnReShade",
            DisplayName : "Ann-ReShade by Anastasia Bouwsma",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/AnastasiaGals/Ann-ReShade/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "부드러운 블룸, 색 보정, 주변광 프리셋"
        ),
        new(
            Id          : "ZenteonFX",
            DisplayName : "ZenteonFX Shaders by Zenteon",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/Zenteon/ZenteonFX/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "전역 조명, SSR, 패스 트레이싱 효과"
        ),
        new(
            Id          : "GShadeShaders",
            DisplayName : "GShade-Shaders by Marot",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/Mortalitas/GShade-Shaders/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "GShade의 대규모 커뮤니티 셰이더 모음"
        ),
        new(
            Id          : "PthoFX",
            DisplayName : "Ptho-FX by PthoEastCoast",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/PthoEastCoast/Ptho-FX/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "시네마틱 색 보정 및 필름 에뮬레이션"
        ),
        new(
            Id          : "Anagrama",
            DisplayName : "The Anagrama Collection by nullfractal",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/nullfrctl/reshade-shaders/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "예술적이고 실험적인 시각 효과"
        ),
        new(
            Id          : "BarbatosShaders",
            DisplayName : "reshade-shaders by Barbatos",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/BarbatosBachiko/Reshade-Shaders/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "앰비언트 오클루전, 블룸, 색상 효과"
        ),
        new(
            Id          : "BFBFX",
            DisplayName : "BFBFX by yaboi BFB",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/yplebedev/BFBFX/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "스타일화된 예술적 후처리 효과"
        ),
        new(
            Id          : "Rendepth",
            DisplayName : "Rendepth by cybereality",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/outmode/rendepth-reshade/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "깊이 기반 3D 렌더링 및 스테레오 효과"
        ),
        new(
            Id          : "CropAndResize",
            DisplayName : "Crop and Resize by P0NYSLAYSTATION",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/P0NYSLAYSTATION/Scaling-Shaders/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "화면 자르기, 크기 조정, 화면비 도구"
        ),
        new(
            Id          : "FXShaders",
            DisplayName : "FXShaders by luluco250",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/luluco250/FXShaders/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "블룸, 그레인, 디더링 및 유틸리티 셰이더 라이브러리"
        ),
        new(
            Id          : "LumeniteFX",
            DisplayName : "LumeniteFX by Kaido",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/umar-afzaal/LumeniteFX/archive/refs/heads/mainline.zip",
            IsMinimum   : false,
            Description : "조명, 블룸, 대기 글로우 효과"
        ),
        new(
            Id          : "NNShaders",
            DisplayName : "NN-Shaders by Sarenya",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/Sarenya/NN-Shaders/archive/refs/heads/master.zip",
            IsMinimum   : false,
            Description : "신경망 기반 이미지 처리 셰이더"
        ),
        new(
            Id          : "QdOledAplFixer",
            DisplayName : "QD-OLED APL Fixer by mspeedo",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/mspeedo/QD-OLED-APL-FIXER/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "QD-OLED ABL 어두워짐을 보정하는 HDR 밝기 증폭"
        ),
        new(
            Id          : "GlamaryeFX",
            DisplayName : "Glamarye Fast Effects by rj200",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/rj200/Glamarye_Fast_Effects_for_ReShade/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "경량 올인원: 샤프닝, AO, 간접 조명, 색 보정을 한 번의 패스로"
        ),
        new(
            Id          : "LumaBoost",
            DisplayName : "LumaBoost by Valadore",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/Valadore/LumaBoost/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "OLED ABL 보정 — 자동 밝기 제한을 상쇄하도록 중간톤을 동적으로 끌어올림"
        ),
        new(
            Id          : "DLSS5Feeder",
            DisplayName : "DLSS5 Feeder shader",
            Kind        : SourceKind.DirectUrl,
            Url         : "",   // no URL — seeded from the Feeder addon zip by RHI, never downloaded
            IsMinimum   : false,
            Description : "DLSS5_Feed.fx — seeded from the Feeder addon zip by RHI. Not shown in the shader picker.",
            Category    : PackCategory.Extra
        ),
        new(
            Id          : "RenoFXHDRToolkit",
            DisplayName : "RenoFX HDR Toolkit by OopyDoopy",
            Kind        : SourceKind.DirectUrl,
            Url         : "https://github.com/clshortfuse/renofx/archive/refs/heads/main.zip",
            IsMinimum   : false,
            Description : "RenoDX 모드가 없는 게임용 SDR→HDR 변환, 톤 매핑, 색 보정",
            Category    : PackCategory.Recommended
        ),
    };

    // Active packs — starts as DefaultPacks, may be overridden by manifest
    private ShaderPack[] _packs = DefaultPacks;

    // ── AvailablePacks ───────────────────────────────────────────────────────────

    /// <summary>
    /// Exposes pack metadata for the picker UI — returns every known pack's Id and DisplayName.
    /// </summary>
    public IReadOnlyList<(string Id, string DisplayName, PackCategory Category)> AvailablePacks { get; private set; } =
        DefaultPacks.OrderBy(p => p.Category).ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
             .Select(p => (p.Id, p.DisplayName, p.Category)).ToList().AsReadOnly();

    /// <summary>
    /// Returns the short description for a pack, or null if none is set.
    /// </summary>
    public string? GetPackDescription(string packId) =>
        _packs.FirstOrDefault(p => p.Id == packId)?.Description;

    /// <summary>
    /// Returns the IDs of packs that the given pack requires (dependencies).
    /// </summary>
    public string[] GetRequiredPacks(string packId) =>
        _packs.FirstOrDefault(p => p.Id == packId)?.Requires ?? Array.Empty<string>();

    // ── Manifest overrides ────────────────────────────────────────────────────────

    /// <summary>
    /// Merges remote manifest shader pack overrides into the active pack list.
    /// Starts from DefaultPacks, applies additions/modifications/removals, then
    /// rebuilds the AvailablePacks property.
    /// </summary>
    public void ApplyManifestOverrides(RemoteManifest? manifest)
    {
        if (manifest?.ShaderPacks == null || manifest.ShaderPacks.Count == 0)
        {
            _packs = DefaultPacks;
            RebuildAvailablePacks();
            return;
        }

        var merged = new List<ShaderPack>(DefaultPacks);

        foreach (var (id, entry) in manifest.ShaderPacks)
        {
            // disabled → remove from list
            if (entry.Disabled == true)
            {
                merged.RemoveAll(p => p.Id == id);
                continue;
            }

            var existingIdx = merged.FindIndex(p => p.Id == id);
            if (existingIdx >= 0)
            {
                // Override non-null fields on existing pack
                var existing = merged[existingIdx];
                merged[existingIdx] = existing with
                {
                    DisplayName = entry.DisplayName ?? existing.DisplayName,
                    Kind        = TryParseKind(entry.Kind) ?? existing.Kind,
                    Url         = entry.Url ?? existing.Url,
                    IsMinimum   = entry.IsMinimum ?? existing.IsMinimum,
                    AssetExt    = entry.AssetExt ?? existing.AssetExt,
                    Description = entry.Description ?? existing.Description,
                    Category    = TryParseCategory(entry.Category) ?? existing.Category,
                    Requires    = entry.Requires ?? existing.Requires,
                };
            }
            else
            {
                // New pack from manifest — requires at minimum a URL and kind
                if (string.IsNullOrEmpty(entry.Url) || string.IsNullOrEmpty(entry.Kind))
                    continue;

                var kind = TryParseKind(entry.Kind);
                if (kind == null) continue;

                merged.Add(new ShaderPack(
                    Id          : id,
                    DisplayName : entry.DisplayName ?? id,
                    Kind        : kind.Value,
                    Url         : entry.Url,
                    IsMinimum   : entry.IsMinimum ?? false,
                    AssetExt    : entry.AssetExt,
                    Description : entry.Description,
                    Category    : TryParseCategory(entry.Category) ?? PackCategory.Extra,
                    Requires    : entry.Requires
                ));
            }
        }

        _packs = merged.ToArray();
        RebuildAvailablePacks();
    }

    private void RebuildAvailablePacks()
    {
        AvailablePacks = _packs
            .OrderBy(p => p.Category)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(p => (p.Id, p.DisplayName, p.Category))
            .ToList()
            .AsReadOnly();
    }

    private static SourceKind? TryParseKind(string? value) =>
        value switch
        {
            "GhRelease" => SourceKind.GhRelease,
            "DirectUrl" => SourceKind.DirectUrl,
            _ => null
        };

    private static PackCategory? TryParseCategory(string? value) =>
        value switch
        {
            "Essential"   => PackCategory.Essential,
            "Recommended" => PackCategory.Recommended,
            "Extra"       => PackCategory.Extra,
            _ => null
        };
}

