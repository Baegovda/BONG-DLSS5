// MainViewModel.Hsf.cs — HSF (Hunter's Sloppy Frames): DLSS Frame Generation for DX9 / DX11 / DX12 / Vulkan
// games through a ReShade add-on. Install / remove / LaunchPad choice from the Extras row, and keeping
// RHI installs up to date at launch and with the 4-hour refresh (nothing is downloaded unless a game uses it).

using Microsoft.Extensions.DependencyInjection;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.ViewModels;

public partial class MainViewModel
{
    /// <summary>Why HSF can't go into this game (a short button label and the full reason), or null when it can.</summary>
    public static (string Short, string Long)? HsfBlock(GameCardViewModel card)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return ("설치 폴더 없음", "이 게임에는 설치 폴더가 없습니다.");
        if (card.GraphicsApi == GraphicsApiType.OpenGL && !card.RequiresVulkanInstall)
            return ("OpenGL 미지원", "HSF는 DirectX 9, 11, 12 및 Vulkan 게임을 지원하며 OpenGL은 지원하지 않습니다.");
        bool vulkan = HsfIsVulkan(card);
        if (vulkan && card.Is32Bit)
            return ("64비트 Vulkan 전용", "HSF는 64비트 Vulkan 게임만 지원합니다.");
        if (card.IsDxvkInstalled) return ("먼저 DXVK 제거", "이 게임에 DXVK가 설치되어 있습니다. HSF는 게임 자체의 DirectX가 필요하므로 먼저 DXVK를 제거하세요.");
        if (card.IsDxvkBlacklisted) return ("안티치트 게임", "이 게임은 RHI의 안티치트 목록에 있습니다. 온라인 안티치트가 있는 게임에서는 절대 HSF를 사용하지 마세요.");
        if (!card.IsRsInstalled) return ("먼저 ReShade 설치", "먼저 ReShade를 설치하세요: HSF는 ReShade 애드온입니다.");
        if (card.UseNormalReShade) return ("애드온 지원 ReShade 필요", "HSF는 애드온을 지원하는 ReShade가 필요합니다. 이 게임의 ReShade 채널을 No Addons에서 변경하세요.");
        if (vulkan && File.Exists(Path.Combine(card.InstallPath, "dxgi.dll"))
            && AuxInstallService.IsReShadeFile(Path.Combine(card.InstallPath, "dxgi.dll")))
            return ("ReShade dxgi.dll 제거", "이 게임 폴더에 DirectX용 ReShade(dxgi.dll)가 있습니다. Vulkan 게임에서는 이 파일이 ReShade Vulkan 레이어와 HSF를 막으므로 먼저 dxgi.dll을 제거하세요.");
        return null;
    }

    /// <summary>
    /// The game runs through ReShade's Vulkan layer (RHI installs ReShade for Vulkan here: a Vulkan game, or a dual
    /// game set to its Vulkan path). DXVK is left out: HSF is blocked there.
    /// </summary>
    public static bool HsfIsVulkan(GameCardViewModel card) =>
        card.RequiresVulkanInstall && !card.DxvkEnabled;

    /// <summary>Why HSF can't go into this game, or null when it can.</summary>
    public static string? HsfBlockReason(GameCardViewModel card) => HsfBlock(card)?.Long;

    /// <summary>HSF needs ReShade 6.8 (6.5 for DirectX 9). A note when the installed one is older, else null.</summary>
    public static string? HsfReShadeVersionNote(GameCardViewModel card)
    {
        var text = card.RsInstalledVersion;
        if (string.IsNullOrEmpty(text) || !Version.TryParse(new string(text.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()), out var v))
            return null;
        if (v >= new Version(6, 8)) return null;
        if (card.GraphicsApi == GraphicsApiType.DirectX9 && v >= new Version(6, 5)) return null;
        return $"이 게임에는 ReShade {text}이(가) 설치되어 있습니다. HSF는 ReShade 6.8(애드온 지원)에서 테스트되었으니, 프레임 생성이 시작되지 않으면 ReShade를 업데이트하세요.";
    }

    /// <summary>
    /// The game runs DirectX 12 as far as RHI knows: the API set in its Rendering API override when there is one
    /// (a game forced to DirectX 11 isn't), else DirectX 12 among the detected APIs. A dual DirectX 11 / 12 game
    /// counts: HSF takes the route back by itself after a DirectX 11 run, while a DirectX 12 run then starts
    /// generating with no restart.
    /// </summary>
    private bool HsfIsDx12(GameCardViewModel card)
    {
        if (HsfIsVulkan(card)) return false;
        if (GetApiOverride(card.GameName, card.Source ?? "") is { Count: > 0 } apis)
            return apis.Contains(nameof(GraphicsApiType.DirectX12), StringComparer.OrdinalIgnoreCase);
        return card.GraphicsApi == GraphicsApiType.DirectX12 || card.DetectedApis.Contains(GraphicsApiType.DirectX12);
    }

    /// <summary>
    /// The D3D12 route for this game when it runs DirectX 12 (<paramref name="forceDx12"/>: the ⚙ dialog says it
    /// does). Reads the DLSS scan, or looks for the game's Streamline itself when the scan hasn't run, so the
    /// route is ready before the first launch. Touches the disk: call off the UI thread.
    /// </summary>
    public HsfService.Route HsfRouteFor(GameCardViewModel card, bool forceDx12 = false)
    {
        bool? hasSl = card.DlssDetection == null ? null : card.HasStreamline;
        if (hasSl == null && !string.IsNullOrEmpty(card.InstallPath))
            hasSl = HsfService.FindGameStreamline(card.InstallPath);
        return HsfService.PickRoute(forceDx12 || HsfIsDx12(card), card.Is32Bit, hasSl, card.StreamlineInstalledVersion);
    }

    /// <summary>
    /// Downloads (or updates) HSF and installs it into the game: add-on, HSF_streamline, HSF.ini with
    /// the D3D12 route set, then HSF.fx and LaunchPad through the shader sync. Returns an error, or null.
    /// </summary>
    public async Task<string?> InstallHsfAsync(GameCardViewModel card, bool launchpadOn)
    {
        if (HsfBlockReason(card) is { } reason) return reason;
        var installPath = card.InstallPath!;
        var svc = App.Services.GetRequiredService<HsfService>();
        var route = HsfService.Route.None;
        try
        {
            var error = await Task.Run(async () =>
            {
                if (!await svc.EnsureStagingAsync().ConfigureAwait(false))
                    return svc.LastError ?? "HSF를 다운로드할 수 없습니다. 나중에 다시 시도하세요.";
                if (launchpadOn && !UpliftService.LaunchpadStaged)
                    await _shaderPackService.EnsurePacksAsync(new[] { UpliftService.LaunchpadPackId }).ConfigureAwait(false);
                svc.Install(installPath, card.Is32Bit, launchpadOn);
                route = HsfRouteFor(card);
                svc.PresetRoute(installPath, route);
                return null;
            });
            if (error != null) return error;
            await DeployShadersForCardAsync(card.GameName);
            _crashReporter.Log($"[MainViewModel.InstallHsf] HSF {svc.StagedVersion} installed for '{card.GameName}' (LaunchPad {(launchpadOn ? "on" : "off")}, route {route})");
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _crashReporter.Log($"[MainViewModel.InstallHsf] '{card.GameName}' — {ex.Message}");
            return ex is UnauthorizedAccessException
                ? "게임 폴더에 쓸 수 없습니다 (액세스 거부). 게임을 종료하거나 관리자 모드를 사용해 보세요."
                : $"HSF를 설치할 수 없습니다: {ex.Message}";
        }
    }

    /// <summary>
    /// Where Universal MFG Unlocker goes with HSF (HSF's installer guide): next to the exe of a 64-bit game, any
    /// API (in DirectX 12 the game loads it; in DirectX 9 / 11 / Vulkan HSF's helper finds it there by its
    /// product name). 32-bit games: in HSF_streamline\mods, never next to the 32-bit exe.
    /// </summary>
    public static bool HsfMfgNextToExe(GameCardViewModel card) => !card.Is32Bit;

    /// <summary>Sets the game's D3D12 route now (⚙ dialog): on = ready for the first DirectX 12 launch, off = HSF decides.</summary>
    public async Task<bool> SetHsfRouteAsync(GameCardViewModel card, bool on)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return false;
        var svc = App.Services.GetRequiredService<HsfService>();
        return await Task.Run(() =>
        {
            var route = on ? HsfRouteFor(card, forceDx12: true) : HsfService.Route.None;
            if (on && route == HsfService.Route.None) return false; // 32-bit, or Streamline 1.x: HSF has no D3D12 route
            return svc.SetRoute(card.InstallPath!, route);
        });
    }

    /// <summary>
    /// Installs Universal MFG Unlocker for HSF (RTX 20 / 30 / 40 cards): as <paramref name="proxyName"/> next
    /// to the game, or in HSF_streamline\mods when it's null. No RTXMFG-XeSS folder (HSF's frame generation
    /// is DLSS). Call after HSF is installed. Returns an error, or null.
    /// </summary>
    public async Task<string?> InstallMfgForHsfAsync(GameCardViewModel card, string? proxyName)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return "이 게임에는 설치 폴더가 없습니다.";
        if (card.MfgAdaInstalled) return "MFG Unlock이 설치되어 있어 Universal MFG Unlocker와 충돌합니다. 먼저 애드온 선택기에서 제거하세요.";
        var installPath = card.InstallPath;
        try
        {
            var (ok, target) = await Task.Run(async () =>
            {
                var t = proxyName ?? Rtx40MfgService.HsfModsRelPath(installPath);
                if (!_rtx40MfgService.IsStagingReady || _rtx40MfgService.HasUpdate)
                    await _rtx40MfgService.EnsureStagingAsync().ConfigureAwait(false);
                return (_rtx40MfgService.Install(installPath, t), t);
            });
            if (!ok) return "Universal MFG Unlocker를 다운로드할 수 없습니다. 다시 시도하거나 해당 행에서 직접 설치하세요.";
            RecordMfgLocation(card, target);
            _crashReporter.Log($"[MainViewModel.InstallMfgForHsf] '{card.GameName}' as {target}");
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException)
        {
            _crashReporter.Log($"[MainViewModel.InstallMfgForHsf] '{card.GameName}' — {ex.Message}");
            return $"Universal MFG Unlocker를 설치할 수 없습니다: {ex.Message}";
        }
    }

    /// <summary>
    /// Removes HSF from the game, then syncs its shaders so HSF.fx (and LaunchPad, if only HSF needed it) leave too.
    /// Universal MFG Unlocker in HSF_streamline\mods goes with it; a proxy copy next to the game stays (its own row removes it).
    /// </summary>
    public async Task UninstallHsfAsync(GameCardViewModel card)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return;
        var installPath = card.InstallPath;
        var store = card.Source ?? "";
        var svc = App.Services.GetRequiredService<HsfService>();
        var mfg = GetRtx40MfgInstalledAs(card.GameName, store);
        bool mfgInMods = Rtx40MfgService.IsHsfModsInstall(mfg);
        await Task.Run(() =>
        {
            if (mfgInMods) _rtx40MfgService.Uninstall(installPath, mfg);
            svc.Uninstall(installPath);
        });
        if (mfgInMods)
        {
            SetRtx40MfgInstalledAs(card.GameName, null, store);
            card.SetMfgState(mfgAdaInstalled: card.MfgAdaInstalled, mfgAdaRtx40Conflict: false, rtx40MfgInstalled: false);
        }
        await DeployShadersForCardAsync(card.GameName);
    }

    /// <summary>Switches LaunchPad (HSF's optical flow) on or off for the game and syncs its shaders. False if it couldn't.</summary>
    public async Task<bool> SetHsfLaunchpadAsync(GameCardViewModel card, bool on)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return false;
        var installPath = card.InstallPath;
        var svc = App.Services.GetRequiredService<HsfService>();
        bool ok = await Task.Run(async () =>
        {
            if (on && !UpliftService.LaunchpadStaged)
                await _shaderPackService.EnsurePacksAsync(new[] { UpliftService.LaunchpadPackId }).ConfigureAwait(false);
            return svc.SetLaunchpad(installPath, on);
        });
        if (ok) await DeployShadersForCardAsync(card.GameName);
        return ok;
    }

    /// <summary>
    /// Updates HSF in the games RHI installed it in (newest release). Shaders
    /// follow through the shader sync hook. A game that's running keeps its files until the next check.
    /// </summary>
    private async Task RefreshHsfInstallsAsync(string caller)
    {
        try
        {
            var cards = _allCards
                .Where(c => !string.IsNullOrEmpty(c.InstallPath) && HsfService.IsRhiInstalled(c.InstallPath!))
                .GroupBy(c => c.InstallPath!, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            if (cards.Count == 0) return;

            var svc = App.Services.GetRequiredService<HsfService>();
            await svc.CheckForUpdateAsync().ConfigureAwait(false);
            if (!svc.IsStagingReady || svc.HasUpdate)
                await svc.EnsureStagingAsync().ConfigureAwait(false);
            if (!svc.IsStagingReady) return;

            int updated = 0;
            foreach (var c in cards)
            {
                // A DirectX 12 game whose route isn't set yet (the DLSS scan hadn't run at install, or HSF took it
                // back after a DirectX 11 run) gets it now, so its next launch generates without a restart
                svc.PresetRoute(c.InstallPath!, HsfRouteFor(c));
                // HSF switches LaunchPad on itself and ReShade lists it last, after HSF_Motion, which reads its flow:
                // put it first (only the order; the game must be closed, ReShade rewrites the preset on exit)
                UpliftService.KeepLaunchpadFirst(c.InstallPath!);
                if (!svc.UpdateInstalled(c.InstallPath!, c.Is32Bit)) continue;
                updated++;
                await DeployShadersForCardAsync(c.GameName).ConfigureAwait(false); // a new HSF.fx
            }
            _crashReporter.Log($"[{caller}] HSF {svc.StagedVersion}: {cards.Count} game(s), {updated} updated");
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[{caller}] HSF refresh failed — {ex.Message}");
        }
    }
}
