using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

/// <summary>
/// Handles mass INI deployment and preset installation across all games.
/// Extracted from SettingsHandler to isolate the deployment concern.
/// </summary>
public class MassDeployHandler
{
    private readonly MainWindow _window;
    private readonly IOptiScalerService _optiScalerService;

    public MassDeployHandler(MainWindow window)
    {
        _window = window;
        _optiScalerService = App.Services.GetRequiredService<IOptiScalerService>();
    }

    public async void MassDeployRsIni_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var eligible = _window.ViewModel.AllCards.Where(c => c.RsStatus == GameStatus.Installed && !string.IsNullOrEmpty(c.InstallPath)).ToList();
            var confirmDialog = new ContentDialog
            {
                Title = "일괄 배포 확인",
                Content = $"ReShade가 설치된 게임 {eligible.Count}개에 ReShade.ini를 배포합니다.\n\n사용자 지정 단축키와 스크린샷 경로 설정은 유지됩니다.\n\n계속할까요?",
                PrimaryButtonText = "배포",
                CloseButtonText = "취소",
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            if (await DialogService.ShowSafeAsync(confirmDialog) != ContentDialogResult.Primary) return;

            int count = 0;
            var screenshotPath = _window.BuildScreenshotSavePath(null); // base path
            var overlayHotkey = _window.ViewModel.Settings.OverlayHotkey;
            var screenshotHotkey = _window.ViewModel.Settings.ScreenshotHotkey;
        
            await Task.Run(() =>
            {
                foreach (var card in eligible)
                {
                    try
                    {
                        var gameName = card.GameName;
                        var installPath = card.InstallPath;
                        var actualScreenshotPath = _window.BuildScreenshotSavePath(gameName);
                        if (card.RequiresVulkanInstall)
                            AuxInstallService.MergeRsVulkanIni(installPath, gameName, actualScreenshotPath, overlayHotkey, screenshotHotkey);
                        else
                            AuxInstallService.MergeRsIni(installPath, actualScreenshotPath, overlayHotkey, screenshotHotkey);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        CrashReporter.Log($"[MassDeployRsIni] Failed for '{card.GameName}' — {ex.Message}");
                    }
                }
            });
            CrashReporter.Log($"[MassDeployRsIni] Deployed reshade.ini to {count} game(s)");
            await ShowDeployResult("ReShade.ini", count);
        }
        catch (Exception unhandledEx)
        {
            CrashReporter.Log($"[MassDeployHandler.MassDeployRsIni_Click] Unhandled error — {unhandledEx.Message}\n{unhandledEx.StackTrace}");
        }
    }

    public async void MassDeployUlIni_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var eligible = _window.ViewModel.AllCards.Where(c => c.UlStatus == GameStatus.Installed && !string.IsNullOrEmpty(c.InstallPath)).ToList();
            var confirmDialog = new ContentDialog
            {
                Title = "일괄 배포 확인",
                Content = $"ReLimiter가 설치된 게임 {eligible.Count}개에 relimiter.ini를 배포합니다.\n\n계속할까요?",
                PrimaryButtonText = "배포",
                CloseButtonText = "취소",
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            if (await DialogService.ShowSafeAsync(confirmDialog) != ContentDialogResult.Primary) return;

            int count = 0;
            await Task.Run(() =>
            {
                foreach (var card in eligible)
                {
                    try
                    {
                        AuxInstallService.CopyUlIni(card.InstallPath);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        CrashReporter.Log($"[MassDeployUlIni] Failed for '{card.GameName}' — {ex.Message}");
                    }
                }
            });
            CrashReporter.Log($"[MassDeployUlIni] Deployed relimiter.ini to {count} game(s)");
            await ShowDeployResult("relimiter.ini", count);
        }
        catch (Exception unhandledEx)
        {
            CrashReporter.Log($"[MassDeployHandler.MassDeployUlIni_Click] Unhandled error — {unhandledEx.Message}\n{unhandledEx.StackTrace}");
        }
    }

    public async void MassDeployDcIni_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var eligible = _window.ViewModel.AllCards.Where(c => c.DcStatus == GameStatus.Installed && !string.IsNullOrEmpty(c.InstallPath)).ToList();
            var confirmDialog = new ContentDialog
            {
                Title = "일괄 배포 확인",
                Content = $"Display Commander가 설치된 게임 {eligible.Count}개에 DisplayCommander.ini를 배포합니다.\n\n계속할까요?",
                PrimaryButtonText = "배포",
                CloseButtonText = "취소",
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            if (await DialogService.ShowSafeAsync(confirmDialog) != ContentDialogResult.Primary) return;

            int count = 0;
            await Task.Run(() =>
            {
                foreach (var card in eligible)
                {
                    try
                    {
                        AuxInstallService.CopyDcIni(card.InstallPath);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        CrashReporter.Log($"[MassDeployDcIni] Failed for '{card.GameName}' — {ex.Message}");
                    }
                }
            });
            CrashReporter.Log($"[MassDeployDcIni] Deployed DisplayCommander.ini to {count} game(s)");
            await ShowDeployResult("DisplayCommander.ini", count);
        }
        catch (Exception unhandledEx)
        {
            CrashReporter.Log($"[MassDeployHandler.MassDeployDcIni_Click] Unhandled error — {unhandledEx.Message}\n{unhandledEx.StackTrace}");
        }
    }

    public async void MassDeployOsIni_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int count = 0;
            var sourceIni = Services.OptiScalerService.OsIniPath;
            if (!File.Exists(sourceIni))
            {
                CrashReporter.Log("[MassDeployOsIni] No OptiScaler.ini found in INIs folder — aborting");
                await ShowDeployResult("OptiScaler.ini", 0);
                return;
            }

            var eligible = _window.ViewModel.AllCards.Where(c => c.OsStatus == GameStatus.Installed && !string.IsNullOrEmpty(c.InstallPath)).ToList();
            var confirmDialog = new ContentDialog
            {
                Title = "일괄 배포 확인",
                Content = $"OptiScaler가 설치된 게임 {eligible.Count}개에 OptiScaler.ini를 배포합니다.\n\n계속할까요?",
                PrimaryButtonText = "배포",
                CloseButtonText = "취소",
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            if (await DialogService.ShowSafeAsync(confirmDialog) != ContentDialogResult.Primary) return;

            var osHotkey = _window.ViewModel.Settings.OsHotkey;
            var osGpuType = _window.ViewModel.Settings.OsGpuType;
            var osDlssInputs = _window.ViewModel.Settings.OsDlssInputs;
            await Task.Run(() =>
            {
                foreach (var card in eligible)
                {
                    try
                    {
                        _optiScalerService.CopyIniToGame(card, osHotkey, osGpuType, osDlssInputs);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        CrashReporter.Log($"[MassDeployOsIni] Failed for '{card.GameName}' — {ex.Message}");
                    }
                }
            });
            CrashReporter.Log($"[MassDeployOsIni] Deployed OptiScaler.ini to {count} game(s)");
            await ShowDeployResult("OptiScaler.ini", count);
        }
        catch (Exception unhandledEx)
        {
            CrashReporter.Log($"[MassDeployHandler.MassDeployOsIni_Click] Unhandled error — {unhandledEx.Message}\n{unhandledEx.StackTrace}");
        }
    }

    private async Task ShowDeployResult(string iniName, int count)
    {
        var message = count > 0
            ? $"✅ 게임 {count}개에 {iniName}을(를) 배포했습니다."
            : $"해당 구성 요소가 설치된 게임이 없습니다.";
        var dialog = new ContentDialog
        {
            Title = "INI 일괄 배포",
            Content = message,
            CloseButtonText = "확인",
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        await DialogService.ShowSafeAsync(dialog);
    }

    public async Task MassPresetInstall_ClickAsync(XamlRoot xamlRoot)
    {
        // ── 1. Show preset picker ────────────────────────────────────────────
        var selectedPresets = await PresetPopupHelper.ShowAsync(xamlRoot);
        if (selectedPresets == null || selectedPresets.Count == 0) return;

        // ── 2. Show game picker — list all games with ReShade installed ──────
        var rsGames = _window.ViewModel.AllCards
            .Where(c => c.RsStatus == GameStatus.Installed && !string.IsNullOrEmpty(c.InstallPath))
            .OrderBy(c => c.GameName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rsGames.Count == 0)
        {
            var noGamesDialog = new ContentDialog
            {
                Title = "사용 가능한 게임 없음",
                Content = "ReShade가 설치된 게임을 찾지 못했습니다. 먼저 하나 이상의 게임에 ReShade를 설치하세요.",
                CloseButtonText = "확인",
                XamlRoot = xamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(noGamesDialog);
            return;
        }

        var gamePanel = new StackPanel { Spacing = 4 };
        var gameCheckBoxes = new List<(GameCardViewModel Card, CheckBox Box)>();

        // Select All / Deselect All buttons
        var selectAllBtn = new Button
        {
            Content = "모두 선택",
            FontSize = 11,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 8, 8),
        };
        var deselectAllBtn = new Button
        {
            Content = "모두 선택 해제",
            FontSize = 11,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 0, 8),
        };
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
        btnRow.Children.Add(selectAllBtn);
        btnRow.Children.Add(deselectAllBtn);
        gamePanel.Children.Add(btnRow);

        foreach (var card in rsGames)
        {
            var cb = new CheckBox
            {
                Content = card.GameName,
                IsChecked = false,
                FontSize = 12,
                Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
                Margin = new Thickness(0, 2, 0, 2),
            };
            gameCheckBoxes.Add((card, cb));
            gamePanel.Children.Add(cb);
        }

        selectAllBtn.Click += (s, ev) => { foreach (var (_, cb) in gameCheckBoxes) cb.IsChecked = true; };
        deselectAllBtn.Click += (s, ev) => { foreach (var (_, cb) in gameCheckBoxes) cb.IsChecked = false; };

        var gameScrollViewer = new ScrollViewer
        {
            Content = gamePanel,
            MaxHeight = 400,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        var gameDialog = new ContentDialog
        {
            Title = $"게임 선택 — {string.Join(", ", selectedPresets)}",
            Content = gameScrollViewer,
            PrimaryButtonText = "배포",
            IsPrimaryButtonEnabled = false,
            CloseButtonText = "취소",
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
            MinWidth = 500,
        };

        // Enable Deploy only when at least one game is ticked
        foreach (var (_, box) in gameCheckBoxes)
        {
            box.Checked += (s, ev) => gameDialog.IsPrimaryButtonEnabled = gameCheckBoxes.Any(cb => cb.Box.IsChecked == true);
            box.Unchecked += (s, ev) => gameDialog.IsPrimaryButtonEnabled = gameCheckBoxes.Any(cb => cb.Box.IsChecked == true);
        }

        var gameResult = await DialogService.ShowSafeAsync(gameDialog);
        if (gameResult != ContentDialogResult.Primary) return;

        // ── 3. Deploy presets to selected games ──────────────────────────────
        var selectedGames = gameCheckBoxes
            .Where(cb => cb.Box.IsChecked == true)
            .Select(cb => cb.Card)
            .ToList();

        int totalDeployed = 0;
        await Task.Run(() =>
        {
            foreach (var card in selectedGames)
            {
                try
                {
                    int count = PresetPopupHelper.DeployPresets(selectedPresets, card.InstallPath);
                    totalDeployed += count;
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[MassPresetInstall] Failed for '{card.GameName}' — {ex.Message}");
                }
            }
        });
        CrashReporter.Log($"[MassPresetInstall] Deployed {selectedPresets.Count} preset(s) to {selectedGames.Count} game(s) ({totalDeployed} total copies)");

        if (totalDeployed == 0) return;

        // ── 4. Offer shader installation ─────────────────────────────────────
        var shaderDialog = new ContentDialog
        {
            Title = "🔧 셰이더를 설치할까요?",
            Content = $"게임 {selectedGames.Count}개에 프리셋을 배포했습니다.\n\n이 게임들에 필요한 셰이더 팩도 설치할까요?",
            PrimaryButtonText = "예",
            CloseButtonText = "아니요",
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        var shaderResult = await DialogService.ShowSafeAsync(shaderDialog);
        if (shaderResult == ContentDialogResult.Primary)
        {
            var presetPaths = selectedPresets.Select(f => Path.Combine(PresetPopupHelper.PresetsDir, f)).ToList();
            foreach (var card in selectedGames)
            {
                try
                {
                    await _window.ViewModel.ApplyPresetShadersAsync(card.GameName, presetPaths, card.Source ?? "");
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[MassPresetInstall] Shader install failed for '{card.GameName}' — {ex.Message}");
                }
            }
            CrashReporter.Log($"[MassPresetInstall] Applied preset shaders to {selectedGames.Count} game(s)");

            // Rebuild overrides panel if the currently selected game was one of the targets
            if (_window.ViewModel.SelectedGame is { } selectedCard
                && selectedGames.Any(c => c.GameName.Equals(selectedCard.GameName, StringComparison.OrdinalIgnoreCase)))
            {
                _window.BuildOverridesPanel(selectedCard);
            }
        }
    }
}
