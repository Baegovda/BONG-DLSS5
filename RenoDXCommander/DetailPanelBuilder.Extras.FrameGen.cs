// DetailPanelBuilder.Extras.FrameGen.cs — Extras section: DLSSG 20/30 and Universal MFG Unlocker rows with their DLL pickers.

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
    private static readonly string[] MfgDllNames =
    {
        "version.dll", "dinput8.dll", "winmm.dll", "d3d9.dll", "d3d10.dll",
        "d3d11.dll", "d3d12.dll", "dxgi.dll", "dsound.dll", "wininet.dll",
        "winhttp.dll", "binkw64.dll", "bink2w64.dll", "xinput1_1.dll",
        "xinput1_2.dll", "xinput1_3.dll", "xinput1_4.dll", "xinput9_1_0.dll",
        "xinputuap.dll",
    };

    /// <summary>
    /// Warns that XeMFG standalone doesn't start while ReShade is installed. Returns true to continue.
    /// "Don't show this again" is saved even when the user cancels.
    /// </summary>
    private async System.Threading.Tasks.Task<bool> ShowUmfgReShadeWarningAsync(XamlRoot? xamlRoot)
    {
        xamlRoot ??= _window.Content.XamlRoot;
        if (xamlRoot == null) return true;

        var dontShowCheck = new CheckBox
        {
            Content = "다시 표시 안 함",
            FontSize = 12,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var content = new StackPanel { Spacing = 4 };
        content.Children.Add(new TextBlock
        {
            Text = "이 게임에 ReShade가 설치되어 있습니다. Universal MFG Unlocker의 XeSS 프레임 생성(메뉴의 \"XeMFG standalone\")은 "
                 + "ReShade가 로드된 동안 시작되지 않으며, 모드 메뉴에 \"Waiting menu_factory_not_dxgi\"가 표시됩니다. "
                 + "ReShade가 DLL로 설치되었든 ASI로 설치되었든 마찬가지입니다.\n\n"
                 + "DLSS 멀티 프레임 생성은 정상적으로 작동합니다. ReShade와 함께 XeMFG를 사용하려면 OptiScaler(나이틀리, F5 또는 DLSS NR)를 설치하고 "
                 + "OptiScaler 설정에서 FG Output을 XeFG로 설정하세요. 그러면 RHI가 모드를 OptiScaler 플러그인으로 설치합니다.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 22,
            Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
        });
        content.Children.Add(dontShowCheck);

        var dialog = new ContentDialog
        {
            Title = "XeMFG와 ReShade",
            Content = content,
            PrimaryButtonText = "계속",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        var result = await DialogService.ShowSafeAsync(dialog);
        if (dontShowCheck.IsChecked == true)
        {
            _window.ViewModel.Settings.UmfgReShadeWarningDismissed = true;
            _window.ViewModel.SaveSettingsPublic();
        }
        return result == ContentDialogResult.Primary;
    }

    private void BuildDlssg2030Row(GameCardViewModel card, StackPanel body)
    {
        _window.ViewModel.SetLastUiAction($"BuildDlssg2030Row({card.GameName})");
        var svc         = App.Services.GetRequiredService<Dlssg20_30Service>();
        var gameName    = card.GameName;
        var store       = card.Source ?? "";
        var installPath = card.InstallPath ?? "";

        var currentDllName = _window.ViewModel.GetDlssg2030InstalledAs(gameName, store);
        bool isInstalled   = svc.IsInstalledIn(installPath, currentDllName);
        // Use cached state from card instead of File.Exists on UI thread
        bool addonConflict = card.MfgAdaInstalled;

        string statusText  = isInstalled ? (svc.StagedVersion ?? "Installed") : "Ready";
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
            Text = "20/30 FG Unlock",
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(label, "20/30 FG Unlock — RTX 20 및 30 시리즈 GPU에서 DLSS 프레임 생성(최대 6X)을 활성화합니다. D3D12. NVIDIA 드라이버 R580 이상이 필요합니다. ASI Loader나 ReShade는 필요 없습니다.");
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        // Col 1 — status (version SHA when installed)
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
            ToolTipService.SetToolTip(statusBlock, $"설치된 이름: {currentDllName}\n클릭하면 GitHub 릴리스가 열립니다");
            statusBlock.PointerPressed += (s, e) =>
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/sdli1995/dlssg_for_sm86"));
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
        ToolTipService.SetToolTip(infoBtn, "20/30 FG Unlock GitHub 페이지 열기");
        infoBtn.Click += (s, e) =>
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/sdli1995/dlssg_for_sm86"));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        // Col 3 — Install button
        var installBtn = new Button
        {
            Content = isInstalled ? "↺  Reinstall 20/30 FG" : "⬇  Install 20/30 FG",
            FontSize = 12,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(8),
            Background = isInstalled ? UIFactory.GetBrush("#182840") : UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = isInstalled ? UIFactory.GetBrush("#7AACDD") : UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = isInstalled ? UIFactory.GetBrush("#2A4468") : UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
        };

        if (addonConflict)
        {
            installBtn.IsEnabled = false;
            installBtn.Opacity   = 0.35;
            installBtn.Content   = KoreanBuild.Tr("Uninstall MFG Unlock first");
            ToolTipService.SetToolTip(installBtn, "MFG Unlock(애드온)이 설치되어 있어 충돌합니다. 먼저 애드온 선택기에서 제거하세요.");
        }
        else
        {
            ToolTipService.SetToolTip(installBtn, isInstalled
                ? $"20/30 FG Unlock 재설치 (현재 {currentDllName}(으)로 배포됨)"
                : "20/30 FG Unlock 설치 — 선택한 프록시 DLL, dlssg_sm86.ini, dlssg_sm86 폴더를 배포합니다");
        }

        installBtn.Click += async (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;

            var st = _window.ViewModel.Settings;
            if (!isInstalled && !await ShowClosedSourceNoticeAsync("20/30 FG Unlock", "sdli1995",
                    () => st.Dlssg2030ClosedSourceDismissed, () => st.Dlssg2030ClosedSourceDismissed = true, installBtn.XamlRoot ?? _window.Content.XamlRoot))
                return;

            var chosen = await ShowDlssg2030DllPickerAsync(card, currentDllName);
            if (chosen == null) return;

            installBtn.IsEnabled = false;
            installBtn.Content   = KoreanBuild.Tr("Installing...");
            try
            {
                // Only one proxy may be in the folder: if reinstalling under a different name,
                // put the old slot back first (the ini and dlssg_sm86\ folder are reused)
                if (isInstalled && !string.IsNullOrEmpty(currentDllName)
                    && !currentDllName.Equals(chosen, StringComparison.OrdinalIgnoreCase))
                    AuxInstallService.SentinelRestore(Path.Combine(installPath, currentDllName));

                bool ok = await Task.Run(async () =>
                {
                    if (!svc.IsStagingReady || svc.HasUpdate)
                        await svc.EnsureStagingAsync().ConfigureAwait(false);
                    return svc.Install(installPath, chosen);
                });

                if (ok)
                {
                    _window.ViewModel.SetDlssg2030InstalledAs(gameName, chosen, store);
                    RequestExtrasRebuild(card);
                }
                else
                {
                    installBtn.Content   = KoreanBuild.Tr("Download failed — try again");
                    installBtn.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildDlssg2030Row] Install failed — {ex.Message}");
                installBtn.Content   = KoreanBuild.Tr("Install failed");
                installBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        // Col 4 — Cog button
        var cogBtn = CogDialog.MakeCogButton("20/30 FG Unlock 설정");
        cogBtn.Click += async (s, e) =>
        {
            try { await ShowDlssg2030SettingsAsync(installPath, isInstalled); }
            catch (Exception ex) { CrashReporter.Log($"[BuildDlssg2030Row] Settings dialog failed — {ex.Message}"); }
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
        ToolTipService.SetToolTip(removeBtn, "이 게임에서 20/30 FG Unlock 제거");
        removeBtn.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            svc.Uninstall(installPath, currentDllName);
            _window.ViewModel.SetDlssg2030InstalledAs(gameName, null, store);
            RequestExtrasRebuild(card);
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    private async Task<string?> ShowDlssg2030DllPickerAsync(GameCardViewModel card, string? currentDllName)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return null;

        string? chosen = null;

        var listPanel    = new StackPanel { Spacing = 4 };
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 360,
            Content   = listPanel,
        };

        foreach (var option in GetExtraDllOptions(card, Dlssg20_30Service.ProxyNames, currentDllName))
        {
            var name            = option.Name;
            bool isRecommended  = option.IsRecommended;
            bool isRhiOwned     = option.BlockedBy != null;
            bool isCurrent      = option.IsCurrent;

            var btn = new Button
            {
                HorizontalAlignment        = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding         = new Thickness(10, 6, 10, 6),
                CornerRadius    = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                IsEnabled       = !isRhiOwned,
                Opacity         = isRhiOwned ? 0.4 : 1.0,
                Background  = isCurrent ? UIFactory.Brush(ResourceKeys.AccentBlueBgBrush) : UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush),
                BorderBrush = isCurrent ? UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush) : UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            };

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
            AddExtraDllBadges(contentRow, btn, option, "20/30 FG Unlock");
            if (isCurrent)
                contentRow.Children.Add(MakeBadge("현재", "#182840", "#7AACDD", "#2A4468"));

            btn.Content = contentRow;

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
            Title = "20/30 FG Unlock DLL 이름 선택",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "게임이 불러올 프록시 DLL을 선택하세요. version.dll부터 시작하고, 게임에 프레임 생성이 나타나지 않으면 목록의 다음 이름으로 재설치하세요. dxgi.dll과 d3d12.dll은 최후의 수단이며 Vulkan 게임에서는 작동하지 않습니다.",
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

    private void BuildRtx40MfgRow(GameCardViewModel card, StackPanel body)
    {
        _window.ViewModel.SetLastUiAction($"BuildRtx40MfgRow({card.GameName})");
        var mfgSvc       = App.Services.GetRequiredService<Rtx40MfgService>();
        var gameName     = card.GameName;
        var store        = card.Source ?? "";
        var installPath  = card.InstallPath ?? "";

        // Use cached state from card instead of File.Exists on UI thread
        bool isInstalled   = card.Rtx40MfgInstalled;
        bool addonConflict = card.MfgAdaInstalled;
        var currentDllName = _window.ViewModel.GetRtx40MfgInstalledAs(gameName, store);
        bool isPluginInstall = isInstalled && Rtx40MfgService.IsPluginInstall(currentDllName); // loaded by OptiScaler
        bool isHsfMods       = isInstalled && Rtx40MfgService.IsHsfModsInstall(currentDllName); // loaded by HSF's helper
        bool hasHsf          = !string.IsNullOrEmpty(installPath)
            && (HsfService.IsInstalledIn(installPath, false) || HsfService.IsInstalledIn(installPath, true));

        // Status
        string statusText  = isInstalled ? (mfgSvc.StagedVersion ?? "Installed") : "Ready";
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
            Text = "Universal MFG Unlocker",
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap, // long name; wraps instead of clipping in the 120 px column
        };
        ToolTipService.SetToolTip(label, "Universal MFG Unlocker(이전 RTX 40 MFG Unlock) — RTX 40 및 RTX 30 시리즈 GPU(RTX 20은 실험적)에서 2x를 넘는(최대 6x) DLSS 멀티 프레임 생성과, 모든 GPU에서 XeSS 프레임 생성(XeMFG)을 제공합니다. 단독 DLL이며 ASI Loader가 필요 없습니다. RHI는 XeMFG에 필요한 Intel XeSS 파일도 설치합니다.");
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
            ToolTipService.SetToolTip(statusBlock, $"설치된 이름: {currentDllName}\n클릭하면 GitHub 릴리스 페이지가 열립니다");
            statusBlock.PointerPressed += (s, e) =>
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dashdogy/MFG-Unlocker/releases"));
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
        ToolTipService.SetToolTip(infoBtn, "Universal MFG Unlocker GitHub 페이지 열기");
        infoBtn.Click += (s, e) =>
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dashdogy/MFG-Unlocker"));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        // Col 3 — Install button
        var installBtn = new Button
        {
            Content = isInstalled ? "↺  Reinstall Universal MFG Unlocker" : "⬇  Install Universal MFG Unlocker",
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

        if (addonConflict)
        {
            installBtn.IsEnabled = false;
            installBtn.Opacity   = 0.35;
            installBtn.Content   = KoreanBuild.Tr("Uninstall MFG Unlock first");
            ToolTipService.SetToolTip(installBtn, "MFG Unlock(애드온)이 이미 설치되어 있어 Universal MFG Unlocker와 충돌합니다. 먼저 애드온 선택기에서 제거하세요.");
        }
        else
        {
            // Cheap check for the tooltip only; the click handler checks OptiScaler's install record
            bool osPluginLikely = card.IsOsInstalled && !string.IsNullOrEmpty(installPath)
                && System.IO.Directory.Exists(System.IO.Path.Combine(installPath, "OptiScaler"));
            ToolTipService.SetToolTip(installBtn,
                isHsfMods ? "HSF_streamline\\mods에 Universal MFG Unlocker 재설치 (HSF 도우미가 불러옴)"
                : isPluginInstall ? $"Universal MFG Unlocker 재설치 (OptiScaler가 {currentDllName}에서 불러옴)"
                : osPluginLikely ? (isInstalled
                    ? $"Universal MFG Unlocker를 재설치합니다. OptiScaler가 설치되어 있으므로 {currentDllName}에서 OptiScaler\\plugins로 이동합니다."
                    : "제작자가 OptiScaler와 함께 쓸 때 권장하는 대로 Universal MFG Unlocker를 OptiScaler 플러그인(OptiScaler\\plugins\\MFGUnlocker.asi)으로 설치합니다.")
                : isInstalled ? $"Universal MFG Unlocker 재설치 (현재 {currentDllName}(으)로 배포됨)"
                : "Universal MFG Unlocker 설치 — 선택한 이름으로 잠금 해제 DLL을 배포합니다");
        }

        installBtn.Click += async (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            var vm = _window.ViewModel;

            // With an OptiScaler 10 build installed by RHI the mod goes in OptiScaler\plugins (no name to pick).
            // Read the live location too: it can have moved since this panel was built.
            bool asPlugin    = await Task.Run(() => vm.IsOptiScalerPluginHost(card));
            var liveDllName  = vm.GetRtx40MfgInstalledAs(gameName, store);
            bool liveInstalled = !string.IsNullOrEmpty(liveDllName) && mfgSvc.IsInstalledIn(installPath, liveDllName);

            // Installed for HSF's helper (HSF_streamline\mods), or a 32-bit game with HSF (HSF's guide: never next to
            // a 32-bit exe): into HSF_streamline\mods, no name to pick
            if ((liveInstalled && Rtx40MfgService.IsHsfModsInstall(liveDllName)) || (hasHsf && card.Is32Bit))
            {
                installBtn.IsEnabled = false;
                installBtn.Content   = KoreanBuild.Tr("Installing...");
                var hsfError = await vm.InstallMfgForHsfAsync(card, null);
                if (hsfError != null) CrashReporter.Log($"[BuildRtx40MfgRow] HSF mods reinstall failed — {hsfError}");
                RequestExtrasRebuild(card);
                return;
            }

            // XeMFG standalone can't start while ReShade hooks DXGI (the mod reports menu_factory_not_dxgi).
            // Through OptiScaler it works, so no warning there.
            if (!asPlugin && card.IsRsInstalled && !card.RequiresVulkanInstall
                && !vm.Settings.UmfgReShadeWarningDismissed
                && !await ShowUmfgReShadeWarningAsync(installBtn.XamlRoot))
                return;

            string? chosen;
            if (asPlugin)
                chosen = Rtx40MfgService.PluginRelPath;
            else
            {
                chosen = await ShowMfgDllPickerAsync(card, Rtx40MfgService.IsPluginInstall(liveDllName) ? null : liveDllName);
                if (chosen == null) return;
            }

            installBtn.IsEnabled = false;
            installBtn.Content   = KoreanBuild.Tr("Installing...");
            try
            {
                // Proxy → plugin: move it so the old proxy name is remembered for when OptiScaler is removed
                if (asPlugin && liveInstalled && !Rtx40MfgService.IsPluginInstall(liveDllName))
                {
                    var from  = liveDllName!;
                    var moved = await Task.Run(() => mfgSvc.MoveToOptiScalerPlugin(installPath, from));
                    if (moved != null) liveDllName = moved;
                }

                // If reinstalling under a different name, remove the old one first
                if (liveInstalled && !string.IsNullOrEmpty(liveDllName)
                    && !liveDllName.Equals(chosen, StringComparison.OrdinalIgnoreCase))
                    mfgSvc.Uninstall(installPath, liveDllName);

                bool ok = await Task.Run(async () =>
                {
                    if (!mfgSvc.IsStagingReady || mfgSvc.HasUpdate)
                        await mfgSvc.EnsureStagingAsync().ConfigureAwait(false);
                    if (!asPlugin) // standalone XeMFG; through OptiScaler it uses OptiScaler's XeSS files
                        await mfgSvc.EnsureXessStagingAsync().ConfigureAwait(false);
                    return mfgSvc.Install(installPath, chosen);
                });

                if (ok)
                {
                    _window.ViewModel.SetRtx40MfgInstalledAs(gameName, chosen, store);
                    card.SetMfgState(mfgAdaInstalled: card.MfgAdaInstalled, mfgAdaRtx40Conflict: card.MfgAdaInstalled, rtx40MfgInstalled: true);
                    RequestExtrasRebuild(card);
                }
                else
                {
                    installBtn.Content   = KoreanBuild.Tr("Download failed — try again");
                    installBtn.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildRtx40MfgRow] Install failed — {ex.Message}");
                installBtn.Content   = KoreanBuild.Tr("Install failed");
                installBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        // Col 4 — Cog button
        var cogBtn = CogDialog.MakeCogButton("Universal MFG Unlocker 설정");
        cogBtn.Click += async (s, e) =>
        {
            try
            {
                var cog = new CogDialog("Universal MFG Unlocker", _window.Content.XamlRoot);
                cog.AddIntro("게임 중 Backspace를 누르면 Universal MFG Unlocker 메뉴가 열립니다. 제공되는 모드:");
                cog.AddNote("• Follow game: 게임 자체 MFG 설정 사용");
                cog.AddNote("• Fixed 2x~6x: 특정 배수 강제");
                cog.AddNote("• Dynamic: 디스플레이 주사율 또는 사용자 지정 FPS 값을 목표로 함");
                cog.AddIntro("2x 초과에서 프레임이 멈추면 이 게임의 DLSS / Streamline 섹션에서 프레임 생성 프리셋을 B로 설정하세요.");
                if (isInstalled)
                    cog.AddNote($"현재 설치된 이름: {currentDllName}");

                if (isPluginInstall)
                {
                    cog.AddSection("OptiScaler가 불러옴");
                    cog.AddIntro("제작자의 권장대로 OptiScaler가 모드를 플러그인으로 불러옵니다. 프레임 생성은 여기가 아니라 OptiScaler에서 선택합니다. "
                        + "OptiScaler ⚙를 열고 DLSS 멀티 프레임 생성은 FG Output을 DLSSG로, XeMFG는 XeFG로 설정하세요. "
                        + "OptiScaler 기본값(Auto)은 프레임 생성을 실행하지 않습니다.");
                    cog.AddNote("DLSS 프리셋과 DLSS 5를 위해 모드 자체 메뉴도 OptiScaler 메뉴 옆에 계속 열립니다. OptiScaler를 제거하면 RHI가 모드를 게임 exe 옆으로 다시 옮깁니다.");
                }

                if (isHsfMods || hasHsf)
                {
                    cog.AddSection("HSF와 함께 사용");
                    cog.AddIntro(isHsfMods
                        ? "HSF_streamline\\mods에 설치됨: HSF 도우미가 그곳에서 언락커를 불러옵니다(32비트 게임). "
                          + "배수는 ReShade 오버레이의 HSF 탭에서 고르세요. HSF를 제거하면 이 사본도 제거됩니다."
                        : "이 게임에 HSF가 설치되어 있습니다. 배수는 ReShade 오버레이의 HSF 탭에서 고르세요.");
                    cog.AddNote("RTXMFG-XeSS 폴더 없음: HSF의 프레임 생성은 DLSS이므로 XeMFG를 사용하지 않습니다.");
                }

                cog.AddSection("XeSS 프레임 생성 (XeMFG)");
                if (isHsfMods || hasHsf)
                    cog.AddIntro("HSF가 있는 게임에는 설치되지 않습니다. 대신 XeMFG를 사용하려면 HSF를 제거하고 모드를 재설치하세요.");
                else if (isPluginInstall)
                    cog.AddIntro("OptiScaler를 통해 사용할 때 XeMFG는 OptiScaler의 FG Output을 XeFG로 설정한 것입니다. OptiScaler가 설치하는 XeSS 파일을 사용하므로 다른 것은 필요 없습니다.");
                else
                    cog.AddIntro("이 모드는 모든 GPU에서 Intel XeSS 프레임 생성(2x~6x, 실험적)도 사용할 수 있습니다. 게임 내 메뉴에서 \"XeMFG standalone\"을 체크하고 게임을 다시 시작하세요. "
                        + "모드 옆 RTXMFG-XeSS 폴더에 Intel의 libxess_fg.dll과 libxell.dll이 필요하며, RHI가 모드와 함께 설치합니다.");
                if (isInstalled && !isPluginInstall && !isHsfMods && !hasHsf && !string.IsNullOrEmpty(installPath))
                {
                    var xessDir = System.IO.Path.Combine(installPath, Rtx40MfgService.XessFolderName);
                    bool xessPresent = System.IO.File.Exists(System.IO.Path.Combine(xessDir, "libxess_fg.dll"))
                                    && System.IO.File.Exists(System.IO.Path.Combine(xessDir, "libxell.dll"));
                    if (xessPresent)
                        cog.AddNote("XeSS 파일: 설치됨.");
                    else
                        cog.AddWarning("XeSS 파일: 없음. 추가하려면 모드를 재설치하세요.");
                }
                if (string.Equals(card.RsInstalledFile, "dxgi.dll", StringComparison.OrdinalIgnoreCase))
                    cog.AddWarning("이 게임에는 ReShade가 dxgi.dll로 설치되어 있습니다. XeMFG 5x 및 6x에서는 일부 생성 프레임이 화면에 표시되지 않을 수 있습니다. "
                        + "그런 경우 게임 재정의 › DLL 이름 재정의에서 ReShade에 다른 DLL 이름을 지정하세요.");
                cog.AddButtons(cog.MakeButton("GitHub 페이지 열기", CogButtonKind.Secondary,
                    async () => { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dashdogy/MFG-Unlocker")); }));
                await cog.ShowAsync();
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildRtx40MfgRow] Settings dialog failed — {ex.Message}");
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
        ToolTipService.SetToolTip(removeBtn, "이 게임에서 Universal MFG Unlocker 제거");
        removeBtn.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            // Live location: it may have moved in or out of OptiScaler\plugins since the panel was built
            mfgSvc.Uninstall(installPath, _window.ViewModel.GetRtx40MfgInstalledAs(gameName, store) ?? currentDllName);
            _window.ViewModel.SetRtx40MfgInstalledAs(gameName, null, store);
            card.SetMfgState(mfgAdaInstalled: card.MfgAdaInstalled, mfgAdaRtx40Conflict: false, rtx40MfgInstalled: false);
            RequestExtrasRebuild(card);
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    private async Task<string?> ShowMfgDllPickerAsync(GameCardViewModel card, string? currentDllName)
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

        foreach (var option in GetExtraDllOptions(card, MfgDllNames, currentDllName))
        {
            var name            = option.Name;
            bool isRecommended  = option.IsRecommended;
            bool isRhiOwned     = option.BlockedBy != null;
            bool isDxgiConflict = string.Equals(name, "dxgi.dll", StringComparison.OrdinalIgnoreCase)
                               && !string.IsNullOrEmpty(card.RsInstalledFile);
            bool isCurrent      = option.IsCurrent;

            var btn = new Button
            {
                HorizontalAlignment        = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding         = new Thickness(10, 6, 10, 6),
                CornerRadius    = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                IsEnabled       = !isRhiOwned,
                Opacity         = isRhiOwned ? 0.4 : 1.0,
                Background  = isCurrent ? UIFactory.Brush(ResourceKeys.AccentBlueBgBrush) : UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush),
                BorderBrush = isCurrent ? UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush) : UIFactory.Brush(ResourceKeys.BorderDefaultBrush),
            };

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
            AddExtraDllBadges(contentRow, btn, option, "Universal MFG Unlocker");
            if (!isRhiOwned && isDxgiConflict)
                contentRow.Children.Add(MakeBadge("ReShade/OS와 충돌 가능", "#2A1A10", "#CC9955", "#5A3A18"));
            if (isCurrent)
                contentRow.Children.Add(MakeBadge("현재", "#182840", "#7AACDD", "#2A4468"));

            btn.Content = contentRow;

            if (!isRhiOwned && isDxgiConflict)
                ToolTipService.SetToolTip(btn, "ReShade나 OptiScaler도 이 이름을 사용하면 dxgi.dll이 충돌할 수 있습니다.");

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
            Title = "Universal MFG Unlocker DLL 이름 선택",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Universal MFG Unlocker DLL을 배포할 파일 이름을 선택하세요. 게임이 일찍 불러오는 이름을 고르고, 다른 모드가 이미 사용 중인 이름은 피하세요.",
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
