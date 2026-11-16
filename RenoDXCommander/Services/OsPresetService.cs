using System.Text.Json;
using RenoDXCommander.Models;

namespace RenoDXCommander.Services;

/// <summary>
/// OptiScaler presets: the 4 global user slots (local app data) and the ready-made presets
/// from optiscaler_presets.json on GitHub (cached; built-in copies when it can't be fetched).
/// </summary>
public static class OsPresetService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RHI", "os_presets.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Loads the preset array from disk. Always returns exactly 4 entries (nulls for empty slots).
    /// If the file does not exist, returns a default array with slot 0 pre-populated as "DLSS Enabler".
    /// </summary>
    public static OsPreset?[] Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<OsPreset?[]>(json);
                if (loaded != null)
                {
                    // Ensure exactly 4 slots
                    var result = new OsPreset?[4];
                    for (int i = 0; i < 4 && i < loaded.Length; i++)
                        result[i] = loaded[i];
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[OsPresetService.Load] {ex.Message}");
        }

        // First-run default: slot 0 = DLSS Enabler
        return new OsPreset?[]
        {
            new OsPreset
            {
                Name               = "DLSS Enabler",
                FgInput            = "upscaler",
                FgOutput           = "dlssg",
                FgNvngxReplacement = "Arturs",
                DeployStreamline   = true,
                DeployDlssEnabler  = true,
            },
            null,
            null,
            null,
        };
    }

    /// <summary>
    /// Saves all 4 preset slots to disk. Creates the directory if needed.
    /// </summary>
    public static void Save(OsPreset?[] presets)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // Always write exactly 4 entries
            var toWrite = new OsPreset?[4];
            for (int i = 0; i < 4; i++)
                toWrite[i] = i < presets.Length ? presets[i] : null;

            var json = JsonSerializer.Serialize(toWrite, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[OsPresetService.Save] {ex.Message}");
        }
    }

    // ── Ready-made presets (optiscaler_presets.json) ──────────────────────────────

    private const string ManifestUrl = "https://raw.githubusercontent.com/RankFTW/RHI/main/optiscaler_presets.json";
    private const int MaxManifestPresets = 12;

    private static readonly string ManifestCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RHI", "optiscaler_presets.json");

    private static readonly JsonSerializerOptions ManifestJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly object _manifestLock = new();
    private static IReadOnlyList<OsPreset>? _manifestPresets;

    /// <summary>Used until optiscaler_presets.json has been fetched once, and when it can't be read.</summary>
    internal static IReadOnlyList<OsPreset> BuiltInPresets { get; } = new List<OsPreset>
    {
        new()
        {
            Id = "RealDlssFg", Name = "Real DLSS FG",
            Description = "OptiScaler를 통한 NVIDIA 자체 DLSS 프레임 생성으로, Reflex가 강제로 켜집니다. RTX 40 및 50 시리즈용.",
            FgEnabled = "true", FgInput = "upscaler", FgOutput = "dlssg", FgNvngxReplacement = "None",
            DeployStreamline = true, DeployDlssEnabler = true, StreamlineVersion = "latest",
            HudFix = "true", ForceReflex = "2", UseGamesReflexMarkers = "false",
        },
        new()
        {
            Id = "DlssEnablerFg", Name = "DLSS Enabler FG",
            Description = "DLSS 프레임 생성을 지원하지 않는 카드를 위해 DLSS Enabler를 통해 DLSS 프레임 생성을 사용합니다.",
            FgEnabled = "true", FgInput = "upscaler", FgOutput = "dlssg", FgNvngxReplacement = "Arturs",
            DeployStreamline = true, DeployDlssEnabler = true, StreamlineVersion = "latest",
            HudFix = "true", ForceReflex = "0", UseGamesReflexMarkers = "true",
        },
        new()
        {
            Id = "UmfgXefg", Name = "UMFG XeFG",
            Description = "Universal MFG Unlocker로 2배를 넘는 XeSS 프레임 생성을 사용합니다. 없으면 자동으로 설치됩니다.",
            FgEnabled = "true", FgInput = "upscaler", FgOutput = "xefg",
            DeployStreamline = true, DeployDlssEnabler = true, StreamlineVersion = "latest",
            HudFix = "true", ForceReflex = "0", UseGamesReflexMarkers = "true",
            InstallUniversalMfg = true,
        },
        new()
        {
            Id = "AnrFg", Name = "ANR FG",
            Description = AnrFgDescription,
            FgInput = "nvngxfg", FgOutput = "auto", FgNvngxReplacement = AnrFgService.ProviderName,
            InstallAnrFg = true,
        },
    };

    private const string AnrFgDescription =
        "게임의 DLSS 프레임 생성 대신 Apex Neural Rendering 프레임 생성(베타)을 사용합니다. "
        + "OptiScaler Nightly(또는 최신 Nightly 기반 빌드)와 DLSS 프레임 생성을 지원하는 게임이 필요하며, 게임에서 DLSS 프레임 생성을 켜야 합니다.";

    /// <summary>The ready-made presets: fetched, else cached, else built-in.</summary>
    public static IReadOnlyList<OsPreset> ManifestPresets
    {
        get
        {
            lock (_manifestLock)
            {
                if (_manifestPresets != null) return _manifestPresets;
                try
                {
                    if (File.Exists(ManifestCachePath)
                        && Parse(File.ReadAllText(ManifestCachePath)) is { } cached)
                        return _manifestPresets = cached;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    CrashReporter.Log($"[OsPresetService.ManifestPresets] Cache read failed — {ex.Message}");
                }
                return _manifestPresets = BuiltInPresets;
            }
        }
    }

    /// <summary>Downloads optiscaler_presets.json and caches it. Keeps the current list on failure.</summary>
    public static async Task FetchManifestAsync(HttpClient http)
    {
        try
        {
            var json = await http.GetStringAsync(ManifestUrl).ConfigureAwait(false);
            var presets = Parse(json);
            if (presets == null)
            {
                CrashReporter.Log("[OsPresetService.FetchManifestAsync] optiscaler_presets.json has no usable presets; keeping the current list");
                return;
            }
            lock (_manifestLock) _manifestPresets = presets;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ManifestCachePath)!);
                await File.WriteAllTextAsync(ManifestCachePath, json).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CrashReporter.Log($"[OsPresetService.FetchManifestAsync] Cache write failed — {ex.Message}");
            }
            CrashReporter.Log($"[OsPresetService.FetchManifestAsync] Loaded {presets.Count} OptiScaler presets");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            CrashReporter.Log($"[OsPresetService.FetchManifestAsync] Fetch failed — {ex.Message}");
        }
    }

    /// <summary>Parses the manifest; null when it's invalid or has no presets with a name.</summary>
    internal static IReadOnlyList<OsPreset>? Parse(string json)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<OsPresetManifest>(json, ManifestJsonOptions);
            var presets = (manifest?.Presets ?? new List<OsPreset>())
                .Concat(manifest?.PresetsV2 ?? new List<OsPreset>())
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name))
                .Take(MaxManifestPresets)
                .ToList();
            return presets is { Count: > 0 } ? presets : null;
        }
        catch (JsonException ex)
        {
            CrashReporter.Log($"[OsPresetService.Parse] Invalid optiscaler_presets.json — {ex.Message}");
            return null;
        }
    }
}
