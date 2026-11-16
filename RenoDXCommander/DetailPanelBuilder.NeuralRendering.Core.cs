// DetailPanelBuilder.NeuralRendering.Core.cs -- the Neural Rendering (DLSS 5) install steps, shared by the
// section's Install button and BONG DLSS5's one-click home screen, plus the automatic method choice.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    /// <summary>
    /// The method picked when the user hasn't chosen one: 32-bit / OpenGL → Feeder; no DLSS → GITC Uplift
    /// (64-bit DX11 / DX12 / Vulkan) else Feeder; DLSS on DX11 / Vulkan → DLSS5 Tool + DX11 Bridge; else ShortFuse.
    /// </summary>
    internal static string RecommendedNrMethod(GameCardViewModel card)
    {
        bool is32Bit  = card.Is32Bit;
        bool isDx11   = card.GraphicsApi == GraphicsApiType.DirectX11;
        bool isDx12   = card.GraphicsApi == GraphicsApiType.DirectX12;
        bool isVulkan = card.GraphicsApi == GraphicsApiType.Vulkan;
        bool upliftApplicable = !is32Bit && (isDx11 || isDx12 || isVulkan);
        return is32Bit                                     ? NrMethodFeeder :
               card.GraphicsApi == GraphicsApiType.OpenGL  ? NrMethodFeeder :
               !card.HasAnyDlssStreamline                  ? (upliftApplicable ? NrMethodUplift : NrMethodFeeder) :
               (isDx11 || isVulkan)                        ? NrMethodDlss5ToolBridge :
                                                             NrMethodShortFuse;
    }

    /// <summary>Readable name of a method key, as in the section's method list.</summary>
    internal static string NrMethodLabel(string key) => key switch
    {
        NrMethodShortFuse       => "ShortFuse DLSS Tool",
        NrMethodDlss5Tool       => "DLSS5 Tool",
        NrMethodDlss5ToolBridge => "DLSS5 Tool + DX11 Bridge",
        NrMethodFeeder          => "DLSS5 Feeder",
        NrMethodUplift          => "GITC Uplift",
        _                       => key,
    };

    /// <summary>True when any Neural Rendering method is installed in the game folder (file checks: call off the UI thread).</summary>
    internal static bool IsNrInstalledIn(GameCardViewModel card)
    {
        var installPath = card.InstallPath;
        if (string.IsNullOrEmpty(installPath)) return false;
        var rdx5Svc = App.Services.GetRequiredService<Renodx5AddonService>();
        return rdx5Svc.IsInstalledIn(installPath)
            || rdx5Svc.IsSfInstalledIn(installPath)
            || File.Exists(Path.Combine(installPath, BridgeDeployFile))
            || File.Exists(Path.Combine(installPath, card.Is32Bit ? FeederDeployFile32 : FeederDeployFile64))
            || UpliftService.IsInstalledIn(installPath, card.Is32Bit);
    }

    /// <summary>
    /// Installs ReShade if needed, then the Neural Rendering method <paramref name="selKey"/>, and records it.
    /// <paramref name="statusBtn"/> shows progress text; empty combos mean "Latest". Throws on failure.
    /// </summary>
    internal async Task RunNrInstallCoreAsync(GameCardViewModel card, string selKey, Button statusBtn,
        ComboBox addonVersionCombo, ComboBox nrVersionCombo, ComboBox? packVersionCombo, bool requireReShade = false)
    {
        var installPath = card.InstallPath!;
        var gameName    = card.GameName;
        var store       = card.Source ?? "";
        var rdx5Svc     = App.Services.GetRequiredService<Renodx5AddonService>();
        var addonSvc    = _window.ViewModel.AddonPackServiceInstance;
        var dlssSvc     = _dlssStreamlineService;
        var upliftSvc   = App.Services.GetRequiredService<UpliftService>();

        // Pre-seed PerGameShaderSelection before installing ReShade so the
        // SyncGameFolder call inside InstallReShadeAsync deploys shaders correctly
        // instead of wiping them (it reads ShaderModeOverride at call time).
        if (selKey == NrMethodFeeder)
        {
            var preGameKey = Models.GameKey.From(card.GameName, card.Source ?? "").ToKey();
            var preCurrent = _gameNameService.PerGameShaderSelection.TryGetValue(preGameKey, out var preSel)
                ? preSel.ToList() : new List<string>();
            if (!preCurrent.Contains("DLSS5Feeder", StringComparer.OrdinalIgnoreCase))
                preCurrent.Add("DLSS5Feeder");
            if (!preCurrent.Contains("LumeniteFX", StringComparer.OrdinalIgnoreCase))
                preCurrent.Add("LumeniteFX");
            _gameNameService.PerGameShaderSelection[preGameKey] = preCurrent;
            _window.ViewModel.SetPerGameShaderMode(card.GameName, "Select", card.Source ?? "");
            card.ShaderModeOverride = "Select";
        }

        // Ensure ReShade is installed first — all NR methods require it
        if (!card.IsRsInstalled)
        {
            _window.DispatcherQueue?.TryEnqueue(() => statusBtn.Content = "ReShade 설치 중...");

            // For DX9 Feeder: dgVoodoo2 will own d3d9.dll, so ReShade must be dxgi.dll.
            // Call InstallReShadeInternalAsync directly with the forced filename instead of
            // going through InstallReShadeCommand which uses auto-detection (returns d3d9.dll for DX9).
            bool feederDx9 = selKey == NrMethodFeeder
                && card.GraphicsApi == GraphicsApiType.DirectX9;
            if (feederDx9)
                await _window.ViewModel.InstallReShadeInternalAsync(card, "dxgi.dll").ConfigureAwait(false);
            else
                await _window.ViewModel.InstallReShadeCommand.ExecuteAsync(card).ConfigureAwait(false);

            // Wait for card to reflect installed state
            await Task.Delay(500).ConfigureAwait(false);
        }
        else if (selKey == NrMethodFeeder
            && (card.DetectedApis.Contains(GraphicsApiType.DirectX9)
                || (card.DetectedApis.Count == 0 && card.GraphicsApi == GraphicsApiType.DirectX9))
            && card.RsRecord?.InstalledAs?.Equals("d3d9.dll", StringComparison.OrdinalIgnoreCase) == true)
        {
            // ReShade already installed as d3d9.dll (wrong for dgVoodoo) — reinstall as dxgi.dll
            _window.DispatcherQueue?.TryEnqueue(() => statusBtn.Content = "ReShade 파일 이름 수정 중...");
            await _window.ViewModel.InstallReShadeInternalAsync(card, "dxgi.dll").ConfigureAwait(false);
            await Task.Delay(300).ConfigureAwait(false);
        }

        // One-click path: stop with a clear reason when ReShade didn't install (the card updates through the
        // dispatcher, so give it a moment), instead of placing DLSS 5 files that can't load.
        if (requireReShade)
        {
            for (int i = 0; i < 20 && !card.IsRsInstalled; i++) await Task.Delay(150).ConfigureAwait(false);
            if (!card.IsRsInstalled)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(card.RsActionMessage)
                    ? "ReShade를 설치하지 못했어요." : card.RsActionMessage);
        }

        switch (selKey)
        {
            case NrMethodDlss5Tool:
                await InstallDlss5ToolAsync(card, statusBtn, addonVersionCombo, nrVersionCombo, rdx5Svc, dlssSvc, addonSvc);
                break;

            case NrMethodDlss5ToolBridge:
                await InstallDlss5ToolAsync(card, statusBtn, addonVersionCombo, nrVersionCombo, rdx5Svc, dlssSvc, addonSvc);
                await InstallBridgeAddonAsync(card, statusBtn, addonSvc, packVersionCombo);
                // Append bridge file to the Dlss5Tool component record
                {
                    var bFiles = Models.RhiInstallManifest.GetComponentFiles(installPath, "Dlss5Tool").ToList();
                    if (!bFiles.Contains(BridgeDeployFile, StringComparer.OrdinalIgnoreCase))
                        bFiles.Add(BridgeDeployFile);
                    Models.RhiInstallManifest.SetComponent(installPath, "Dlss5Tool", bFiles);
                }
                break;

            case NrMethodShortFuse:
                await InstallShortFuseAsync(card, statusBtn, addonVersionCombo, nrVersionCombo, rdx5Svc, dlssSvc);
                break;

            case NrMethodFeeder:
                await InstallFeederAddonAsync(card, statusBtn, addonSvc, dlssSvc, nrVersionCombo, addonVersionCombo, packVersionCombo);
                break;

            case NrMethodUplift:
                await InstallUpliftAsync(card, statusBtn, addonVersionCombo, nrVersionCombo, dlssSvc, upliftSvc);
                break;
        }

        // If Cost Scaler preference is On, deploy it now (NR DLL is freshly placed).
        // Not for GITC Uplift: its NR DLL isn't beside the game exe, where the Cost Scaler works.
        if (selKey != NrMethodUplift && _window.ViewModel.GetNrCostScalerEnabled(gameName, store))
        {
            var csSvc = App.Services.GetRequiredService<DlssNrCostScalerService>();
            if (csSvc.IsStagingReady)
                csSvc.Install(installPath);
        }

        // Persist chosen method
        _window.ViewModel.SetNrMethodOverride(gameName, selKey, store);

        // Record the active NR method in rhi_install.txt for cross-component reference
        // (e.g. OptiScaler uninstall uses this to know nvngx_dlssnr.dll is NR-owned)
        Models.RhiInstallManifest.SetNrMethod(installPath, selKey);

        // Remove conflicting global addons — DLSS5 Tool and ShortFuse both deploy NR addons
        // that conflict with the NR section. Remove them from the global set so they don't
        // get re-deployed on every refresh.
        var globalAddons = _window.ViewModel.Settings.EnabledGlobalAddons;
        var conflicting  = new[] { "DLSS5 Tool", "ShortFuse DLSS Tool" };
        bool removedAny  = false;
        foreach (var c in conflicting)
            if (globalAddons.RemoveAll(a => a.Equals(c, StringComparison.OrdinalIgnoreCase)) > 0)
                removedAny = true;
        if (removedAny)
        {
            _window.ViewModel.SaveSettingsPublic();
            CrashReporter.Log($"[NeuralRendering.Install] Removed conflicting global addons (DLSS5 Tool / ShortFuse) for '{gameName}'");
        }

        // Also remove from per-game selection if the game uses one
        RemoveNrConflictingAddonsFromPerGameSelection(gameName, store, conflicting);

        // Re-deploy addons for this game so stale NR addon files are removed immediately
        _window.ViewModel.DeployAddonsForCard(gameName);
    }
}
