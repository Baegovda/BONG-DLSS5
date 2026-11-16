// DetailPanelBuilder.Extras.Hsf.cs — Extras section (MFG Unlocks): HSF (Hunter's Sloppy Frames) row, its install
// notice and its ⚙ dialog. The work is in MainViewModel.Hsf.cs / HsfService.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    private const string HsfTooltip =
        "HSF (Hunter's Sloppy Frames) — DirectX 9, 11, 12 및 Vulkan 게임용 DLSS 프레임 생성(최대 6x)을 제공하는 ReShade 애드온입니다. "
        + "DLSS를 사용하는 DirectX 12 게임에서는 게임 자체 DLSS 데이터를, 그 외에는 iMMERSE LaunchPad의 옵티컬 플로우를 사용합니다. "
        + "RTX 40 이상 GPU(RTX 20 / 30은 Universal MFG Unlocker 사용)와 애드온을 지원하는 ReShade가 필요합니다.";

    private void BuildHsfRow(GameCardViewModel card, StackPanel body)
    {
        _window.ViewModel.SetLastUiAction($"BuildHsfRow({card.GameName})");
        var svc         = App.Services.GetRequiredService<HsfService>();
        var installPath = card.InstallPath ?? "";

        bool isInstalled  = HsfService.IsInstalledIn(installPath, card.Is32Bit);
        bool rhiInstalled = isInstalled && HsfService.IsRhiInstalled(installPath);
        var installedVer  = rhiInstalled ? HsfService.ReadMarker(installPath).Version : null;
        var block         = MainViewModel.HsfBlock(card);

        string statusText  = !isInstalled ? "Ready" : rhiInstalled ? (installedVer ?? "Installed") : "Manual";
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
            Text = "HSF",
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(label, HsfTooltip);
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
            ToolTipService.SetToolTip(statusBlock, rhiInstalled
                ? $"BONG DLSS5가 설치한 HSF {installedVer}\n클릭하면 HSF GitHub 페이지가 열립니다"
                : "HSF가 수동으로 설치되었습니다. 업데이트를 받으려면 BONG DLSS5로 재설치하세요.\n클릭하면 HSF GitHub 페이지가 열립니다");
            statusBlock.PointerPressed += (s, e) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(HsfService.ReleasesPage));
            AttachLinkCursor(statusBlock);
        }
        Grid.SetColumn(statusBlock, 1);
        row.Children.Add(statusBlock);

        // Col 2 — Info button
        var infoBtn = new Button
        {
            Content = "Info",
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
        ToolTipService.SetToolTip(infoBtn, "HSF GitHub 페이지 열기");
        infoBtn.Click += (s, e) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(HsfService.ReleasesPage));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        // Col 3 — Install button
        var installBtn = new Button
        {
            Content = isInstalled ? "↺  Reinstall HSF" : "⬇  Install HSF",
            FontSize = 12,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(8),
            Background = isInstalled ? UIFactory.GetBrush("#182840") : UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = isInstalled ? UIFactory.GetBrush("#7AACDD") : UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = isInstalled ? UIFactory.GetBrush("#2A4468") : UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
        };
        if (block is { } b)
        {
            installBtn.IsEnabled = false;
            installBtn.Opacity   = 0.35;
            installBtn.Content   = KoreanBuild.Tr(b.Short);
            // Disabled buttons show no tooltip: the label carries the reason too
            ToolTipService.SetToolTip(label, $"{HsfTooltip}\n\n{b.Long}");
        }
        else
        {
            var versionNote = MainViewModel.HsfReShadeVersionNote(card);
            ToolTipService.SetToolTip(installBtn, (isInstalled
                ? "HSF를 재설치합니다(최신 릴리스). HSF 설정은 유지됩니다."
                : "HSF 설치: 애드온, HSF_streamline 폴더(HSF 라이선스 파일 포함), HSF.ini, HSF.fx, 그리고 옵티컬 플로우용 iMMERSE LaunchPad.")
                + (versionNote != null ? "\n\n" + versionNote : ""));
        }

        installBtn.Click += async (s, e) =>
        {
            try
            {
                if (string.IsNullOrEmpty(installPath)) return;
                if (!await ShowHsfNoticeAsync(card, installBtn.XamlRoot)) return;
                var mfg = await AskHsfMfgUnlockerAsync(card, installBtn.XamlRoot);
                if (mfg.Cancelled) return;

                installBtn.IsEnabled = false;
                installBtn.Content   = KoreanBuild.Tr("Installing...");
                // Reinstall keeps the game's LaunchPad choice; a new install starts with it on
                bool launchpad = !HsfService.IsRhiInstalled(installPath) || HsfService.ReadMarker(installPath).LaunchpadOn;
                var error = await _window.ViewModel.InstallHsfAsync(card, launchpad);
                if (error == null)
                {
                    if (mfg.Install)
                    {
                        var mfgError = await _window.ViewModel.InstallMfgForHsfAsync(card, mfg.ProxyName);
                        if (mfgError != null)
                            await ShowHsfErrorAsync(mfgError, installBtn.XamlRoot, "Universal MFG Unlocker가 설치되지 않았습니다");
                    }
                    RequestExtrasRebuild(card);
                    return;
                }
                installBtn.Content   = KoreanBuild.Tr("Install failed");
                installBtn.IsEnabled = true;
                statusBlock.Text = "Failed";
                statusBlock.Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush);
                ToolTipService.SetToolTip(statusBlock, error);
                await ShowHsfErrorAsync(error, installBtn.XamlRoot);
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildHsfRow] Install failed — {ex.Message}");
                installBtn.Content   = KoreanBuild.Tr("Install failed");
                installBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        // Col 4 — Cog button
        var cogBtn = CogDialog.MakeCogButton("HSF 설정");
        cogBtn.Click += async (s, e) =>
        {
            try { await ShowHsfSettingsAsync(card, isInstalled, rhiInstalled, svc); }
            catch (Exception ex) { CrashReporter.Log($"[BuildHsfRow] Settings dialog failed — {ex.Message}"); }
        };
        Grid.SetColumn(cogBtn, 4);
        row.Children.Add(cogBtn);

        // Col 5 — Remove button
        var removeBtn = new Button
        {
            Width = 36, Height = 32, Padding = new Thickness(0),
            Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Content = new TextBlock { Text = "✕", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush) },
            Opacity = isInstalled ? 1.0 : 0,
            IsHitTestVisible = isInstalled,
        };
        ToolTipService.SetToolTip(removeBtn, "이 게임에서 HSF 제거 (애드온, HSF.ini, HSF_streamline, HSF.fx)");
        removeBtn.Click += async (s, e) =>
        {
            try
            {
                removeBtn.IsEnabled = false;
                await _window.ViewModel.UninstallHsfAsync(card);
                RequestExtrasRebuild(card);
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildHsfRow] Remove failed — {ex.Message}");
                removeBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    /// <summary>
    /// Shown before an install: anti-cheat, the game's own / OptiScaler frame generation, and how to open
    /// HSF's menu. "Don't show this again" hides it, except when OptiScaler is installed in the game.
    /// Returns true to continue.
    /// </summary>
    private async Task<bool> ShowHsfNoticeAsync(GameCardViewModel card, XamlRoot? xamlRoot)
    {
        xamlRoot ??= _window.Content.XamlRoot;
        bool osInstalled = card.IsOsInstalled;
        if (xamlRoot == null || (_window.ViewModel.Settings.HsfNoticeDismissed && !osInstalled)) return true;

        var content = new StackPanel { Spacing = 8 };
        void Para(string text) => content.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 20,
            Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
        });
        Para("온라인 안티치트가 있는 게임에서는 절대 HSF를 사용하지 마세요. 계정이 정지될 수 있습니다.");
        Para("게임 자체 프레임 생성을 끄세요. HSF 메뉴는 ReShade 오버레이(Home 키)의 탭에 있습니다. 그곳에서 배수를 설정하고 FRAME GEN을 켜세요.");
        if (osInstalled)
            Para("이 게임에 OptiScaler가 설치되어 있습니다. HSF와 OptiScaler의 프레임 생성은 충돌할 수 있으니 하나만 사용하고, 게임이 충돌하면 OptiScaler를 제거한 뒤 HSF만 사용해 보세요.");
        var dontShow = new CheckBox { Content = "다시 표시 안 함", FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };
        if (!osInstalled) content.Children.Add(dontShow);

        var dialog = new ContentDialog
        {
            Title = "HSF 설치 전 안내",
            Content = content,
            PrimaryButtonText = "설치",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        var result = await DialogService.ShowSafeAsync(dialog);
        if (dontShow.IsChecked == true)
        {
            _window.ViewModel.Settings.HsfNoticeDismissed = true;
            _window.ViewModel.SaveSettingsPublic();
        }
        return result == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Asked on every HSF install: should Universal MFG Unlocker go in too (RTX 20 / 30 / 40 cards need it)?
    /// Not asked when it's already in the game, MFG Unlock (add-on) is, or the card is RTX 50 or newer.
    /// The detected card sets the default answer. A yes in a 64-bit game (any API) goes on to the DLL name
    /// picker, next to the exe; 32-bit games get it in HSF_streamline\mods. Cancelled = install nothing.
    /// </summary>
    private async Task<(bool Cancelled, bool Install, string? ProxyName)> AskHsfMfgUnlockerAsync(GameCardViewModel card, XamlRoot? xamlRoot)
    {
        xamlRoot ??= _window.Content.XamlRoot;
        if (xamlRoot == null || card.Rtx40MfgInstalled || card.MfgAdaInstalled) return (false, false, null);
        var gpu = GpuInfoService.Primary(await GpuInfoService.GetNvidiaGpusAsync());
        if (gpu is { Family: GpuFamily.Rtx50 or GpuFamily.Newer }) return (false, false, null);
        bool needs = gpu != null && GpuInfoService.NeedsMfgUnlocker(gpu.Family);
        bool nextToExe = MainViewModel.HsfMfgNextToExe(card);

        var content = new StackPanel { Spacing = 8 };
        void Para(string text) => content.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 20,
            Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
        });
        Para("RTX 20, 30, 40 카드는 HSF에 Universal MFG Unlocker가 필요합니다. RTX 20과 30은 모든 프레임 생성에, RTX 40은 2x 초과에 필요합니다. RTX 50 카드는 필요 없습니다.");
        Para(gpu == null
            ? "BONG DLSS5가 이 PC에서 NVIDIA 그래픽 카드를 찾지 못했습니다."
            : $"그래픽 카드: {gpu.Name} ({gpu.FamilyLabel}). {(needs ? "언락커가 필요합니다." : "RTX 카드가 아니므로 DLSS 프레임 생성을 실행할 수 없습니다.")}");
        Para(nextToExe
            ? "게임 exe 옆에 설치됩니다. 다음 단계에서 다른 모드와 충돌하지 않도록 설치할 DLL 이름을 선택합니다."
            : "32비트 게임이므로 언락커는 HSF 도우미가 불러오는 HSF_streamline\\mods에 설치됩니다.");

        var dialog = new ContentDialog
        {
            Title = "Universal MFG Unlocker도 설치할까요?",
            Content = content,
            PrimaryButtonText = "둘 다 설치",
            SecondaryButtonText = "HSF만",
            CloseButtonText = "취소",
            DefaultButton = needs ? ContentDialogButton.Primary : ContentDialogButton.Secondary,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        var result = await DialogService.ShowSafeAsync(dialog);
        if (result == ContentDialogResult.Secondary) return (false, false, null);
        if (result != ContentDialogResult.Primary) return (true, false, null);
        if (!nextToExe) return (false, true, null);

        var name = await ShowMfgDllPickerAsync(card, null);
        return name == null ? (true, false, null) : (false, true, name);
    }

    private static async Task ShowHsfErrorAsync(string error, XamlRoot? xamlRoot, string title = "HSF가 설치되지 않았습니다")
    {
        if (xamlRoot == null) return;
        await DialogService.ShowSafeAsync(new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap, FontSize = 13 },
            CloseButtonText = "확인",
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        });
    }

    private async Task ShowHsfSettingsAsync(GameCardViewModel card, bool isInstalled, bool rhiInstalled, HsfService svc)
    {
        var installPath = card.InstallPath ?? "";
        var cog = new CogDialog("HSF", _window.Content.XamlRoot);
        cog.AddIntro("HSF는 게임에 DLSS 프레임 생성을 추가합니다. ReShade 오버레이(Home 키)를 열고 HSF 탭으로 이동해 "
            + "FRAME GEN을 켜고 배수를 고르세요(게임 중 F7로 전환). 더 부드러운 프레임을 위해 그곳에서 FPS 제한을 설정하세요.");

        cog.AddSection("이 게임");
        if (!isInstalled)
            cog.AddNote("이 게임에 HSF가 설치되어 있지 않습니다.");
        else
        {
            cog.AddNote(rhiInstalled
                ? $"설치됨: HSF {HsfService.ReadMarker(installPath).Version ?? "(알 수 없는 버전)"}. 새 릴리스가 나오면 BONG DLSS5가 업데이트합니다."
                : "수동으로 설치됨. 업데이트를 받으려면 BONG DLSS5로 재설치하세요.");
            cog.AddNote($"DirectX 12 경로: {HsfService.RouteText(installPath)}.");
        }

        // D3D12 route: HSF can only start it before the game makes its device, so on its own it asks for a restart
        // after the first DirectX 12 run. Setting it here (RHI does it by itself for DirectX 12 games) avoids that.
        if (isInstalled && !card.Is32Bit && !MainViewModel.HsfIsVulkan(card))
        {
            cog.AddSection("DirectX 12");
            cog.AddIntro("DirectX 12 게임에서는 HSF 프레임 생성이 게임보다 먼저 시작되어야 합니다. HSF는 첫 "
                + "DirectX 12 실행 시 이를 설정한 뒤 다시 시작을 요청합니다. BONG DLSS5는 DirectX 12로 실행되는 것으로 알려진 게임에 대해 설치 시 이를 설정하므로 "
                + "(렌더링 API 재정의를 따름) 첫 실행부터 프레임이 생성됩니다. 이 게임이 "
                + "DirectX 12로 실행되는데 BONG DLSS5가 설정하지 않았다면 여기서 설정하세요. DirectX 11로 실행하면 HSF가 스스로 해제합니다.");
            bool routeOn = HsfService.IsRouteSet(installPath);
            cog.AddCombo("실행 전 준비",
                "켜짐: DirectX 12에서 다시 시작 없이 프레임 생성이 시작됩니다. HSF 결정: HSF가 첫 DirectX 12 실행 시 설정하고 한 번 다시 시작을 요청합니다.",
                new[] { "켜짐", "HSF 결정" }, routeOn ? 0 : 1,
                i =>
                {
                    bool on = i == 0;
                    _ = Task.Run(async () =>
                    {
                        bool ok = await _window.ViewModel.SetHsfRouteAsync(card, on);
                        _window.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (ok) cog.SetSuccess(on ? "DirectX 12 경로 설정됨: 다음 DirectX 12 실행부터 다시 시작 없이 프레임이 생성됩니다."
                                                      : "DirectX 12 경로 해제됨: HSF가 직접 설정합니다.");
                            else cog.SetError(on ? "경로를 설정할 수 없습니다(Streamline 1.x 게임에는 HSF DirectX 12 경로가 없음). 로그를 확인하세요."
                                                 : "HSF.ini를 변경할 수 없습니다. 게임을 닫고 다시 시도하세요.");
                        });
                    });
                });
        }
        if (MainViewModel.HsfReShadeVersionNote(card) is { } rsNote) cog.AddWarning(rsNote);
        if (MainViewModel.HsfBlockReason(card) is { } reason) cog.AddWarning(reason);

        cog.AddSection("모션");
        cog.AddIntro("DLSS를 사용하는 DirectX 12 게임은 자체 모션 데이터를 HSF에 제공합니다. 그 외 모든 게임(DirectX 9, 11, Vulkan)은 "
            + "ReShade 효과의 옵티컬 플로우가 필요하며, BONG DLSS5는 HSF와 함께 iMMERSE LaunchPad를 설치합니다. LumeniteFX Kernel도 작동하며(include 폴더 포함), "
            + "셰이더에 있다면 HSF의 FLOW FROM 행에서 선택하세요.");
        if (rhiInstalled)
        {
            bool lpOn = HsfService.ReadMarker(installPath).LaunchpadOn;
            cog.AddCombo("iMMERSE LaunchPad",
                "이 게임이 항상 자체 DLSS 데이터를 HSF에 제공하거나 대신 LumeniteFX를 사용하는 경우에만 끄세요. 플로우 소스가 없으면 DirectX 9, 11, Vulkan 게임에 \"no motion vectors\"가 표시됩니다.",
                new[] { "켜짐", "꺼짐" }, lpOn ? 0 : 1,
                i =>
                {
                    bool on = i == 0;
                    cog.SetStatus(on ? "LaunchPad 추가 중..." : "LaunchPad 제거 중...");
                    _ = Task.Run(async () =>
                    {
                        bool ok = await _window.ViewModel.SetHsfLaunchpadAsync(card, on);
                        _window.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (ok) cog.SetSuccess(on ? "LaunchPad가 게임 셰이더에 추가되었습니다." : "LaunchPad를 게임 셰이더에서 제거했습니다.");
                            else cog.SetError("LaunchPad를 변경할 수 없습니다. 로그를 확인하세요.");
                        });
                    });
                });
        }

        cog.AddSection("알아 두기");
        var gpu = GpuInfoService.Primary(await GpuInfoService.GetNvidiaGpusAsync());
        cog.AddNote(gpu == null
            ? "그래픽 카드: NVIDIA 카드를 찾을 수 없습니다. HSF에는 NVIDIA RTX 카드가 필요합니다."
            : gpu.Family switch
            {
                GpuFamily.Rtx50 or GpuFamily.Newer => $"그래픽 카드: {gpu.Name}. 자체적으로 최대 6x까지 지원하므로 Universal MFG Unlocker가 필요 없습니다.",
                GpuFamily.Rtx40 => $"그래픽 카드: {gpu.Name}. 자체적으로 2x를 지원하며, Universal MFG Unlocker(같은 섹션)로 최대 6x까지 올릴 수 있습니다.",
                GpuFamily.Rtx20 or GpuFamily.Rtx30 => $"그래픽 카드: {gpu.Name}. 프레임 생성에 Universal MFG Unlocker(같은 섹션)가 필요합니다.",
                _ => $"그래픽 카드: {gpu.Name}. RTX 카드가 아니므로 DLSS 프레임 생성을 실행할 수 없습니다.",
            });
        cog.AddNote("DirectX 9, 11, Vulkan 게임: 창 모드 또는 테두리 없는 창 모드로 플레이하세요. HSF는 생성된 프레임을 게임 위의 별도 창에 표시합니다.");
        if (MainViewModel.HsfIsVulkan(card))
            cog.AddNote("Vulkan: HSF는 ReShade의 Vulkan 레이어를 통해 실행됩니다. 게임 폴더에 DirectX용 ReShade(dxgi.dll)가 있으면 작동하지 않습니다.");
        cog.AddNote("게임 자체 프레임 생성을 끄고, HSF를 OptiScaler의 프레임 생성과 함께 사용하지 마세요.");
        cog.AddWarning("온라인 안티치트가 있는 게임에서는 절대 HSF를 사용하지 마세요.");

        var addonDir = HsfService.AddonDir(installPath);
        cog.AddButtons(
            cog.MakeButton("GitHub 페이지 열기", CogButtonKind.Secondary,
                async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri(HsfService.ReleasesPage)); }),
            cog.MakeOpenFileButton("HSF.ini 열기", isInstalled ? Path.Combine(addonDir, HsfService.IniName) : null),
            cog.MakeOpenFileButton("도우미 로그 열기", isInstalled ? Path.Combine(addonDir, HsfService.StreamlineFolder, "hsf_dlssg.log") : null));
        await cog.ShowAsync();
    }
}
