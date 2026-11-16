// DetailPanelBuilder.Extras.ApiWrappers.cs — Extras section: DXVK and dgVoodoo rows.

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
    private void BuildDxvkRow(GameCardViewModel card, StackPanel body)
    {
        _window.ViewModel.SetLastUiAction($"BuildDxvkRow({card.GameName})");
        // Col 0: label (120)  Col 1: status (80)  Col 2: Info (36)
        // Col 3: install (*)  Col 4: cog (36)     Col 5: delete (36)
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelColW(1, 0, 350, _window.ExtrasContainer.ActualWidth)) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        bool isDisabled = !card.IsDxvkToggleEnabled;

        // Col 0 — label
        var label = new TextBlock
        {
            Text = "DXVK",
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = isDisabled ? 0.35 : 1.0,
            Tag = card,
        };
        ToolTipService.SetToolTip(label, card.DxvkToggleTooltip
            ?? "DXVK — DX8, DX9, DX10, DX11 호출을 Vulkan으로 변환하여 Lilium HDR 셰이더로 scRGB HDR을 사용할 수 있게 합니다.");
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        // Col 1 — status
        var statusBlock = new TextBlock
        {
            Text = card.DxvkStatusText,
            FontSize = 12,
            Foreground = UIFactory.GetBrush(card.DxvkStatusColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalTextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            TextDecorations = card.IsDxvkInstalled
                ? Windows.UI.Text.TextDecorations.Underline
                : Windows.UI.Text.TextDecorations.None,
            Opacity = isDisabled ? 0.35 : 1.0,
        };
        if (card.IsDxvkInstalled && !isDisabled)
        {
            ToolTipService.SetToolTip(statusBlock, "클릭하여 DXVK 릴리스 열기");
            statusBlock.PointerPressed += (s, e) => _window.DetailDxvkStatus_PointerPressed(s, e);
            statusBlock.PointerEntered += (s, e) => _window.LinkText_PointerEntered(s, e);
            statusBlock.PointerExited  += (s, e) => _window.LinkText_PointerExited(s, e);
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
            CornerRadius = new CornerRadius(8),
            Background = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
            Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush),
            BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
            BorderThickness = new Thickness(1),
            Tag = card,
            Opacity = isDisabled ? 0.35 : 1.0,
        };
        // Same wording as the Components Info buttons (AddonInfoResolver.GetTooltip)
        bool hasDxvkNotes = _window.ViewModel.Manifest?.DxvkGameNotes != null
            && _window.ViewModel.Manifest.DxvkGameNotes.TryGetValue(card.GameName, out var dxvkNote)
            && !string.IsNullOrWhiteSpace(dxvkNote.Notes);
        ToolTipService.SetToolTip(infoBtn, hasDxvkNotes ? "게임별 노트 있음" : "일반 DXVK 정보");
        infoBtn.Click += (s, e) => _window.DxvkInfoButton_Click(s, e);
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        // Col 3 — Install button
        var installBtn = new Button
        {
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            CornerRadius = new CornerRadius(8),
            FontSize = 12,
            Background = UIFactory.GetBrush(card.DxvkBtnBackground),
            Foreground = UIFactory.GetBrush(card.DxvkBtnForeground),
            BorderBrush = UIFactory.GetBrush(card.DxvkBtnBorderBrush),
            BorderThickness = new Thickness(1),
            Tag = card,
            IsEnabled = card.DxvkInstallEnabled && !isDisabled,
            Opacity = isDisabled ? 0.35 : 1.0,
            IsHitTestVisible = !isDisabled,
        };
        installBtn.Content = card.DxvkActionLabel;
        installBtn.Click += (s, e) => _window.InstallDxvkButton_Click(s, e);
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        // Col 4 — Cog button
        var cogBtn = CogDialog.MakeCogButton("DXVK 설정");
        cogBtn.Tag = card;
        cogBtn.IsEnabled = !isDisabled;
        cogBtn.Opacity = isDisabled ? 0.35 : 1.0;
        cogBtn.Click += (s, e) => _window.DxvkCogButton_Click(s, e);
        Grid.SetColumn(cogBtn, 4);
        row.Children.Add(cogBtn);

        // Col 5 — Delete button
        bool showDelete = card.DxvkDeleteVisibility == Visibility.Visible;
        var deleteBtn = new Button
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
            Tag = card,
            Opacity = showDelete ? 1.0 : 0.0,
            IsHitTestVisible = showDelete,
        };
        ToolTipService.SetToolTip(deleteBtn, "DXVK 제거");
        deleteBtn.Click += (s, e) => _window.UninstallDxvkButton_Click(s, e);
        Grid.SetColumn(deleteBtn, 5);
        row.Children.Add(deleteBtn);

        body.Children.Add(row);
    }

    private void BuildDgVoodooRow(GameCardViewModel card, StackPanel body)
    {
        _window.ViewModel.SetLastUiAction($"BuildDgVoodooRow({card.GameName})");
        var gameName    = card.GameName;
        var store       = card.Source ?? "";
        var installPath = card.InstallPath ?? "";

        var dgSvc   = App.Services.GetRequiredService<DgVoodooService>();
        var manifest = _window.ViewModel.Manifest;

        bool isInstalled = dgSvc.IsDeployed(installPath);

        // Status: show deployed version — prefer stored per-game override, else latest from manifest
        string? latestVersion = manifest?.DgVoodooVersions?.Keys.FirstOrDefault();
        var activeVersion = _window.ViewModel.GetDgVoodooVersion(gameName, store) ?? latestVersion;
        string statusText  = isInstalled ? (activeVersion != null ? $"v{activeVersion}" : "Installed") : "Ready";
        string statusColor = isInstalled ? "#5ECB7D" : "#A0AABB";

        // ── Row grid ──────────────────────────────────────────────────────────
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
            Text = "dgVoodoo2",
            FontSize = 12,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        // The install button is disabled when the manifest lists no dgVoodoo2 versions and
        // disabled buttons show no tooltip, so the reason is added to this label.
        ToolTipService.SetToolTip(label,
            "dgVoodoo2 — DX9 호출을 DX11로 변환하여 DX9 게임에서 ReShade 컴퓨트 셰이더와 DLSS5 Feeder를 사용할 수 있게 합니다.\nD3D9.dll + dgVoodoo.conf를 게임 폴더에 배포합니다."
            + (manifest?.DgVoodooVersions?.Count > 0
                ? ""
                : "\n\n지금은 설치할 수 없습니다: 온라인 데이터에 dgVoodoo2 버전이 없습니다. 나중에 새로 고침을 시도하세요."));
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
            ToolTipService.SetToolTip(statusBlock, "클릭하여 dgVoodoo2 릴리스 페이지 열기");
            statusBlock.PointerPressed += (s, e) =>
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dege-diosg/dgVoodoo2/releases"));
            statusBlock.PointerEntered += (s, e) => _window.LinkText_PointerEntered(s, e);
            statusBlock.PointerExited  += (s, e) => _window.LinkText_PointerExited(s, e);
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
        ToolTipService.SetToolTip(infoBtn, "dgVoodoo2 릴리스 페이지 열기");
        infoBtn.Click += (s, e) =>
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dege-diosg/dgVoodoo2/releases"));
        Grid.SetColumn(infoBtn, 2);
        row.Children.Add(infoBtn);

        // Col 3 — Install button
        var installBtn = new Button
        {
            Content = isInstalled ? "↺  Redeploy dgVoodoo2" : "⬇  Install dgVoodoo2",
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
            IsEnabled = manifest?.DgVoodooVersions?.Count > 0,
        };
        ToolTipService.SetToolTip(installBtn,
            isInstalled ? "dgVoodoo2 재배포 — 스테이징된 버전에서 D3D9.dll과 dgVoodoo.conf를 새로 고칩니다"
                        : "dgVoodoo2 설치 — D3D9.dll과 dgVoodoo.conf를 게임 폴더에 배포합니다");

        installBtn.Click += async (s, ev) =>
        {
            if (string.IsNullOrEmpty(installPath) || manifest?.DgVoodooVersions == null) return;
            installBtn.IsEnabled = false;
            installBtn.Content   = "Installing...";
            try
            {
                // Pick version: prefer stored per-game override, then latest from manifest
                KeyValuePair<string, string> versionEntry;
                var storedVersion = _window.ViewModel.GetDgVoodooVersion(gameName, store);
                if (!string.IsNullOrEmpty(storedVersion)
                    && manifest.DgVoodooVersions.TryGetValue(storedVersion, out var storedUrl))
                    versionEntry = new KeyValuePair<string, string>(storedVersion, storedUrl);
                else
                    versionEntry = manifest.DgVoodooVersions.First();

                await dgSvc.EnsureStagedAsync(versionEntry.Key, versionEntry.Value).ConfigureAwait(false);
                var deployed = await Task.Run(() =>
                    dgSvc.DeployToGame(installPath, versionEntry.Key, is64Bit: !card.Is32Bit));

                if (deployed.Count > 0)
                {
                    _window.ViewModel.SetDgVoodooStandalone(gameName, true, store);
                    CrashReporter.Log($"[BuildDgVoodooRow] dgVoodoo2 v{versionEntry.Key} deployed standalone for '{gameName}'");

                    // If ReShade was at d3d9.dll, dgVoodoo2's SentinelBackup has already saved it
                    // as d3d9.dll.original. Copy it to dxgi.dll so ReShade hooks dgVoodoo2's DX11 output.
                    // Do NOT move d3d9.dll (that's now dgVoodoo2).
                    if (card.IsRsInstalled
                        && card.RsInstalledFile?.Equals("d3d9.dll", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        CrashReporter.Log($"[BuildDgVoodooRow] Copying ReShade from D3D9.dll.original → dxgi.dll for dgVoodoo2 coexistence on '{gameName}'");
                        try
                        {
                            // D3D9.dll.original is the ReShade DLL backed up by SentinelBackup during deploy.
                            // Copy it to dxgi.dll so ReShade hooks dgVoodoo2's DX11 output.
                            // No sentinel needed on dxgi.dll — this is our file, not a foreign DLL.
                            var sentinelPath = Path.Combine(installPath, "d3d9.dll.original");
                            var dxgiPath     = Path.Combine(installPath, "dxgi.dll");
                            if (File.Exists(sentinelPath) && new FileInfo(sentinelPath).Length > 0)
                            {
                                File.Copy(sentinelPath, dxgiPath, overwrite: true);
                                // Update ReShade tracking record
                                if (card.RsRecord != null)
                                {
                                    card.RsRecord.InstalledAs = "dxgi.dll";
                                    _auxInstallService.SaveAuxRecord(card.RsRecord);
                                }
                                card.RsInstalledFile = "dxgi.dll";
                                CrashReporter.Log($"[BuildDgVoodooRow] ReShade copied to dxgi.dll for '{gameName}'");
                            }
                            else
                            {
                                CrashReporter.Log($"[BuildDgVoodooRow] D3D9.dll.original not found or is empty sentinel — skipping coexistence copy for '{gameName}'");
                            }
                        }
                        catch (Exception rsEx)
                        {
                            CrashReporter.Log($"[BuildDgVoodooRow] Failed to copy ReShade to dxgi.dll for '{gameName}' — {rsEx.Message}");
                        }
                    }

                    // Rebuild after everything (including ReShade reinstall) is complete
                    RequestExtrasRebuild(card);
                }
                else
                {
                    installBtn.Content = "❌ Deploy failed";
                    installBtn.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildDgVoodooRow] Install failed for '{gameName}' — {ex.Message}");
                installBtn.Content   = "❌ Failed";
                installBtn.IsEnabled = true;
            }
        };
        Grid.SetColumn(installBtn, 3);
        row.Children.Add(installBtn);

        // Col 4 — Cog
        var cogBtn = CogDialog.MakeCogButton("dgVoodoo2 설정");
        cogBtn.Click += async (s, ev) =>
        {
            try
            {
                var cog = new CogDialog("dgVoodoo2", _window.Content.XamlRoot);

                // Build version list from manifest
                var versions = manifest?.DgVoodooVersions?.Keys.ToList() ?? new List<string>();
                if (versions.Count == 0)
                {
                    cog.AddEmpty("매니페스트에 사용 가능한 dgVoodoo2 버전이 없습니다.");
                    await cog.ShowAsync();
                    return;
                }

                var currentVersion = _window.ViewModel.GetDgVoodooVersion(gameName, store) ?? versions[0];
                int currentIdx = versions.IndexOf(currentVersion);
                if (currentIdx < 0) currentIdx = 0;

                ComboBox? versionCombo = null;

                // Picking a version saves it and, if dgVoodoo2 is installed, redeploys it straight away
                async Task RedeployAsync(string selectedVersion, string versionUrl)
                {
                    if (versionCombo != null) versionCombo.IsEnabled = false;
                    cog.SetStatus($"dgVoodoo2 v{selectedVersion} 배포 중...");
                    try
                    {
                        await Task.Run(() => dgSvc.EnsureStagedAsync(selectedVersion, versionUrl));
                        var redeployed = await Task.Run(() => dgSvc.DeployToGame(installPath, selectedVersion, is64Bit: !card.Is32Bit));
                        if (redeployed.Count > 0)
                        {
                            CrashReporter.Log($"[BuildDgVoodooRow] Redeployed dgVoodoo2 v{selectedVersion} for '{gameName}'");
                            cog.SetSuccess($"dgVoodoo2 v{selectedVersion}을(를) 게임 폴더에 배포했습니다.");
                        }
                        else
                        {
                            CrashReporter.Log($"[BuildDgVoodooRow] Redeploy of v{selectedVersion} deployed no files for '{gameName}'");
                            cog.SetError($"dgVoodoo2 v{selectedVersion}을(를) 배포할 수 없습니다. 자세한 내용은 로그를 확인하세요.");
                        }
                    }
                    catch (Exception ex)
                    {
                        CrashReporter.Log($"[BuildDgVoodooRow] Redeploy failed — {ex.Message}");
                        cog.SetError($"dgVoodoo2 v{selectedVersion}을(를) 배포할 수 없습니다: {ex.Message}");
                    }
                    finally
                    {
                        if (versionCombo != null) versionCombo.IsEnabled = true;
                    }
                    RequestExtrasRebuild(card);
                }

                versionCombo = cog.AddCombo("버전",
                    "이 게임에 사용할 dgVoodoo2 버전입니다. 일부 게임에서는 이전 버전이 더 잘 작동할 수 있습니다 (예: Mass Effect는 2.87.3).",
                    versions, currentIdx, i =>
                    {
                        var selectedVersion = versions[i];
                        _window.ViewModel.SetDgVoodooVersion(gameName, selectedVersion, store);

                        // Redeploy if already installed
                        if (isInstalled && manifest?.DgVoodooVersions?.TryGetValue(selectedVersion, out var versionUrl) == true)
                            _ = RedeployAsync(selectedVersion, versionUrl);
                        else
                            cog.SetSuccess($"버전을 {selectedVersion}(으)로 설정했습니다. dgVoodoo2를 설치할 때 사용됩니다.");
                    });
                versionCombo.MaxDropDownHeight = 300;

                cog.AddNote("이 게임에 dgVoodoo2가 설치되어 있으면 버전을 선택하는 즉시 게임 폴더에 재배포됩니다.");

                await cog.ShowAsync();
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[BuildDgVoodooRow] dgVoodoo2 settings dialog failed for '{gameName}' — {ex.Message}");
            }
        };
        Grid.SetColumn(cogBtn, 4);
        row.Children.Add(cogBtn);

        // Col 5 — Remove button (visible only when installed)
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
            Opacity = isInstalled ? 1.0 : 0.0,
            IsHitTestVisible = isInstalled,
        };
        ToolTipService.SetToolTip(removeBtn, "이 게임에서 dgVoodoo2 제거");
        removeBtn.Click += async (s, ev) =>
        {
            if (string.IsNullOrEmpty(installPath)) return;
            // Coexistence guard: don't remove if Luma or Feeder also needs dgVoodoo2
            bool lumaNeeds    = card.LumaStatus == GameStatus.Installed;
            bool feederNeeds  = File.Exists(Path.Combine(installPath, "dlss5-feed.addon32"))
                             || File.Exists(Path.Combine(installPath, "dlss5-feed.addon64"));
            if (lumaNeeds || feederNeeds)
            {
                CrashReporter.Log($"[BuildDgVoodooRow] {(lumaNeeds ? "Luma" : "Feeder")} still installed — only clearing standalone flag for '{gameName}'");
                _window.ViewModel.SetDgVoodooStandalone(gameName, false, store);
                RequestExtrasRebuild(card);
                return;
            }
            dgSvc.RemoveFromGame(installPath);
            _window.ViewModel.SetDgVoodooStandalone(gameName, false, store);
            CrashReporter.Log($"[BuildDgVoodooRow] dgVoodoo2 removed for '{gameName}'");

            // If ReShade was coexisting as dxgi.dll, dgVoodoo2's SentinelRestore already put
            // ReShade back at D3D9.dll (from D3D9.dll.original). Just delete the dxgi.dll copy
            // and update the tracking record — no full reinstall needed.
            if (card.IsRsInstalled
                && card.RsInstalledFile?.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase) == true)
            {
                try
                {
                    var dxgiPath = Path.Combine(installPath, "dxgi.dll");
                    if (File.Exists(dxgiPath)) File.Delete(dxgiPath);
                    // Also clean up any stale dxgi.dll.original left from a previous failed attempt
                    var dxgiOrigPath = Path.Combine(installPath, "dxgi.dll.original");
                    if (File.Exists(dxgiOrigPath)) File.Delete(dxgiOrigPath);
                    if (card.RsRecord != null)
                    {
                        card.RsRecord.InstalledAs = "d3d9.dll";
                        _auxInstallService.SaveAuxRecord(card.RsRecord);
                    }
                    card.RsInstalledFile = "d3d9.dll";
                    CrashReporter.Log($"[BuildDgVoodooRow] Cleaned up coexistence dxgi.dll, updated ReShade record to d3d9.dll for '{gameName}'");
                }
                catch (Exception rsEx)
                {
                    CrashReporter.Log($"[BuildDgVoodooRow] Failed to clean up dxgi.dll for '{gameName}' — {rsEx.Message}");
                }
            }

            RequestExtrasRebuild(card);
        };
        Grid.SetColumn(removeBtn, 5);
        row.Children.Add(removeBtn);

        body.Children.Add(row);
    }
}
