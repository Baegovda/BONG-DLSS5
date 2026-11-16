// DetailPanelBuilder.Extras.Unlocks.cs — Extras section: MFG Ada unlock and DLSS Enabler rows, DLSS Enabler DLL picker.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private void BuildMfgAdaUnlockRow(GameCardViewModel card, StackPanel body)
    {
        _window.ViewModel.SetLastUiAction($"BuildMfgAdaUnlockRow({card.GameName})");
        var gameName    = card.GameName;
        var store       = card.Source ?? "";
        var installPath = card.InstallPath ?? "";

        const string DeployFileName  = "renodx-mfgunlock.addon64";
        var stagedPath  = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RHI", "addons", "MFG Ada Unlock.addon64");

        // Use cached state from card instead of File.Exists on UI thread
        bool isInstalled   = card.MfgAdaInstalled;
        bool rsInstalled   = card.IsRsInstalled;
        bool rtx40Conflict = card.Rtx40MfgInstalled;
        // Use AddonPackService.IsDownloaded instead of File.Exists
        bool staged        = _window.ViewModel.AddonPackServiceInstance.IsDownloaded("MFG Ada Unlock");

        var   addonVersion = AddonPackService.LoadAddonVersion("MFG Ada Unlock");
        string statusText  = isInstalled ? (string.IsNullOrEmpty(addonVersion) ? "설치됨" : $"v{addonVersion}") : "Ready";
        string statusColor = isInstalled ? "#5ECB7D" : "#A0AABB";

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelColW(1, 0, 350, _window.ExtrasContainer.ActualWidth)) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        // Col 0 — label
        var label = new TextBlock
        {
            Text = "MFG Unlock",
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(label, "MFG Unlock — RTX 40 시리즈 GPU에서 DLSS 멀티 프레임 생성(3x/4x/6x)을 잠금 해제하고, RTX 20 및 30 시리즈에 DLSS 프레임 생성(DLSS-G 및 XMX)을 제공하며(실험적), RTX 20~50 시리즈에서 Intel XeSS 프레임 생성(XeMFG, 2x~6x)을 실행합니다. ReShade가 필요합니다.");
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        // Col 1 — status
        var statusBlock = new TextBlock
        {
            Text = statusText,
            FontSize = 12,
            Foreground = UIFactory.GetBrush(statusColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalTextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            TextDecorations = isInstalled ? Windows.UI.Text.TextDecorations.Underline : Windows.UI.Text.TextDecorations.None,
        };
        if (isInstalled)
        {
            ToolTipService.SetToolTip(statusBlock, "클릭하여 GitHub 릴리스 열기");
            statusBlock.PointerPressed += (s, e) =>
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/mavismmg/MFGAdaUnlock-RenoDx/releases"));
            AttachLinkCursor(statusBlock);
        }
        Grid.SetColumn(statusBlock, 1);
        row.Children.Add(statusBlock);

        // Col 2 — Info button (always blue)
        var infoBtn = new Button
        {
            Content = "정보",
            FontSize = 11,
            Padding = new Thickness(6, 2, 6, 2),
            Width = 36,
            Height = 32,
            Background = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
        ToolTipService.SetToolTip(infoBtn, "MFG Unlock GitHub 페이지 열기");
        infoBtn.Click += (s, e) =>
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/mavismmg/MFGAdaUnlock-RenoDx"));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        // Col 3 — Install button
        string btnLabel;
        bool btnEnabled = true;
        if (!rsInstalled)
        {
            btnLabel   = "⚠  ReShade 필요";
            btnEnabled = false;
        }
        else if (rtx40Conflict)
        {
            btnLabel   = "⚠  Universal MFG Unlocker 설치됨";
            btnEnabled = false;
        }
        else if (!staged)
        {
            btnLabel   = "⬇  MFG Unlock 설치";
            btnEnabled = true; // will download on demand when clicked
        }
        else
        {
            btnLabel = isInstalled ? "↺  MFG Unlock 재설치" : "⬇  MFG Unlock 설치";
        }

        var installBtn = new Button
        {
            Content = btnLabel,
            FontSize = 12,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(8),
            Background = isInstalled ? UIFactory.GetBrush("#182840") : UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = isInstalled ? UIFactory.GetBrush("#7AACDD") : UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = isInstalled ? UIFactory.GetBrush("#2A4468") : UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
            IsEnabled = btnEnabled,
            Opacity = btnEnabled ? 1.0 : 0.35,
        };

        if (!rsInstalled)
            ToolTipService.SetToolTip(installBtn, "먼저 ReShade를 설치하세요 — MFG Unlock에 필요합니다");
        else if (rtx40Conflict)
            ToolTipService.SetToolTip(installBtn, "Universal MFG Unlocker가 이미 설치되어 있어 충돌합니다. 먼저 제거하세요.");

        installBtn.Click += async (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            installBtn.IsEnabled = false;
            installBtn.Content   = "다운로드 중...";
            try
            {
                // Download on demand if not yet staged
                if (!File.Exists(stagedPath))
                {
                    var addonSvc = _window.ViewModel.AddonPackServiceInstance;
                    var entry = addonSvc.AvailablePacks.FirstOrDefault(p =>
                        p.PackageName.Equals("MFG Ada Unlock", StringComparison.OrdinalIgnoreCase));
                    if (entry != null)
                        await addonSvc.DownloadAddonAsync(entry).ConfigureAwait(false);
                }

                if (!File.Exists(stagedPath))
                {
                    CrashReporter.Log("[BuildMfgAdaUnlockRow] Staged file still not found after download attempt");
                    _window.DispatcherQueue?.TryEnqueue(() => installBtn.Content = "다운로드 실패");
                    return;
                }

                // Follow reshade.ini AddonPath; ReShade ignores addons in the game folder when it is set
                var deployDir = ModInstallService.GetAddonDeployPath(installPath);
                var dest = Path.Combine(deployDir, DeployFileName);
                File.Copy(stagedPath, dest, overwrite: true);
                // Track it so addon updates are copied here too
                AddonPackService.TrackAddonDeployment(deployDir, DeployFileName);
                RemoveMfgAdaRootCopy(installPath, deployDir);
                CrashReporter.Log($"[BuildMfgAdaUnlockRow] Installed '{DeployFileName}' to '{deployDir}'");

                // Intel's XeSS files for the mod's XeMFG modes, in the game's exe folder.
                // Not fatal: DLSS-G works without them.
                _window.DispatcherQueue?.TryEnqueue(() => installBtn.Content = "XeSS 파일 설치 중...");
                var mfgSvc = App.Services.GetRequiredService<Rtx40MfgService>();
                await mfgSvc.EnsureXessStagingAsync().ConfigureAwait(false);
                if (!mfgSvc.DeployAdaXessFiles(installPath))
                    CrashReporter.Log($"[BuildMfgAdaUnlockRow] XeSS files not deployed to '{installPath}' (download failed or folder not writable)");
                _window.DispatcherQueue?.TryEnqueue(() =>
                {
                    card.SetMfgState(mfgAdaInstalled: true, mfgAdaRtx40Conflict: card.Rtx40MfgInstalled, rtx40MfgInstalled: card.Rtx40MfgInstalled);
                    RequestExtrasRebuild(card);
                });
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildMfgAdaUnlockRow] Install failed — {ex.Message}");
                _window.DispatcherQueue?.TryEnqueue(() => { installBtn.IsEnabled = true; installBtn.Content = KoreanBuild.Tr(btnLabel); });
            }
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        // Col 4 — Cog
        var cogBtn = CogDialog.MakeCogButton("MFG Unlock 설정");
        cogBtn.Click += async (s, e) =>
        {
            try
            {
                var cog = new CogDialog("MFG Unlock", _window.Content.XamlRoot);
                BuildMfgAdaCogContent(cog, card);
                await cog.ShowAsync();
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildMfgAdaUnlockRow] Settings dialog failed — {ex.Message}");
            }
        };
        Grid.SetColumn(cogBtn, 4);
        row.Children.Add(cogBtn);

        // Col 5 — Remove button
        var removeBtn = new Button
        {
            Width = 36,
            Height = 32,
            Padding = new Thickness(0),
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Content = new TextBlock { Text = "✕", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush) },
            Opacity = isInstalled ? 1.0 : 0,
            IsHitTestVisible = isInstalled,
        };
        ToolTipService.SetToolTip(removeBtn, "이 게임에서 MFG Unlock 제거");
        removeBtn.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            try
            {
                // Remove from the AddonPath folder and from the game folder (older installs went there)
                var deployDir = ModInstallService.GetAddonDeployPath(installPath);
                foreach (var dir in new[] { deployDir, installPath }.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var dest = Path.Combine(dir, DeployFileName);
                    if (File.Exists(dest)) File.Delete(dest);
                    AddonPackService.UntrackAddonDeployment(dir, DeployFileName);
                }
                CrashReporter.Log($"[BuildMfgAdaUnlockRow] Removed '{DeployFileName}' from '{deployDir}'");

                // RHI's copies of Intel's XeSS files (hashing runs off the UI thread)
                var mfgSvc = App.Services.GetRequiredService<Rtx40MfgService>();
                _ = Task.Run(() => mfgSvc.RemoveAdaXessFiles(installPath));

                // Also remove from addon selections so SyncGameFolder doesn't redeploy it.
                // MFG Ada Unlock installed via Extras greys out the picker toggle — the user
                // can't deselect it there, so we must clean up the selection here.
                const string PackName = "MFG Ada Unlock";

                // Global selection
                var globalAddons = _window.ViewModel.Settings.EnabledGlobalAddons;
                if (globalAddons.Remove(PackName))
                {
                    _window.ViewModel.SaveSettingsPublic();
                    CrashReporter.Log($"[BuildMfgAdaUnlockRow] Removed '{PackName}' from global addon selection");
                }

                // Per-game selection (composite key first, then legacy name-only)
                var gameNameService = _window.ViewModel.GameNameServiceInstance;
                var compositeKey = Models.GameKey.FromCard(card.GameName, card.Source).ToKey();
                bool perGameChanged = false;
                if (gameNameService.PerGameAddonSelection.TryGetValue(compositeKey, out var perGame))
                    perGameChanged = perGame.Remove(PackName);
                if (!perGameChanged && gameNameService.PerGameAddonSelection.TryGetValue(card.GameName, out var perGameLegacy))
                    perGameChanged = perGameLegacy.Remove(PackName);
                if (perGameChanged)
                {
                    _window.ViewModel.SaveSettingsPublic();
                    CrashReporter.Log($"[BuildMfgAdaUnlockRow] Removed '{PackName}' from per-game addon selection for '{card.GameName}'");
                }

                card.SetMfgState(mfgAdaInstalled: false, mfgAdaRtx40Conflict: card.Rtx40MfgInstalled, rtx40MfgInstalled: card.Rtx40MfgInstalled);
                RequestExtrasRebuild(card);
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildMfgAdaUnlockRow] Remove failed — {ex.Message}");
            }
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    private static readonly string[] DeDllNames =
        { "version.dll", "dxgi.dll", "winmm.dll", "dbghelp.dll", "psapi.dll", "winhttp.dll" };

    /// <summary>
    /// After installing MFG Ada Unlock into a reshade.ini AddonPath folder, removes the copy older
    /// installs left in the game folder. ReShade doesn't load it there, and it would go stale.
    /// </summary>
    private static void RemoveMfgAdaRootCopy(string installPath, string deployDir)
    {
        const string fileName = "renodx-mfgunlock.addon64";
        if (string.Equals(Path.TrimEndingDirectorySeparator(deployDir), Path.TrimEndingDirectorySeparator(installPath),
                StringComparison.OrdinalIgnoreCase))
            return;
        var oldCopy = Path.Combine(installPath, fileName);
        try
        {
            if (File.Exists(oldCopy))
            {
                File.Delete(oldCopy);
                CrashReporter.Log($"[BuildMfgAdaUnlockRow] Removed old '{fileName}' from '{installPath}' (AddonPath is '{deployDir}')");
            }
            AddonPackService.UntrackAddonDeployment(installPath, fileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashReporter.Log($"[BuildMfgAdaUnlockRow] Could not remove old '{fileName}' from '{installPath}' — {ex.Message}");
        }
    }

    // ── MFG Ada Unlock ⚙ ──────────────────────────────────────────────────────
    // The mod keeps its settings in MFGAdaUnlock.ini beside the game's exe (it uses the exe's folder,
    // not the addon folder). Frame generation method = the mod's General › Frame generation method:
    //   NVIDIA DLSS-G           → [Backend] Selected=DLSSG, [DLSSG] XmxGenerator=0
    //   Intel XeMFG (CUDA)      → [Backend] Selected=XeMFG, [DLSSG] XmxGenerator=0
    //   XeMFG via DLSS-G (XMX)  → [Backend] Selected=DLSSG, [DLSSG] XmxGenerator=1
    // A missing ini or unknown value means DLSS-G, as in the mod.

    private const string MfgAdaIniName = "MFGAdaUnlock.ini";
    private static readonly string[] MfgAdaFgMethods = { "NVIDIA DLSS-G", "Intel XeMFG (CUDA)", "XeMFG via DLSS-G (XMX)" };

    private static int ReadMfgAdaFgMethod(string iniPath)
    {
        var selected = Dlssg20_30Service.ReadIniValue(iniPath, "Backend", "Selected");
        if (string.Equals(selected, "XeMFG", StringComparison.OrdinalIgnoreCase)) return 1;
        return Dlssg20_30Service.ReadIniValue(iniPath, "DLSSG", "XmxGenerator") == "1" ? 2 : 0;
    }

    private static void WriteMfgAdaFgMethod(string iniPath, int method)
    {
        Dlssg20_30Service.WriteIniValue(iniPath, "Backend", "Selected", method == 1 ? "XeMFG" : "DLSSG");
        Dlssg20_30Service.WriteIniValue(iniPath, "DLSSG", "XmxGenerator", method == 2 ? "1" : "0");
    }

    private static string ApiDisplayName(GraphicsApiType main, HashSet<GraphicsApiType> apis)
    {
        var api = main != GraphicsApiType.Unknown ? main : apis.FirstOrDefault(a => a != GraphicsApiType.Unknown);
        return api switch
        {
            GraphicsApiType.DirectX8  => "DirectX 8",
            GraphicsApiType.DirectX9  => "DirectX 9",
            GraphicsApiType.DirectX10 => "DirectX 10",
            GraphicsApiType.DirectX11 => "DirectX 11",
            GraphicsApiType.OpenGL    => "OpenGL",
            _ => "구형 그래픽 API",
        };
    }

    private void BuildMfgAdaCogContent(CogDialog cog, GameCardViewModel card)
    {
        var installPath = card.InstallPath ?? "";
        var iniPath = string.IsNullOrEmpty(installPath) ? "" : Path.Combine(installPath, MfgAdaIniName);

        cog.AddIntro("MFG Unlock은 RTX 40 시리즈 GPU에서 DLSS 멀티 프레임 생성(3x, 4x, 6x)을 잠금 해제하고, " +
                     "RTX 20 및 30 시리즈 GPU에 DLSS 프레임 생성(DLSS-G 및 XMX)을 제공하며(실험적), " +
                     "RTX 20~RTX 50 시리즈 GPU에서 Intel XeSS 프레임 생성(XeMFG, 2x~6x)을 실행합니다.");

        // Graphics API support, from the mod's README comparison table (1.8.0). Dual-API games with
        // DirectX 12 get no warning: the user can run the DX12 version.
        var apis = new HashSet<GraphicsApiType>(card.DetectedApis ?? new HashSet<GraphicsApiType>()) { card.GraphicsApi };
        if (apis.Contains(GraphicsApiType.DirectX12)) { }
        else if (apis.Contains(GraphicsApiType.Vulkan))
            cog.AddWarning("Vulkan 게임입니다. MFG Unlock 1.8.0 기준 Vulkan 지원은 실험적입니다. Intel XeMFG (CUDA)는 게임이 " +
                           "DLSS 슈퍼 해상도 또는 레이 재구성을 사용해야 하고, NVIDIA DLSS-G는 실험적이며, XeMFG via DLSS-G (XMX)는 DirectX 12 전용입니다. " +
                           "일부 Vulkan 게임은 ReShade가 로드되기 전에 실행 옵션도 필요합니다(예: +r_allowBlackListedLayers 1).");
        else if (apis.Overlaps(new[] { GraphicsApiType.DirectX8, GraphicsApiType.DirectX9, GraphicsApiType.DirectX10,
                                       GraphicsApiType.DirectX11, GraphicsApiType.OpenGL }))
            cog.AddWarning($"이 게임은 {ApiDisplayName(card.GraphicsApi, apis)}을(를) 사용합니다. MFG Unlock 1.8.0 기준 DirectX 12 게임에서만 작동하므로 " +
                           "(Vulkan 게임은 실험적) 여기서는 작동하지 않습니다.");

        cog.AddSection("프레임 생성");
        Button? openIniBtn = null;
        ComboBox? dlssSrCombo = null;
        int current = string.IsNullOrEmpty(iniPath) ? 0 : ReadMfgAdaFgMethod(iniPath);
        cog.AddCombo("프레임 생성 방식",
            "NVIDIA DLSS-G: NVIDIA의 프레임 생성으로, RTX 40 시리즈에서 3x~6x, RTX 20 및 30 시리즈에서는 실험적으로 작동합니다(DLSS 프레임 생성 DLL 310 이상 필요, RTX 20/30은 처음 한 번 게임 재시작 필요). " +
            "Intel XeMFG (CUDA): Intel의 XeSS 프레임 생성으로, RTX 20~RTX 50 시리즈에서 2x~6x. " +
            "XeMFG via DLSS-G (XMX): 게임의 DLSS 프레임 생성을 사용하되 Intel 모델이 프레임을 생성합니다. " +
            "변경 후 게임을 재시작하고, 게임 자체 메뉴에서 프레임 생성을 켜세요.",
            MfgAdaFgMethods, current,
            i =>
            {
                if (string.IsNullOrEmpty(iniPath)) return false;
                WriteMfgAdaFgMethod(iniPath, i);
                if (openIniBtn != null) openIniBtn.IsEnabled = true; // the ini may not have existed before
                if (dlssSrCombo != null) dlssSrCombo.IsEnabled = i == 1; // DLSS-SR input is an XeMFG (CUDA) option
                CrashReporter.Log($"[MfgAdaCog] '{card.GameName}': frame generation method = {MfgAdaFgMethods[i]}");
                cog.SetSuccess("저장했습니다. 게임이 실행 중이면 재시작하세요.");
                return true;
            },
            isEnabled: !string.IsNullOrEmpty(iniPath));

        // [Adapters] DlssSr: lets XeMFG work in games with DLSS Super Resolution but no DLSS frame generation
        bool dlssSrOn = !string.IsNullOrEmpty(iniPath) && Dlssg20_30Service.ReadIniValue(iniPath, "Adapters", "DlssSr") == "1";
        dlssSrCombo = cog.AddCombo("DLSS-SR 입력 (실험적)",
            "DLSS 슈퍼 해상도는 사용하지만 XeMFG가 연결할 DLSS 프레임 생성이 없는 게임용입니다. " +
            "카메라는 추정되며 HUD는 장면과 함께 보간됩니다. Intel XeMFG (CUDA)에서만 사용됩니다. 변경 후 게임을 재시작하세요.",
            new[] { "꺼짐", "켜짐" }, dlssSrOn ? 1 : 0,
            i =>
            {
                if (string.IsNullOrEmpty(iniPath)) return false;
                Dlssg20_30Service.WriteIniValue(iniPath, "Adapters", "DlssSr", i == 1 ? "1" : "0");
                if (openIniBtn != null) openIniBtn.IsEnabled = true;
                CrashReporter.Log($"[MfgAdaCog] '{card.GameName}': DLSS-SR input = {(i == 1 ? "On" : "Off")}");
                cog.SetSuccess("저장했습니다. 게임이 실행 중이면 재시작하세요.");
                return true;
            },
            isEnabled: !string.IsNullOrEmpty(iniPath) && current == 1);

        // Intel XeSS files used by both XeMFG methods
        switch (Rtx40MfgService.GetAdaXessState(installPath, out var xessDetail))
        {
            case Rtx40MfgService.AdaXessState.Ready:
                cog.AddNote("Intel XeSS 파일(libxess_fg.dll 및 libxell.dll)이 게임 폴더에 있으므로 두 XeMFG 방식 모두 사용할 수 있습니다.");
                break;
            case Rtx40MfgService.AdaXessState.WrongVersion:
                cog.AddWarning($"게임 폴더에 MFG Unlock이 허용하지 않는 버전의 Intel XeSS 파일이 있습니다({xessDetail}). " +
                               "MFG Unlock을 재설치하여 교체하세요. 게임 원본 파일은 백업으로 보관되며 모드를 제거하면 복원됩니다.");
                break;
            default:
                cog.AddWarning($"게임 폴더에 Intel XeSS 파일이 없습니다({xessDetail}). 두 XeMFG 방식 모두 이 파일이 필요합니다. " +
                               "MFG Unlock을 재설치하여 추가하세요.");
                break;
        }

        cog.AddNote("기타 설정은 ReShade 오버레이의 모드 패널에 있습니다.");
        openIniBtn = cog.MakeOpenFileButton("MFGAdaUnlock.ini 열기", string.IsNullOrEmpty(iniPath) ? null : iniPath);
        cog.AddButtons(
            openIniBtn,
            cog.MakeButton("GitHub 페이지 열기", CogButtonKind.Secondary,
                async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/mavismmg/MFGAdaUnlock-RenoDx")); }));
    }

    private void BuildDlssEnablerRow(GameCardViewModel card, StackPanel body)
    {
        _window.ViewModel.SetLastUiAction($"BuildDlssEnablerRow({card.GameName})");
        var deSvc       = App.Services.GetRequiredService<DlssEnablerService>();
        var gameName    = card.GameName;
        var store       = card.Source ?? "";
        var installPath = card.InstallPath ?? "";

        var currentDllName = _window.ViewModel.GetDeInstalledAs(gameName, store);
        _window.ViewModel.SetLastUiAction($"BuildDlssEnablerRow:IsInstalled({card.GameName})");
        bool isInstalled   = deSvc.IsStandaloneInstalledIn(installPath, currentDllName);

        // Mutual exclusivity with OptiScaler
        bool osConflict = card.IsOsInstalled;

        _window.ViewModel.SetLastUiAction($"BuildDlssEnablerRow:StagedVersion({card.GameName})");
        // Status text
        string statusText  = isInstalled ? (deSvc.StagedVersion ?? "설치됨") : "Ready";
        string statusColor = isInstalled ? "#5ECB7D" : "#A0AABB";

        _window.ViewModel.SetLastUiAction($"BuildDlssEnablerRow:BuildGrid({card.GameName})");
        // ── Row grid matching Components section exactly ───────────────────────
        // Col 0: label (120)  Col 1: status (80)  Col 2: Info (36)
        // Col 3: install (*)  Col 4: cog (36)     Col 5: delete (36)
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelColW(1, 0, 350, _window.ExtrasContainer.ActualWidth)) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        // Col 0 — label
        var label = new TextBlock
        {
            Text = "DLSS Enabler",
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(label, "DLSS Enabler — DLSS를 기본 지원하지 않는 게임에서 DLSS를 사용할 수 있게 해 주는 독립형 프록시 DLL입니다.");
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        // Col 1 — status
        var statusBlock = new TextBlock
        {
            Text = statusText,
            FontSize = 12,
            Foreground = UIFactory.GetBrush(statusColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalTextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            TextDecorations = isInstalled ? Windows.UI.Text.TextDecorations.Underline : Windows.UI.Text.TextDecorations.None,
        };
        if (isInstalled)
        {
            ToolTipService.SetToolTip(statusBlock, $"설치된 이름: {currentDllName}\n클릭하여 GitHub 릴리스 열기");
            statusBlock.PointerPressed += (s, e) =>
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/RankFTW/rhi-repo/releases"));
            AttachLinkCursor(statusBlock);
        }
        Grid.SetColumn(statusBlock, 1);
        row.Children.Add(statusBlock);

        // Col 2 — Info button
        var infoBtn = new Button
        {
            Content = "정보",
            FontSize = 11,
            Padding = new Thickness(6, 2, 6, 2),
            Width = 36,
            Height = 32,
            Background = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
        ToolTipService.SetToolTip(infoBtn, "DLSS Enabler Nexus 페이지 열기");
        infoBtn.Click += (s, e) =>
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.nexusmods.com/site/mods/757"));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        // Col 3 — Install button
        string installBtnLabel;
        if (osConflict)
            installBtnLabel = "OptiScaler로 설치됨";
        else
            installBtnLabel = isInstalled ? "↺  DLSS Enabler 재설치" : "⬇  DLSS Enabler 설치";

        var installBtn = new Button
        {
            Content = installBtnLabel,
            FontSize = 12,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(8),
            Background = isInstalled
                ? UIFactory.GetBrush("#182840")
                : UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = isInstalled
                ? UIFactory.GetBrush("#7AACDD")
                : UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = isInstalled
                ? UIFactory.GetBrush("#2A4468")
                : UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
        };

        if (osConflict)
        {
            installBtn.IsEnabled = false;
            installBtn.IsHitTestVisible = false;
            installBtn.Opacity = 0.35;
            ToolTipService.SetToolTip(installBtn, "OptiScaler와 함께 설치할 수 없습니다 — DLSS Enabler는 이미 OptiScaler에 포함되어 있습니다");
        }
        else
        {
            ToolTipService.SetToolTip(installBtn, isInstalled
                ? $"DLSS Enabler 재설치 (현재 '{currentDllName}')"
                : "DLSS Enabler 설치 — 사용할 DLL 이름 선택");

            installBtn.Click += async (s, e) =>
            {
                if (string.IsNullOrEmpty(installPath)) return;
                var chosen = await ShowDeDllPickerAsync(card, currentDllName);
                if (chosen == null) return;

                installBtn.IsEnabled = false;
                installBtn.Content   = "설치 중...";
                try
                {
                    bool ok = await deSvc.InstallStandaloneAsync(gameName, installPath, store, chosen, currentDllName);
                    if (ok)
                    {
                        _window.ViewModel.SetDeInstalledAs(gameName, chosen, store);
                        RequestExtrasRebuild(card);
                    }
                    else
                    {
                        installBtn.Content   = "설치 실패";
                        installBtn.IsEnabled = true;
                    }
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[BuildDlssEnablerRow] Install failed — {ex.Message}");
                    installBtn.Content   = "설치 실패";
                    installBtn.IsEnabled = true;
                }
            };
        }
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        // Col 4 — Cog button
        var cogBtn = CogDialog.MakeCogButton(osConflict ? "OptiScaler와 함께 설치할 수 없음" : "DLSS Enabler 설정");
        cogBtn.IsEnabled = !osConflict;
        cogBtn.IsHitTestVisible = !osConflict;
        cogBtn.Opacity = osConflict ? 0.35 : 1.0;
        cogBtn.Click += async (s, e) =>
        {
            try
            {
                var cog = new CogDialog("DLSS Enabler", _window.Content.XamlRoot);
                cog.AddEmpty();
                await cog.ShowAsync();
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildDlssEnablerRow] Settings dialog failed — {ex.Message}");
            }
        };
        Grid.SetColumn(cogBtn, 4);
        row.Children.Add(cogBtn);

        // Col 5 — Remove button (hidden when not installed)
        var removeBtn = new Button
        {
            Width = 36,
            Height = 32,
            Padding = new Thickness(0),
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Content = new TextBlock { Text = "✕", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush) },
            Opacity = isInstalled ? 1.0 : 0,
            IsHitTestVisible = isInstalled,
        };
        ToolTipService.SetToolTip(removeBtn, "이 게임에서 독립형 DLSS Enabler 제거");
        removeBtn.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath) || string.IsNullOrEmpty(currentDllName)) return;
            deSvc.UninstallStandalone(installPath, currentDllName);
            _window.ViewModel.SetDeInstalledAs(gameName, null, store);
            RequestExtrasRebuild(card);
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
        _window.ViewModel.SetLastUiAction($"BuildDlssEnablerRow:Done({card.GameName})");
    }

    private async Task<string?> ShowDeDllPickerAsync(GameCardViewModel card, string? currentDllName)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return null;

        string? chosen = null;

        var listPanel    = new StackPanel { Spacing = 4 };
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 420,
            Content   = listPanel,
        };

        foreach (var option in GetExtraDllOptions(card, DeDllNames, currentDllName))
        {
            var name           = option.Name;
            bool isRecommended = option.IsRecommended;
            bool isRhiOwned    = option.BlockedBy != null;
            bool isDxgiConflict = string.Equals(name, "dxgi.dll", StringComparison.OrdinalIgnoreCase)
                               && !string.IsNullOrEmpty(card.RsInstalledFile);
            bool isCurrent     = option.IsCurrent;

            var btn = new Button
            {
                HorizontalAlignment        = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding         = new Thickness(10, 6, 10, 6),
                CornerRadius    = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                IsEnabled       = !isRhiOwned,
                Opacity         = isRhiOwned ? 0.4 : 1.0,
            };

            if (isCurrent)
            {
                btn.Background  = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush);
                btn.BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush);
            }
            else
            {
                btn.Background  = UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush);
                btn.BorderBrush = UIFactory.Brush(ResourceKeys.BorderDefaultBrush);
            }

            var contentRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            contentRow.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 12,
                Foreground = isRhiOwned
                    ? UIFactory.Brush(ResourceKeys.TextTertiaryBrush)
                    : UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
                VerticalAlignment = VerticalAlignment.Center,
            });

            if (isRecommended)
                contentRow.Children.Add(MakeBadge("권장", "#1A3A20", "#6AE87A", "#2A5A30"));
            AddExtraDllBadges(contentRow, btn, option, "DLSS Enabler");
            if (!isRhiOwned && isDxgiConflict)
                contentRow.Children.Add(MakeBadge("ReShade/OS와 충돌 가능", "#2A1A10", "#CC9955", "#5A3A18"));
            if (isCurrent)
                contentRow.Children.Add(MakeBadge("현재", "#182840", "#7AACDD", "#2A4468"));

            btn.Content = contentRow;

            if (!isRhiOwned && isDxgiConflict)
                ToolTipService.SetToolTip(btn, "ReShade 또는 OS 구성 요소가 같은 이름을 사용하면 dxgi.dll이 충돌할 수 있습니다.");

            btn.Tag    = name;
            btn.Click += (s, ev) =>
            {
                chosen = (s as Button)?.Tag as string;
                if (s is FrameworkElement fe)
                {
                    var dialog = FindParentContentDialog(fe);
                    dialog?.Hide();
                }
            };

            listPanel.Children.Add(btn);
        }

        var pickerDialog = new ContentDialog
        {
            Title = "DLSS Enabler DLL 이름 선택",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "독립형 DLSS Enabler DLL의 파일 이름을 선택하세요. 대부분의 게임은 version.dll로 작동합니다.",
                        FontSize = 11,
                        Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    scrollViewer,
                }
            },
            CloseButtonText = "취소",
            XamlRoot        = _window.Content.XamlRoot,
        };

        await DialogService.ShowSafeAsync(pickerDialog);
        return chosen;
    }
}
