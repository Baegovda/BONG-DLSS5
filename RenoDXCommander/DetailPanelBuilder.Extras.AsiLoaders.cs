// DetailPanelBuilder.Extras.AsiLoaders.cs — Extras section: Ultimate ASI Loader and RTX Encore rows with their DLL pickers.

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
    private void BuildUalRow(GameCardViewModel card, StackPanel body)
    {
        _window.ViewModel.SetLastUiAction($"BuildUalRow({card.GameName})");
        var ualSvc    = _window.ViewModel.UalServiceInstance;
        var gameName  = card.GameName;
        var store     = card.Source ?? "";
        var installPath = card.InstallPath ?? "";

        // Detect current install state
        var ualRecord   = string.IsNullOrEmpty(installPath) ? null
            : _auxInstallService.FindRecord(gameName, installPath, UltimateAsiLoaderService.AddonType);
        bool isInstalled = ualRecord != null;
        string? installedAs = ualRecord?.InstalledAs;

        // Consistency guard: if the dict says installed but FindRecord finds no record,
        // the aux record was lost (e.g. Game Pass path version change). Clear the stale dict entry
        // so the collapsed header doesn't show "ASI Loader winmm.dll" when nothing is actually installed.
        // Not while an install folder move is waiting: the record still points at the old folder.
        if (!isInstalled && !string.IsNullOrEmpty(_window.ViewModel.GetUalInstalledAs(gameName, store))
            && !_window.ViewModel.IsFolderMigrationPending(card))
        {
            CrashReporter.Log($"[BuildUalRow] Clearing stale UalInstalledAs entry for '{gameName}' — dict said installed but no aux record found");
            _window.ViewModel.SetUalInstalledAs(gameName, null, store);
        }

        // Status text
        string statusText;
        string statusColor;
        if (isInstalled)
        {
            var staged = card.Is32Bit ? ualSvc.StagedVersion32 : ualSvc.StagedVersion64;
            statusText  = staged ?? "설치됨";
            statusColor = "#5ECB7D";
        }
        else
        {
            statusText  = "Ready";
            statusColor = "#A0AABB";
        }

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
            Text = "ASI Loader",
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(label, "Ultimate ASI Loader — 게임 프로세스에 .asi 플러그인을 로드하는 프록시 DLL입니다.");
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
            ToolTipService.SetToolTip(statusBlock, $"설치된 이름: {installedAs}\n클릭하여 GitHub 릴리스 열기");
            statusBlock.PointerPressed += (s, e) =>
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/ThirteenAG/Ultimate-ASI-Loader/releases"));
            AttachLinkCursor(statusBlock);
        }
        Grid.SetColumn(statusBlock, 1);
        row.Children.Add(statusBlock);

        // Col 2 — Info button (matches Components style)
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
        ToolTipService.SetToolTip(infoBtn, "Ultimate ASI Loader GitHub 릴리스 페이지 열기");
        infoBtn.Click += (s, e) =>
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/ThirteenAG/Ultimate-ASI-Loader/releases"));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        // Col 3 — Install button
        var installBtn = new Button
        {
            Content = isInstalled ? "↺  ASI Loader 재설치" : "⬇  ASI Loader 설치",
            FontSize = 12,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
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
        ToolTipService.SetToolTip(installBtn, isInstalled
            ? $"Ultimate ASI Loader 재설치 (현재 '{installedAs}')"
            : "Ultimate ASI Loader 설치 — 사용할 DLL 이름 선택");
        installBtn.Click += async (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            var chosen = await ShowUalDllPickerAsync(card, ualRecord?.InstalledAs);
            if (chosen == null) return;

            installBtn.IsEnabled = false;
            installBtn.Content = "설치 중...";
            try
            {
                var previousName = ualRecord?.InstalledAs;
                var (success, hookedOriginal) = await ualSvc.InstallAsync(card, chosen);
                if (success)
                {
                    _window.ViewModel.SetUalInstalledAs(gameName, chosen, store);
                    // Moving to a new name put the old name's XHooked.dll back; an extra there follows it
                    if (!string.IsNullOrEmpty(previousName) && !previousName.Equals(chosen, StringComparison.OrdinalIgnoreCase))
                        _window.ViewModel.OnUalReleasedName(card, previousName);
                    if (hookedOriginal != null)
                    {
                        var chainedExtra = _window.ViewModel.OnUalChainedFile(card, chosen, hookedOriginal);
                        _ = DialogService.ShowSafeAsync(new ContentDialog
                        {
                            Title = chainedExtra != null ? "추가 파일 이름 변경됨" : "원본 DLL 체인 연결됨",
                            Content = chainedExtra != null
                                ? $"ASI Loader가 로드할 수 있도록 {chainedExtra}의 이름을 '{hookedOriginal}'(으)로 변경했습니다. ASI Loader와 함께 계속 작동합니다."
                                : $"ASI Loader가 자동으로 체인 로드할 수 있도록 기존 '{chosen}'의 이름을 '{hookedOriginal}'(으)로 변경했습니다.",
                            CloseButtonText = "확인",
                            XamlRoot = _window.Content.XamlRoot,
                        });
                    }
                    RequestExtrasRebuild(card);
                }
                else
                {
                    installBtn.Content = "❌ 설치 실패";
                }
            }
            finally { installBtn.IsEnabled = true; }
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        // Col 4 — Cog (empty for now, matches Components cog size/position)
        var cogBtn = CogDialog.MakeCogButton("ASI Loader 설정");
        cogBtn.Tag = card;
        cogBtn.Click += async (s, e) =>
        {
            try
            {
                var cog = new CogDialog("ASI Loader", _window.Content.XamlRoot);
                cog.AddEmpty();
                await cog.ShowAsync();
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[DetailPanelBuilder.BuildUalRow] Settings dialog failed — {ex.Message}");
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
        ToolTipService.SetToolTip(removeBtn, "이 게임에서 Ultimate ASI Loader 제거");
        removeBtn.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            var removedName = ualRecord?.InstalledAs ?? _window.ViewModel.GetUalInstalledAs(gameName, store);
            ualSvc.Uninstall(card);
            _window.ViewModel.SetUalInstalledAs(gameName, null, store);
            _window.ViewModel.OnUalReleasedName(card, removedName);
            RequestExtrasRebuild(card);
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }

    private async Task<string?> ShowUalDllPickerAsync(GameCardViewModel card, string? currentDllName)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return null;

        var names = card.Is32Bit
            ? UltimateAsiLoaderService.Win32Names
            : UltimateAsiLoaderService.Win64Names;

        // Collect files already in the game folder
        var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (Directory.Exists(card.InstallPath))
                foreach (var f in Directory.GetFiles(card.InstallPath, "*.dll"))
                    existingFiles.Add(Path.GetFileName(f));
        }
        catch (Exception ex) { CrashReporter.Log($"[DetailPanelBuilder.ShowUalDllPickerAsync] Failed to list DLLs in '{card.InstallPath}' — {ex.Message}"); }

        // RHI-managed filenames to flag as conflict
        var rhiOwned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(card.RsInstalledFile))  rhiOwned.Add(card.RsInstalledFile);
        if (!string.IsNullOrEmpty(card.OsInstalledFile))  rhiOwned.Add(card.OsInstalledFile);
        if (!string.IsNullOrEmpty(card.DcInstalledFile))  rhiOwned.Add(card.DcInstalledFile);

        string? chosen = null;

        var listPanel = new StackPanel { Spacing = 4 };
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 420,
            Content = listPanel,
        };

        foreach (var name in names)
        {
            bool isRecommended = UltimateAsiLoaderService.RecommendedNames.Contains(name, StringComparer.OrdinalIgnoreCase);
            bool isRhiConflict = UltimateAsiLoaderService.RhiConflictNames.Contains(name, StringComparer.OrdinalIgnoreCase);
            bool isTaken       = existingFiles.Contains(name) && !rhiOwned.Contains(name) && name != currentDllName;
            bool isRhiOwned    = rhiOwned.Contains(name);
            bool isCurrent     = string.Equals(name, currentDllName, StringComparison.OrdinalIgnoreCase);

            var btn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 6, 10, 6),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                IsEnabled = !isRhiOwned,
                Opacity = isRhiOwned ? 0.4 : 1.0,
            };

            // Styling
            if (isCurrent)
            {
                btn.Background   = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush);
                btn.BorderBrush  = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush);
            }
            else
            {
                btn.Background  = UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush);
                btn.BorderBrush = UIFactory.Brush(ResourceKeys.BorderDefaultBrush);
            }

            // Content: name + badges
            var contentRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            contentRow.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 12,
                Foreground = isTaken || isRhiOwned
                    ? UIFactory.Brush(ResourceKeys.TextTertiaryBrush)
                    : UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
                VerticalAlignment = VerticalAlignment.Center,
            });

            if (isRecommended)
                contentRow.Children.Add(MakeBadge("권장", "#1A3A20", "#6AE87A", "#2A5A30"));
            if (isTaken)
                contentRow.Children.Add(MakeBadge("사용 중", "#2A1818", "#CC6666", "#5A2828"));
            if (isRhiOwned)
                contentRow.Children.Add(MakeBadge("RHI에서 사용 중", "#2A1818", "#CC6666", "#5A2828"));
            if (isRhiConflict && !isRhiOwned)
                contentRow.Children.Add(MakeBadge("ReShade/OS와 충돌 가능", "#2A1A10", "#CC9955", "#5A3A18"));
            if (isCurrent)
                contentRow.Children.Add(MakeBadge("현재", "#182840", "#7AACDD", "#2A4468"));

            btn.Content = contentRow;

            // Tooltip for taken files
            if (isTaken)
                ToolTipService.SetToolTip(btn, $"게임 폴더에 '{name}'이(가) 이미 있습니다. 선택하면 ASI Loader가 체인 로드할 수 있도록 기존 파일의 이름을 '{Path.GetFileNameWithoutExtension(name)}Hooked.dll'(으)로 변경합니다.");
            else if (isRhiOwned)
                ToolTipService.SetToolTip(btn, "이 파일 이름은 RHI가 관리하는 구성 요소(ReShade, OptiScaler 또는 DC)에서 이미 사용 중입니다. 다른 이름을 선택하세요.");

            btn.Tag = name;
            btn.Click += (s, ev) =>
            {
                chosen = (s as Button)?.Tag as string;
                // Close the dialog by finding and closing it
                if (s is FrameworkElement fe)
                {
                    var dialog = FindParentContentDialog(fe);
                    dialog?.Hide();
                }
            };

            listPanel.Children.Add(btn);
        }

        var dialog = new ContentDialog
        {
            Title = "ASI Loader DLL 이름 선택",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "ASI Loader의 파일 이름을 선택하세요. 대부분의 게임은 version.dll 또는 winmm.dll로 작동합니다.",
                        FontSize = 11,
                        Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    scrollViewer,
                }
            },
            CloseButtonText = "취소",
            XamlRoot = _window.Content.XamlRoot,
        };

        await DialogService.ShowSafeAsync(dialog);
        return chosen;
    }

    // ── RTX Encore ────────────────────────────────────────────────────────────

    private void BuildRtxEncoreRow(GameCardViewModel card, StackPanel body)
    {
        _window.ViewModel.SetLastUiAction($"BuildRtxEncoreRow({card.GameName})");
        var svc         = App.Services.GetRequiredService<RtxEncoreService>();
        var gameName    = card.GameName;
        var store       = card.Source ?? "";
        var installPath = card.InstallPath ?? "";

        var currentDllName = _window.ViewModel.GetRtxEncoreInstalledAs(gameName, store);
        bool isInstalled   = svc.IsInstalledIn(installPath, currentDllName);
        bool nrDeployed    = _window.ViewModel.GetRtxEncoreNrDllDeployed(gameName, store);

        string statusText  = isInstalled ? (svc.StagedVersion ?? "설치됨") : "Ready";
        string statusColor = isInstalled ? "#5ECB7D" : "#A0AABB";

        _window.ViewModel.SetLastUiAction($"BuildRtxEncoreRow:BuildGrid({card.GameName})");

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelColW(1, 0, 350, _window.ExtrasContainer.ActualWidth)) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        var label = new TextBlock { Text = "RTX Encore", FontSize = 12, Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush), VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(label, "RTX Encore — RTX 20/30/40용 프레임 생성 X2-X6, Smooth Motion 및 Neural Rendering.");
        Grid.SetColumn(label, 0); row.Children.Add(label);

        var statusBlock = new TextBlock { Text = statusText, FontSize = 12, Foreground = UIFactory.GetBrush(statusColor), VerticalAlignment = VerticalAlignment.Center, HorizontalTextAlignment = Microsoft.UI.Xaml.TextAlignment.Center, TextDecorations = isInstalled ? Windows.UI.Text.TextDecorations.Underline : Windows.UI.Text.TextDecorations.None };
        if (isInstalled) { ToolTipService.SetToolTip(statusBlock, $"설치된 이름: {currentDllName}\n클릭하여 GitHub 릴리스 열기"); statusBlock.PointerPressed += (s, e) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(RtxEncoreService.RepoUrl + "/releases")); AttachLinkCursor(statusBlock); }
        Grid.SetColumn(statusBlock, 1); row.Children.Add(statusBlock);

        var infoBtn = new Button { Content = "정보", FontSize = 11, Padding = new Thickness(6, 2, 6, 2), Width = 36, Height = 32, Background = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush), Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush), BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
        ToolTipService.SetToolTip(infoBtn, "RTX Encore GitHub 페이지 열기");
        infoBtn.Click += (s, e) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(RtxEncoreService.RepoUrl));
        Grid.SetColumn(infoBtn, 2); row.Children.Add(infoBtn);

        string installBtnLabel = isInstalled ? "↺  RTX Encore 재설치" : "⬇  RTX Encore 설치";
        var installBtn = new Button { Content = installBtnLabel, FontSize = 12, Height = 32, HorizontalAlignment = HorizontalAlignment.Stretch, CornerRadius = new CornerRadius(8), Background = isInstalled ? UIFactory.GetBrush("#182840") : UIFactory.Brush(ResourceKeys.AccentBlueBgBrush), Foreground = isInstalled ? UIFactory.GetBrush("#7AACDD") : UIFactory.Brush(ResourceKeys.AccentBlueBrush), BorderBrush = isInstalled ? UIFactory.GetBrush("#2A4468") : UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush), BorderThickness = new Thickness(1) };
        ToolTipService.SetToolTip(installBtn, isInstalled ? $"RTX Encore 재설치 (현재 '{currentDllName}')" : "RTX Encore 설치 — 사용할 DLL 이름 선택");
        installBtn.Click += async (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            var st = _window.ViewModel.Settings;
            if (!isInstalled && !await ShowClosedSourceNoticeAsync("RTX Encore", "SilyNoMeta",
                    () => st.RtxEncoreClosedSourceDismissed, () => st.RtxEncoreClosedSourceDismissed = true, installBtn.XamlRoot ?? _window.Content.XamlRoot))
                return;
            var chosen = await ShowRtxEncoreDllPickerAsync(card, currentDllName);
            if (chosen == null) return;
            installBtn.IsEnabled = false; installBtn.Content = "설치 중...";
            try
            {
                var oldModDir = string.IsNullOrEmpty(currentDllName) ? null : svc.ModDir(installPath, currentDllName);
                bool ok = await svc.InstallAsync(installPath, chosen, currentDllName);
                if (ok)
                {
                    _window.ViewModel.SetRtxEncoreInstalledAs(gameName, chosen, store);

                    // If the user had NR DLL deployment enabled in the cog, deploy it now
                    if (_window.ViewModel.GetRtxEncoreNrDllDeployed(gameName, store))
                    {
                        var dlssS = App.Services.GetRequiredService<IDlssStreamlineService>();
                        if (await svc.EnsureNrDllCachedAsync(dlssS) != null)
                            await Task.Run(() => svc.DeployNrDll(installPath, chosen));
                        else
                            CrashReporter.Log($"[RtxEncoreInstall] NR DLL {RtxEncoreService.NrRequiredVer} not available — skipping NR deploy");
                    }

                    // Intel XeSS SDK files for the XeSS upscaler and XeFG engine: the game's own copies are
                    // replaced in place, the rest go in RTX Encore's runtime folder. Not fatal: everything else works without them.
                    var mfgSvc = App.Services.GetRequiredService<Rtx40MfgService>();
                    var newModDir = svc.ModDir(installPath, chosen);
                    installBtn.Content = mfgSvc.IsEncoreIntelStagingReady ? "XeSS 파일 설치 중..." : "XeSS 파일 다운로드 중...";
                    bool staged = await mfgSvc.EnsureEncoreIntelStagingAsync();
                    installBtn.Content = "XeSS 파일 설치 중...";
                    bool xessOk = staged && await Task.Run(() =>
                    {
                        if (oldModDir != null && !string.Equals(oldModDir, newModDir, StringComparison.OrdinalIgnoreCase))
                            mfgSvc.RemoveEncoreIntelFiles(installPath, oldModDir); // moved between the game folder and plugins\
                        return mfgSvc.DeployEncoreIntelFiles(installPath, newModDir);
                    });
                    if (!xessOk)
                        CrashReporter.Log($"[RtxEncoreInstall] XeSS files not deployed for '{installPath}' (download failed or folder not writable)");

                    RequestExtrasRebuild(card);
                }
                else { installBtn.Content = "설치 실패"; installBtn.IsEnabled = true; }
            }
            catch (Exception ex) { CrashReporter.Log($"[BuildRtxEncoreRow] Install failed — {ex.Message}"); installBtn.Content = "설치 실패"; installBtn.IsEnabled = true; }
        };
        Grid.SetColumn(installBtn, 3); row.Children.Add(installBtn);

        var cogBtn = CogDialog.MakeCogButton("RTX Encore 설정");
        cogBtn.Click += async (s, e) =>
        {
            try { await ShowRtxEncoreSettingsAsync(card, svc, installPath, isInstalled, nrDeployed); }
            catch (Exception ex) { CrashReporter.Log($"[DetailPanelBuilder.BuildRtxEncoreRow] Settings dialog failed — {ex.Message}"); }
        };
        Grid.SetColumn(cogBtn, 4); row.Children.Add(cogBtn);

        var removeBtn2 = new Button { Width = 36, Height = 32, Padding = new Thickness(0), Background = UIFactory.Brush(ResourceKeys.AccentRedBgBrush), Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush), BorderBrush = UIFactory.Brush(ResourceKeys.AccentPurpleBorderBrush), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Content = new TextBlock { Text = "✕", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush) }, Opacity = isInstalled ? 1.0 : 0, IsHitTestVisible = isInstalled };
        ToolTipService.SetToolTip(removeBtn2, "이 게임에서 RTX Encore 제거");
        removeBtn2.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(installPath) || string.IsNullOrEmpty(currentDllName)) return;
            var modDir = svc.ModDir(installPath, currentDllName); // before the DLL is gone
            svc.Uninstall(installPath, currentDllName, removeNrDll: nrDeployed);
            var mfgSvc = App.Services.GetRequiredService<Rtx40MfgService>();
            _ = Task.Run(() => mfgSvc.RemoveEncoreIntelFiles(installPath, modDir));
            _window.ViewModel.SetRtxEncoreInstalledAs(gameName, null, store);
            if (nrDeployed) _window.ViewModel.SetRtxEncoreNrDllDeployed(gameName, false, store);
            RequestExtrasRebuild(card);
        };
        Grid.SetColumn(removeBtn2, 5); row.Children.Add(removeBtn2);

        body.Children.Add(row);
        _window.ViewModel.SetLastUiAction($"BuildRtxEncoreRow:Done({card.GameName})");
    }

    private async Task<string?> ShowRtxEncoreDllPickerAsync(GameCardViewModel card, string? currentDllName)
    {
        if (string.IsNullOrEmpty(card.InstallPath)) return null;
        // Bink names already keep the game's own Bink DLL as <name>Hooked.dll, so they get no hooked option
        // The manifest can recommend another name per game (Euro Truck Simulator 2 needs winmm.dll); it's listed first
        var preferred = _window.ViewModel.GetRtxEncoreRecommendedDll(card.GameName);
        var options = GetExtraDllOptions(card, RtxEncoreService.KnownProxyNames, currentDllName,
            noHooked: n => n.StartsWith("bink", StringComparison.OrdinalIgnoreCase), preferred: preferred)
            .OrderBy(o => o.IsRecommended ? 0 : 1).ToList();
        var rhiConflicts = options.Where(o => o.BlockedBy != null)
            .ToDictionary(o => o.Name, o => o.BlockedBy!, StringComparer.OrdinalIgnoreCase);

        var onDiskConflicts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(card.InstallPath))
        {
            foreach (var name in RtxEncoreService.KnownProxyNames)
            {
                if (rhiConflicts.ContainsKey(name)) continue;
                if (string.Equals(name, currentDllName, StringComparison.OrdinalIgnoreCase)) continue;
                var diskPath = name.EndsWith(".asi", StringComparison.OrdinalIgnoreCase)
                    ? System.IO.Path.Combine(card.InstallPath, "plugins", name)
                    : System.IO.Path.Combine(card.InstallPath, name);
                if (File.Exists(diskPath))
                {
                    try { var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(diskPath); onDiskConflicts[name] = !string.IsNullOrEmpty(fvi.FileDescription) ? fvi.FileDescription : !string.IsNullOrEmpty(fvi.ProductName) ? fvi.ProductName : "기존 파일"; }
                    catch { onDiskConflicts[name] = "기존 파일"; }
                }
            }
        }

        string? chosen = null;
        var listPanel = new StackPanel { Spacing = 4 };
        var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 480, Content = listPanel };

        foreach (var option in options)
        {
            var name           = option.Name;
            bool isRecommended = option.IsRecommended;
            bool isAsi         = name.EndsWith(".asi", StringComparison.OrdinalIgnoreCase);
            bool isCurrent     = option.IsCurrent;
            bool isRhiOwned    = rhiConflicts.ContainsKey(name);
            bool isOnDisk      = onDiskConflicts.ContainsKey(name);
            var btn = new Button { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6, 10, 6), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), IsEnabled = !isRhiOwned, Opacity = isRhiOwned ? 0.4 : 1.0, Background = isCurrent ? UIFactory.Brush(ResourceKeys.AccentBlueBgBrush) : UIFactory.Brush(ResourceKeys.SurfaceOverlayBrush), BorderBrush = isCurrent ? UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush) : UIFactory.Brush(ResourceKeys.BorderDefaultBrush) };
            var contentRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            contentRow.Children.Add(new TextBlock { Text = name, FontSize = 12, Foreground = isRhiOwned ? UIFactory.Brush(ResourceKeys.TextTertiaryBrush) : UIFactory.Brush(ResourceKeys.TextPrimaryBrush), VerticalAlignment = VerticalAlignment.Center });
            if (isRecommended) contentRow.Children.Add(MakeBadge("권장", "#1A3A20", "#6AE87A", "#2A5A30"));
            if (isAsi)         contentRow.Children.Add(MakeBadge("ASI 플러그인",  "#1A2A3A", "#7AACDD", "#2A4A6A"));
            AddExtraDllBadges(contentRow, btn, option, "RTX Encore");
            if (!isRhiOwned && isOnDisk) contentRow.Children.Add(MakeBadge($"디스크에 있음: {onDiskConflicts[name]}", "#2A1A10", "#CC9955", "#5A3A18"));
            if (isCurrent)     contentRow.Children.Add(MakeBadge("현재", "#182840", "#7AACDD", "#2A4468"));
            btn.Content = contentRow;
            // Blocked names got their tooltip from AddExtraDllBadges
            if (!isRhiOwned && isOnDisk && name.StartsWith("bink", StringComparison.OrdinalIgnoreCase))
                ToolTipService.SetToolTip(btn, $"'{name}'이(가) 이미 있습니다({onDiskConflicts[name]}). RTX Encore는 이 파일을 '{System.IO.Path.GetFileNameWithoutExtension(name)}Hooked.dll'(으)로 옆에 두어야 합니다. RHI가 이름을 바꾸고 제거 시 되돌립니다.");
            else if (!isRhiOwned && isOnDisk) ToolTipService.SetToolTip(btn, $"'{name}'이(가) 이미 있습니다({onDiskConflicts[name]}). RHI가 백업한 뒤 제거 시 복원합니다.");
            btn.Tag = name;
            btn.Click += (s, ev) => { chosen = (s as Button)?.Tag as string; if (s is FrameworkElement fe) FindParentContentDialog(fe)?.Hide(); };
            listPanel.Children.Add(btn);
        }

        var pickerDialog = new ContentDialog
        {
            Title = "RTX Encore DLL 이름 선택",
            Content = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = "RTX Encore의 파일 이름을 선택하세요. RHI 구성 요소가 사용 중인 이름은 비활성화됩니다. 주황색 이름은 디스크에 이미 파일이 있습니다 — RHI가 백업한 뒤 제거 시 복원합니다.", FontSize = 11, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), TextWrapping = TextWrapping.Wrap }, scrollViewer } },
            CloseButtonText = "취소", XamlRoot = _window.Content.XamlRoot, RequestedTheme = ElementTheme.Dark,
        };
        await DialogService.ShowSafeAsync(pickerDialog);
        return chosen;
    }
}
