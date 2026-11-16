// MassDlssDeployDialog.cs — Batch DLSS & Streamline deployment dialog
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

/// <summary>
/// Builds and shows the Mass DLSS & Streamline batch deploy dialog.
/// Allows users to select games and deploy specific DLSS/Streamline versions to all at once.
/// </summary>
public class MassDlssDeployDialog
{
    private readonly MainViewModel _viewModel;
    private readonly IDlssStreamlineService _dlssService;
    private readonly XamlRoot _xamlRoot;
    private readonly Action? _onComplete;

    private const string NoneOption = "None";
    private const string DefaultOption = "Default (Restore)";
    private const string CustomOption = "Custom";

    public MassDlssDeployDialog(MainViewModel viewModel, IDlssStreamlineService dlssService, XamlRoot xamlRoot, Action? onComplete = null)
    {
        _viewModel = viewModel;
        _dlssService = dlssService;
        _xamlRoot = xamlRoot;
        _onComplete = onComplete;
    }

    public async Task ShowAsync()
    {
        // ── Build eligible game list (games with DLSS or Streamline detected) ──
        var eligibleCards = _viewModel.AllCards
            .Where(c => c.HasAnyDlssStreamline && !c.IsHidden)
            .OrderBy(c => c.GameName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (eligibleCards.Count == 0)
        {
            var emptyDialog = new ContentDialog
            {
                Title = "DLSS/Streamline 게임 없음",
                Content = "DLSS 또는 Streamline DLL이 있는 게임이 감지되지 않았습니다.\n전체 새로 고침을 실행하여 검색하세요.",
                CloseButtonText = "확인",
                XamlRoot = _xamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(emptyDialog);
            return;
        }

        // ── Left side: Game checkboxes ──
        var checkBoxes = new List<CheckBox>();
        var gameListPanel = new StackPanel { Spacing = 2 };

        foreach (var card in eligibleCards)
        {
            var isV1 = IsV1Game(card);
            var cb = new CheckBox
            {
                Content = card.GameName,
                FontSize = 12,
                IsEnabled = !isV1,
                Foreground = UIFactory.ThemedBrush(isV1
                    ? Windows.UI.Color.FromArgb(255, 120, 120, 120)
                    : Windows.UI.Color.FromArgb(255, 220, 220, 220)),
            };
            if (isV1)
                ToolTipService.SetToolTip(cb, "건너뜀 — v1.x DLSS/Streamline은 최신 버전과 호환되지 않습니다");
            checkBoxes.Add(cb);
            gameListPanel.Children.Add(cb);
        }

        var selectAllBtn = new Button
        {
            Content = "모두 선택", FontSize = 11, Padding = new Thickness(8, 4, 8, 4),
            Background = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 30, 40, 60)),
            Foreground = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 180, 190, 220)),
            CornerRadius = new CornerRadius(6),
        };
        selectAllBtn.Click += (_, _) => { foreach (var cb in checkBoxes) if (cb.IsEnabled) cb.IsChecked = true; };

        var deselectAllBtn = new Button
        {
            Content = "모두 선택 해제", FontSize = 11, Padding = new Thickness(8, 4, 8, 4),
            Background = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 30, 40, 60)),
            Foreground = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 180, 190, 220)),
            CornerRadius = new CornerRadius(6),
        };
        deselectAllBtn.Click += (_, _) => { foreach (var cb in checkBoxes) cb.IsChecked = false; };

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        buttonRow.Children.Add(selectAllBtn);
        buttonRow.Children.Add(deselectAllBtn);

        var leftPanel = new StackPanel { Spacing = 0, Width = 280 };
        leftPanel.Children.Add(buttonRow);
        leftPanel.Children.Add(new ScrollViewer
        {
            Content = gameListPanel,
            MaxHeight = 400,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });

        // ── Right side: Version dropdowns (pre-populated with saved defaults) ──
        var settings = _viewModel.Settings;
        var dlssCombo  = BuildVersionCombo(_dlssService.DlssVersions,       settings.DefaultDlssVersion,  includeDriverOverride: true, currentDriverOverride: settings.DefaultSrDriverOverride);
        var dlssdCombo = BuildVersionCombo(_dlssService.DlssdVersions,      settings.DefaultDlssdVersion, includeDriverOverride: true, currentDriverOverride: settings.DefaultRrDriverOverride);
        var dlssgCombo = BuildVersionCombo(_dlssService.DlssgVersions,      settings.DefaultDlssgVersion, includeDriverOverride: true, currentDriverOverride: settings.DefaultFgDriverOverride);
        var slCombo    = BuildVersionCombo(_dlssService.StreamlineVersions,  settings.DefaultStreamlineVersion);

        // Force visual refresh when switching away from "NVIDIA Override" — WinUI doesn't clear the old text otherwise
        static void ForceComboRefresh(ComboBox cb) =>
            cb.SelectionChanged += (s, e) => VisualStateManager.GoToState(cb, "Normal", false);
        ForceComboRefresh(dlssCombo);
        ForceComboRefresh(dlssdCombo);
        ForceComboRefresh(dlssgCombo);

        // ── Preset dropdowns (pre-populated with saved defaults) ──
        var srPresetCombo = BuildPresetCombo(DlssPresetService.SrPresets, settings.DefaultSrPreset);
        var rrPresetCombo = BuildPresetCombo(DlssPresetService.RrPresets, settings.DefaultRrPreset);
        var fgPresetCombo = BuildPresetCombo(DlssPresetService.FgPresets, settings.DefaultFgPreset);

        var rightPanel = new StackPanel { Spacing = 8, Width = 320 };
        rightPanel.Children.Add(BuildDropdownSection("DLSS 슈퍼 해상도", dlssCombo));
        rightPanel.Children.Add(BuildDropdownSection("DLSS 레이 재구성", dlssdCombo));
        rightPanel.Children.Add(BuildDropdownSection("DLSS 프레임 생성", dlssgCombo));
        rightPanel.Children.Add(BuildDropdownSection("Streamline", slCombo));

        // Presets section
        rightPanel.Children.Add(new Border
        {
            Height = 1,
            Background = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 60, 70, 90)),
            Margin = new Thickness(0, 4, 0, 4),
        });
        rightPanel.Children.Add(BuildDropdownSection("SR 프리셋", srPresetCombo));
        rightPanel.Children.Add(BuildDropdownSection("RR 프리셋", rrPresetCombo));
        rightPanel.Children.Add(BuildDropdownSection("FG 프리셋", fgPresetCombo));

        var rightScroll = new ScrollViewer
        {
            Content = rightPanel,
            MaxHeight = 420,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        // Auto-create profiles checkbox — lives outside the scroll so it's always visible
        var autoCreateCheck = new CheckBox
        {
            Content = "NVIDIA 프로필 자동 생성",
            IsChecked = true,
            FontSize = 11,
            Foreground = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 180, 190, 220)),
            Margin = new Thickness(0, 4, 0, 0),
        };
        ToolTipService.SetToolTip(autoCreateCheck,
            "게임에 일치하는 NVIDIA 드라이버 프로필이 없으면 프리셋을 적용할 수 있도록 새로 만듭니다. "
            + "일치하는 프로필에 게임 exe가 없으면 추가합니다.");
        rightPanel.Children.Add(autoCreateCheck);

        // ── Separator ──
        var separator = new Border
        {
            Width = 1,
            Background = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 60, 70, 90)),
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(16, 0, 16, 0),
        };

        // ── Right-side separator (constrains combo panel visually) ──
        var rightSeparator = new Border
        {
            Width = 1,
            Background = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 60, 70, 90)),
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(16, 0, 0, 0),
        };

        // ── Main layout ──
        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(leftPanel, 0);
        Grid.SetColumn(separator, 1);
        Grid.SetColumn(rightPanel, 2);
        Grid.SetColumn(rightSeparator, 3);
        mainGrid.Children.Add(leftPanel);
        mainGrid.Children.Add(separator);
        mainGrid.Children.Add(rightPanel);
        mainGrid.Children.Add(rightSeparator);

        // ── Dialog ──
        var dialog = new ContentDialog
        {
            Title = "DLSS 및 Streamline 일괄 배포",
            Content = mainGrid,
            PrimaryButtonText = "배포",
            SecondaryButtonText = "복원",
            CloseButtonText = "취소",
            XamlRoot = _xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        dialog.Resources["ContentDialogMaxWidth"] = 800.0;

        var result = await DialogService.ShowSafeAsync(dialog);

        if (result == ContentDialogResult.Primary)
        {
            await ExecuteDeployAsync(eligibleCards, checkBoxes, dlssCombo, dlssdCombo, dlssgCombo, slCombo, srPresetCombo, rrPresetCombo, fgPresetCombo, autoCreateCheck.IsChecked == true);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            await ExecuteRestoreAllAsync(eligibleCards, checkBoxes);
        }
    }

    private async Task ExecuteDeployAsync(
        List<GameCardViewModel> eligibleCards,
        List<CheckBox> checkBoxes,
        ComboBox dlssCombo, ComboBox dlssdCombo, ComboBox dlssgCombo, ComboBox slCombo,
        ComboBox srPresetCombo, ComboBox rrPresetCombo, ComboBox fgPresetCombo,
        bool autoCreateProfiles)
    {
        var dlssVersion  = dlssCombo.SelectedItem  as string;
        var dlssdVersion = dlssdCombo.SelectedItem as string;
        var dlssgVersion = dlssgCombo.SelectedItem as string;
        var slVersion    = slCombo.SelectedItem    as string;

        var srPresetSelection = srPresetCombo.SelectedItem as string;
        var rrPresetSelection = rrPresetCombo.SelectedItem as string;
        var fgPresetSelection = fgPresetCombo.SelectedItem as string;

        // "NVIDIA Override" in the version combo means: write the driver DLL override, skip file swap
        bool srDriverEnable = dlssVersion  == NvidiaOverrideOption;
        bool rrDriverEnable = dlssdVersion == NvidiaOverrideOption;
        bool fgDriverEnable = dlssgVersion == NvidiaOverrideOption;

        // Nothing selected at all
        bool anyDllSelected    = dlssVersion  != NoneOption || dlssdVersion != NoneOption || dlssgVersion != NoneOption || slVersion != NoneOption;
        bool anyPresetSelected = srPresetSelection != NoneOption || rrPresetSelection != NoneOption || fgPresetSelection != NoneOption;
        if (!anyDllSelected && !anyPresetSelected)
            return;

        int dlssCount = 0, dlssdCount = 0, dlssgCount = 0, slCount = 0;
        int srPresetCount = 0, rrPresetCount = 0, fgPresetCount = 0, presetMissedCount = 0;
        int srDriverCount = 0, rrDriverCount = 0, fgDriverCount = 0;
        int skippedNoComponent = 0, skippedAlreadyAtVersion = 0;
        var failedGames = new List<string>();
        // Set auto-create flag on preset service
        var presetService = App.Services.GetRequiredService<DlssPresetService>();
        var previousAutoCreate = presetService.AutoCreateProfiles;
        presetService.AutoCreateProfiles = autoCreateProfiles;
        presetService.ProfilesCreatedCount = 0;

        // Show progress
        var progressText = new TextBlock
        {
            Text = "선택한 게임에 배포 중...",
            FontSize = 12,
            Foreground = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 200, 200, 200)),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var progressRing = new ProgressRing
        {
            IsActive = true,
            Width = 24,
            Height = 24,
        };
        var progressPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        progressPanel.Children.Add(progressRing);
        progressPanel.Children.Add(progressText);

        var progressContainer = new Grid
        {
            MinWidth = 350,
        };
        progressContainer.Children.Add(progressPanel);

        var progressDialog = new ContentDialog
        {
            Title = "배포 중...",
            Content = progressContainer,
            XamlRoot = _xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        await using var progressSession = await DialogService.ShowProgressAsync(progressDialog, 10);
        if (progressSession == null) return;

        // Count selected games for progress
        int totalSelected = 0;
        for (int j = 0; j < checkBoxes.Count; j++)
            if (checkBoxes[j].IsChecked == true && !IsV1Game(eligibleCards[j])) totalSelected++;
        int processed = 0;

        for (int i = 0; i < eligibleCards.Count; i++)
        {
            var card = eligibleCards[i];
            var cb = checkBoxes[i];
            if (cb.IsChecked != true) continue;

            var detection = card.DlssDetection;
            if (detection == null) { skippedNoComponent++; continue; }

            // Skip games whose install folder no longer exists — avoids hangs on
            // deleted/moved/disconnected paths
            if (string.IsNullOrEmpty(card.InstallPath) || !Directory.Exists(card.InstallPath))
            {
                CrashReporter.Log($"[MassDlssDeployDialog] Skipping '{card.GameName}' — install path not found: '{card.InstallPath}'");
                skippedNoComponent++;
                continue;
            }

            processed++;
            progressText.Text = $"[{processed}/{totalSelected}] {card.GameName}";
            await Task.Delay(1); // Yield to UI thread so text updates visually
            try
            {

                // DLSS SR — skip v1.x
                if (dlssVersion != NoneOption && detection.DlssPath != null
                    && !(card.DlssInstalledVersion?.StartsWith("1.") == true))
                {
                    if (srDriverEnable)
                    {
                        if (presetService.IsSupported && card.HasDlss)
                        { presetService.SetSrDriverOverride(card.GameName, card.InstallPath, true); srDriverCount++; }
                    }
                    else if (dlssVersion == DefaultOption)
                    {
                        if (_dlssService.HasBackup(detection.DlssPath))
                        { _dlssService.Restore(detection.DlssPath); dlssCount++; }
                    }
                    else if (dlssVersion == CustomOption)
                    {
                        await _dlssService.SwapDlssCustomAsync(detection.DlssPath); dlssCount++;
                    }
                    else
                    {
                        var installed = DlssStreamlineService.FormatVersion(_dlssService.GetFileVersion(detection.DlssPath));
                        if (installed == dlssVersion) { skippedAlreadyAtVersion++; }
                        else { await _dlssService.SwapDlssAsync(detection.DlssPath, dlssVersion!); dlssCount++; }
                    }
                }
                else if (dlssVersion != NoneOption && detection.DlssPath == null) { skippedNoComponent++; }

                // DLSS RR
                if (dlssdVersion != NoneOption && detection.DlssdPath != null)
                {
                    if (rrDriverEnable)
                    {
                        if (presetService.IsSupported && card.HasDlssd)
                        { presetService.SetRrDriverOverride(card.GameName, card.InstallPath, true); rrDriverCount++; }
                    }
                    else if (dlssdVersion == DefaultOption)
                    {
                        if (_dlssService.HasBackup(detection.DlssdPath))
                        { _dlssService.Restore(detection.DlssdPath); dlssdCount++; }
                    }
                    else if (dlssdVersion == CustomOption)
                    {
                        await _dlssService.SwapDlssCustomAsync(detection.DlssdPath); dlssdCount++;
                    }
                    else
                    {
                        var installed = DlssStreamlineService.FormatVersion(_dlssService.GetFileVersion(detection.DlssdPath));
                        if (installed == dlssdVersion) { skippedAlreadyAtVersion++; }
                        else { await _dlssService.SwapDlssdAsync(detection.DlssdPath, dlssdVersion!); dlssdCount++; }
                    }
                }
                else if (dlssdVersion != NoneOption && detection.DlssdPath == null) { skippedNoComponent++; }

                // DLSS FG
                if (dlssgVersion != NoneOption && detection.DlssgPath != null)
                {
                    if (fgDriverEnable)
                    {
                        if (presetService.IsSupported && card.HasDlssg)
                        { presetService.SetFgDriverOverride(card.GameName, card.InstallPath, true); fgDriverCount++; }
                    }
                    else if (dlssgVersion == DefaultOption)
                    {
                        if (_dlssService.HasBackup(detection.DlssgPath))
                        { _dlssService.Restore(detection.DlssgPath); dlssgCount++; }
                    }
                    else if (dlssgVersion == CustomOption)
                    {
                        await _dlssService.SwapDlssCustomAsync(detection.DlssgPath); dlssgCount++;
                    }
                    else
                    {
                        var installed = DlssStreamlineService.FormatVersion(_dlssService.GetFileVersion(detection.DlssgPath));
                        if (installed == dlssgVersion) { skippedAlreadyAtVersion++; }
                        else { await _dlssService.SwapDlssgAsync(detection.DlssgPath, dlssgVersion!); dlssgCount++; }
                    }
                }
                else if (dlssgVersion != NoneOption && detection.DlssgPath == null) { skippedNoComponent++; }

                // Streamline — skip v1.x
                if (slVersion != NoneOption && detection.StreamlineFolder != null
                    && !(card.StreamlineInstalledVersion?.StartsWith("1.") == true))
                {
                    if (slVersion == DefaultOption)
                    {
                        _dlssService.RestoreStreamline(detection.StreamlineFolder); slCount++;
                    }
                    else if (slVersion == CustomOption)
                    {
                        await _dlssService.SwapStreamlineCustomAsync(detection.StreamlineFolder); slCount++;
                    }
                    else
                    {
                        var installed = DlssStreamlineService.FormatVersion(_dlssService.GetFileVersion(detection.StreamlineInterposerPath!));
                        if (installed == slVersion) { skippedAlreadyAtVersion++; }
                        else { await _dlssService.SwapStreamlineAsync(detection.StreamlineFolder, slVersion!); slCount++; }
                    }
                }
                else if (slVersion != NoneOption && detection.StreamlineFolder == null) { skippedNoComponent++; }

                // ── Apply presets ──
                if (anyPresetSelected)
                {
                    var profileCountBefore = presetService.IsSupported ? 0 : -1; // can't track if not supported
                    bool presetApplied = false;

                    if (srPresetSelection != NoneOption)
                    {
                        var srValue = GetPresetValue(DlssPresetService.SrPresets, srPresetSelection!);
                        if (presetService.SetSrPreset(card.GameName, card.InstallPath, srValue))
                        { srPresetCount++; presetApplied = true; }
                        else if (!presetApplied) presetMissedCount++;
                    }
                    if (rrPresetSelection != NoneOption)
                    {
                        var rrValue = GetPresetValue(DlssPresetService.RrPresets, rrPresetSelection!);
                        if (presetService.SetRrPreset(card.GameName, card.InstallPath, rrValue))
                        { rrPresetCount++; presetApplied = true; }
                        else if (!presetApplied) presetMissedCount++;
                    }
                    if (fgPresetSelection != NoneOption)
                    {
                        var fgValue = GetPresetValue(DlssPresetService.FgPresets, fgPresetSelection!);
                        if (presetService.SetFgPreset(card.GameName, card.InstallPath, fgValue))
                        { fgPresetCount++; presetApplied = true; }
                        else if (!presetApplied) presetMissedCount++;
                    }
                }
            }
            catch (DlssSwapException ex)
            {
                // Keep going with the other games; report this one at the end
                CrashReporter.Log($"[MassDlssDeployDialog] '{card.GameName}' failed — {ex.Message}");
                failedGames.Add($"{card.GameName}: {ex.Message}");
            }

            // Refresh card versions
            card.RefreshDlssVersions(_dlssService);
            card.NotifyAll();
        }

        // Close progress dialog — release gate explicitly before Hide
        await progressSession.DisposeAsync();

        // Restore auto-create flag
        presetService.AutoCreateProfiles = previousAutoCreate;

        // ── Show results ──
        var report = new System.Text.StringBuilder();
        if (dlssCount > 0) report.AppendLine($"DLSS SR을 게임 {dlssCount}개에 배포함");
        if (dlssdCount > 0) report.AppendLine($"DLSS RR을 게임 {dlssdCount}개에 배포함");
        if (dlssgCount > 0) report.AppendLine($"DLSS FG를 게임 {dlssgCount}개에 배포함");
        if (slCount > 0) report.AppendLine($"Streamline을 게임 {slCount}개에 배포함");
        if (srPresetCount > 0) report.AppendLine($"SR 프리셋을 게임 {srPresetCount}개에 적용함");
        if (rrPresetCount > 0) report.AppendLine($"RR 프리셋을 게임 {rrPresetCount}개에 적용함");
        if (fgPresetCount > 0) report.AppendLine($"FG 프리셋을 게임 {fgPresetCount}개에 적용함");
        if (srDriverCount > 0) report.AppendLine($"SR NVIDIA Override를 게임 {srDriverCount}개에 설정함");
        if (rrDriverCount > 0) report.AppendLine($"RR NVIDIA Override를 게임 {rrDriverCount}개에 설정함");
        if (fgDriverCount > 0) report.AppendLine($"FG NVIDIA Override를 게임 {fgDriverCount}개에 설정함");
        if (autoCreateProfiles && presetService.ProfilesCreatedCount > 0)
            report.AppendLine($"생성된 NVIDIA 프로필: {presetService.ProfilesCreatedCount}");
        if (skippedAlreadyAtVersion > 0) report.AppendLine($"\n건너뜀: {skippedAlreadyAtVersion} (이미 선택한 버전임)");
        if (skippedNoComponent > 0) report.AppendLine($"건너뜀: {skippedNoComponent} (구성 요소 없음)");
        if (presetMissedCount > 0) report.AppendLine($"프리셋 미적용: 게임 {presetMissedCount}개 (NVIDIA 프로필 없음)");
        if (failedGames.Count > 0)
        {
            report.AppendLine($"\n실패: 게임 {failedGames.Count}개");
            foreach (var f in failedGames) report.AppendLine($"  • {f}");
        }

        if (report.Length == 0) report.Append("변경된 내용이 없습니다.");

        var resultDialog = new ContentDialog
        {
            Title = "일괄 배포 완료",
            Content = new TextBlock { Text = report.ToString().TrimEnd(), TextWrapping = TextWrapping.Wrap, FontSize = 12 },
            CloseButtonText = "확인",
            XamlRoot = _xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        await DialogService.ShowSafeAsync(resultDialog);
        _onComplete?.Invoke();
    }

    private async Task ExecuteRestoreAllAsync(List<GameCardViewModel> eligibleCards, List<CheckBox> checkBoxes)
    {
        int restoredCount = 0;
        int presetsResetCount = 0;
        var presetService = App.Services.GetRequiredService<DlssPresetService>();

        // Show progress
        var progressText = new TextBlock
        {
            Text = "선택한 게임 복원 중...",
            FontSize = 12,
            Foreground = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 200, 200, 200)),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var progressRing = new ProgressRing { IsActive = true, Width = 24, Height = 24 };
        var progressPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        progressPanel.Children.Add(progressRing);
        progressPanel.Children.Add(progressText);

        var progressContainer = new Grid { MinWidth = 350 };
        progressContainer.Children.Add(progressPanel);

        var progressDialog = new ContentDialog
        {
            Title = "복원 중...",
            Content = progressContainer,
            XamlRoot = _xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        await using var progressSession = await DialogService.ShowProgressAsync(progressDialog, 10);
        if (progressSession == null) return;

        for (int i = 0; i < eligibleCards.Count; i++)
        {
            var card = eligibleCards[i];
            var cb = checkBoxes[i];
            if (cb.IsChecked != true) continue;

            var detection = card.DlssDetection;
            if (detection == null) continue;

            progressText.Text = $"{card.GameName} 복원 중...";
            await Task.Delay(1);

            // Restore DLL backups
            if (card.HasAnyDlssBackup)
            {
                _dlssService.RestoreAll(detection);
                card.RefreshDlssVersions(_dlssService);
                card.NotifyAll();
                restoredCount++;
            }

            // Reset presets to Default (0x0) and clear driver overrides
            bool presetReset = false;
            if (presetService.SetSrPreset(card.GameName, card.InstallPath, 0)) presetReset = true;
            if (presetService.SetRrPreset(card.GameName, card.InstallPath, 0)) presetReset = true;
            if (presetService.SetFgPreset(card.GameName, card.InstallPath, 0)) presetReset = true;
            if (presetService.IsSupported)
                presetService.ClearAllDriverOverrides(card.GameName, card.InstallPath ?? "");
            if (presetReset) presetsResetCount++;
        }

        await progressSession.DisposeAsync();

        var reportText = new System.Text.StringBuilder();
        if (restoredCount > 0) reportText.AppendLine($"게임 {restoredCount}개를 기본 DLL로 복원했습니다.");
        if (presetsResetCount > 0) reportText.AppendLine($"게임 {presetsResetCount}개의 프리셋을 기본값으로 초기화했습니다.");
        if (restoredCount == 0 && presetsResetCount == 0) reportText.Append("복원할 백업이나 초기화할 프리셋이 있는 게임이 없습니다.");

        var resultDialog = new ContentDialog
        {
            Title = "복원 완료",
            Content = new TextBlock
            {
                Text = reportText.ToString().TrimEnd(),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
            },
            CloseButtonText = "확인",
            XamlRoot = _xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        await DialogService.ShowSafeAsync(resultDialog);
        _onComplete?.Invoke();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsV1Game(GameCardViewModel card)
    {
        // No longer used for greying out — games with v1.x are selectable.
        // Per-component v1.x skipping is handled during deployment.
        return false;
    }

    private const string NvidiaOverrideOption = "NVIDIA Override";

    private ComboBox BuildVersionCombo(IReadOnlyList<string> versions, string savedDefault = "", bool includeDriverOverride = false, bool currentDriverOverride = false)
    {
        var items = new List<string> { NoneOption, DefaultOption };
        items.AddRange(versions);
        items.Add(CustomOption);
        if (includeDriverOverride)
            items.Add(NvidiaOverrideOption);

        int selectedIdx = 0; // Default to "None"
        if (includeDriverOverride && currentDriverOverride)
        {
            selectedIdx = items.Count - 1; // "NVIDIA Override" is last
        }
        else if (!string.IsNullOrEmpty(savedDefault))
        {
            var idx = items.IndexOf(savedDefault);
            if (idx >= 0) selectedIdx = idx;
        }

        return new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = selectedIdx,
            FontSize = 12,
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }

    private static StackPanel BuildDropdownSection(string label, ComboBox combo)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 200, 210, 230)),
        });
        panel.Children.Add(combo);
        return panel;
    }

    private static ComboBox BuildPresetCombo((string Name, uint Value)[] presets, uint savedDefault = 0)
    {
        var items = new List<string> { NoneOption };
        foreach (var (name, _) in presets)
            items.Add(name);

        int selectedIdx = 0; // Default to "None"
        if (savedDefault != 0)
        {
            var presetIdx = Array.FindIndex(presets, p => p.Value == savedDefault);
            if (presetIdx >= 0) selectedIdx = presetIdx + 1; // +1 for "None" at index 0
        }

        var presetCombo = new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = selectedIdx,
            FontSize = 12,
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(presetCombo,
            "각 게임의 NVIDIA 드라이버 프로필을 통해 적용되는 프리셋입니다. None은 각 게임의 현재 프리셋을 그대로 둡니다.");
        return presetCombo;
    }

    private static uint GetPresetValue((string Name, uint Value)[] presets, string selection)
    {
        foreach (var (name, value) in presets)
        {
            if (string.Equals(name, selection, StringComparison.OrdinalIgnoreCase))
                return value;
        }
        return 0; // Default
    }
}
