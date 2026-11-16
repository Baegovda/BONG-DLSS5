// MainViewModel.AnrFg.cs — installs ANR FG (Apex Neural Rendering frame generation) for the
// OptiScaler preset that asks for it (installAnrFg in optiscaler_presets.json).

using Microsoft.Extensions.DependencyInjection;
using RenoDXCommander.Services;

namespace RenoDXCommander.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// Makes sure ANR FG is in the game's OptiScaler custom folder, downloading or updating it first.
    /// Needs an OptiScaler build that loads custom frame generation providers: Nightly from 29 Oct 2026,
    /// or a fork / custom build based on one. Checked in the installed DLL, not by version name.
    /// Returns an error message, or null when it's in place.
    /// </summary>
    public async Task<string?> EnsureAnrFgForPresetAsync(GameCardViewModel card)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return "이 게임에는 설치 폴더가 없습니다.";
        if (!card.IsOsInstalled) return "먼저 OptiScaler (나이틀리)를 설치하세요.";

        var installPath = card.InstallPath;
        var record = _auxInstaller.FindRecord(card.GameName, installPath, OptiScalerService.AddonType);
        var variant = record?.OsVariant ?? OsVariants.Stable;
        var osDll = string.IsNullOrEmpty(record?.InstalledAs) ? null : FileHelper.SafeCombine(installPath, record.InstalledAs);
        bool supported = osDll != null && await Task.Run(() => OptiScalerService.SupportsCustomFgProviders(osDll));
        if (!supported)
        {
            _crashReporter.Log($"[MainViewModel.EnsureAnrFgForPreset] '{card.GameName}': OptiScaler {variant} ({record?.InstalledAs ?? "no record"}) can't load custom frame generation providers");
            return $"이 OptiScaler 버전({OsVariants.Label(variant)})은 아직 ANR FG를 불러올 수 없습니다. 버전을 나이틀리 또는 최근 나이틀리 기반 빌드로 설정한 후 프리셋을 다시 적용하세요.";
        }

        var anr = App.Services.GetRequiredService<AnrFgService>();
        try
        {
            var error = await Task.Run(async () =>
            {
                if (!await anr.EnsureStagingAsync().ConfigureAwait(false))
                    return "ANR FG를 다운로드하지 못했습니다. 나중에 다시 시도하세요.";
                return anr.Install(installPath);
            });
            if (error == null)
                _crashReporter.Log($"[MainViewModel.EnsureAnrFgForPreset] ANR FG {anr.StagedVersion} in place for '{card.GameName}'");
            return error;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException)
        {
            _crashReporter.Log($"[MainViewModel.EnsureAnrFgForPreset] '{card.GameName}' — {ex.Message}");
            return $"ANR FG를 설치하지 못했습니다: {ex.Message}";
        }
    }
}
