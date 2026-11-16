// MainWindow.CustomGames.cs -- the notice and review dialog for games found in the user's custom game
// folders (MainViewModel.CustomFolders.cs). The notice is a small link button in the status bar (present
// in every layout: Classic+, Tabs and Spotlight restyle the footer but keep it), never a modal; the
// review dialog only opens when the user clicks it (or after Settings › Scan now).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private Button? _customGamesNotice;
    private bool _customGamesNoticeDismissed;
    private bool _customGamesReviewOpen;

    private void HookCustomGamesNotice()
    {
        ViewModel.PendingCustomGamesChanged += () =>
        {
            // A new scan result shows the notice again even after "Not now"
            _customGamesNoticeDismissed = false;
            UpdateCustomGamesNotice();
        };
    }

    private void UpdateCustomGamesNotice()
    {
        try
        {
            if (IsShuttingDown) return;
            int n = ViewModel.PendingCustomGames.Count;
            if (_customGamesNotice == null)
            {
                if (n == 0) return;
                if (StatusBarVersionText.Parent is not StackPanel host)
                {
                    CrashReporter.Log("[MainWindow.UpdateCustomGamesNotice] Status bar panel not found — notice not shown");
                    return;
                }
                _customGamesNotice = new Button
                {
                    FontSize = 11,
                    Padding = new Thickness(8, 2, 8, 2),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush),
                };
                ToolTipService.SetToolTip(_customGamesNotice,
                    "RHI가 사용자 지정 게임 폴더(설정 › 도구 및 유지 관리)에서 아직 목록에 없는 게임을 찾았습니다. 클릭하여 추가할 게임을 선택하세요.");
                _customGamesNotice.Click += async (s, e) =>
                {
                    try { await ShowCustomGamesReviewAsync(); }
                    catch (Exception ex) { CrashReporter.Log($"[MainWindow.CustomGamesNotice] Review failed — {ex.Message}"); }
                };
                host.Children.Insert(0, _customGamesNotice);
            }
            _customGamesNotice.Content = n == 1
                ? "사용자 지정 폴더에서 새 게임 1개 발견 · 검토"
                : $"사용자 지정 폴더에서 새 게임 {n}개 발견 · 검토";
            _customGamesNotice.Visibility = n > 0 && !_customGamesNoticeDismissed ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.UpdateCustomGamesNotice] {ex.Message}");
        }
    }

    /// <summary>Review dialog for the pending candidates. Returns how many games were added.</summary>
    internal async Task<int> ShowCustomGamesReviewAsync()
    {
        if (_customGamesReviewOpen || IsShuttingDown) return 0;
        var candidates = ViewModel.PendingCustomGames.ToList();
        if (candidates.Count == 0) { UpdateCustomGamesNotice(); return 0; }
        if (Content?.XamlRoot is not { } root) return 0;

        _customGamesReviewOpen = true;
        try
        {
            var rows = new List<(CustomGameCandidate C, CheckBox Check, TextBox Name, ComboBox Exe)>();
            var list = new StackPanel { Spacing = 8 };
            foreach (var c in candidates)
            {
                var check = new CheckBox { IsChecked = true, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
                ToolTipService.SetToolTip(check, "이 게임 포함");
                var nameBox = new TextBox { Text = c.SuggestedName, FontSize = 13, PlaceholderText = "게임 이름" };
                ToolTipService.SetToolTip(nameBox, "RHI에 표시되는 이름입니다. RHI가 모드를 찾을 수 있도록 게임의 실제 제목을 사용하세요.");
                var exeCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
                foreach (var exe in c.Exes)
                    exeCombo.Items.Add($"{exe.RelativePath}  ({FormatExeSize(exe.SizeBytes)})");
                exeCombo.SelectedIndex = 0;
                ToolTipService.SetToolTip(exeCombo,
                    "RHI가 실행하고 옆에 설치할 exe입니다. 가장 적합한 항목이 선택되며, 런처이거나 잘못된 버전이면 변경하세요.");

                var head = new Grid { ColumnSpacing = 8 };
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                head.Children.Add(check);
                Grid.SetColumn(nameBox, 1);
                head.Children.Add(nameBox);

                var body = new StackPanel { Spacing = 6 };
                body.Children.Add(head);
                var exeRow = new Grid { ColumnSpacing = 8, Margin = new Thickness(40, 0, 0, 0) };
                exeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                exeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                exeRow.Children.Add(new TextBlock
                {
                    Text = "Exe", FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
                });
                Grid.SetColumn(exeCombo, 1);
                exeRow.Children.Add(exeCombo);
                body.Children.Add(exeRow);
                if (c.IsAmbiguous)
                    body.Children.Add(new TextBlock
                    {
                        Text = "exe를 확인하세요: 게임으로 보이는 파일이 여러 개입니다.",
                        FontSize = 11, Margin = new Thickness(40, 0, 0, 0), TextWrapping = TextWrapping.Wrap,
                        Foreground = UIFactory.Brush(ResourceKeys.AccentAmberBrush),
                    });
                body.Children.Add(new TextBlock
                {
                    Text = $"엔진: {EngineLabel(c.Engine)}  ·  {c.FolderPath}",
                    FontSize = 11, Margin = new Thickness(40, 0, 0, 0), TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                    Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
                });

                list.Children.Add(new Border
                {
                    Child = body,
                    Padding = new Thickness(10, 8, 10, 8),
                    CornerRadius = new CornerRadius(6),
                    BorderThickness = new Thickness(1),
                    BorderBrush = UIFactory.Brush(ResourceKeys.BorderSubtleBrush),
                });
                rows.Add((c, check, nameBox, exeCombo));
            }

            var content = new StackPanel { Spacing = 10 };
            content.Children.Add(new TextBlock
            {
                Text = "사용자 지정 게임 폴더의 이 폴더들은 목록에 없는 게임으로 보입니다. "
                     + "이름과 exe를 확인한 다음 원하는 게임을 추가하세요. 무시한 게임은 다시 제안되지 않습니다.",
                TextWrapping = TextWrapping.Wrap, FontSize = 12,
                Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
            });
            content.Children.Add(new ScrollViewer
            {
                Content = list,
                MaxHeight = 460,
                Padding = new Thickness(0, 0, 14, 0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            });

            var dialog = new ContentDialog
            {
                Title = candidates.Count == 1 ? "사용자 지정 폴더의 새 게임" : $"사용자 지정 폴더의 새 게임 {candidates.Count}개",
                Content = content,
                PrimaryButtonText = "선택 항목 추가",
                SecondaryButtonText = "선택 항목 무시",
                CloseButtonText = "나중에",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = root,
                Background = UIFactory.Brush(ResourceKeys.SurfaceToolbarBrush),
                RequestedTheme = ElementTheme.Dark,
            };
            dialog.Resources["ContentDialogMaxWidth"] = 760.0;

            var result = await DialogService.ShowSafeAsync(dialog);
            var picked = rows.Where(r => r.Check.IsChecked == true).ToList();

            if (result == ContentDialogResult.Secondary)
            {
                ViewModel.IgnoreCustomGames(picked.Select(r => r.C).ToList());
                if (picked.Count > 0)
                    ShowStatusBarMessage(picked.Count == 1 ? "게임 1개 무시됨" : $"게임 {picked.Count}개 무시됨", TimeSpan.FromSeconds(6));
                return 0;
            }
            if (result != ContentDialogResult.Primary)
            {
                // "Not now": hide the notice until the next scan finds something
                _customGamesNoticeDismissed = true;
                UpdateCustomGamesNotice();
                return 0;
            }

            int added = 0;
            var errors = new List<string>();
            foreach (var (c, _, nameBox, exeCombo) in picked)
            {
                var idx = exeCombo.SelectedIndex;
                if (idx < 0 || idx >= c.Exes.Count) idx = 0;
                try
                {
                    var err = ViewModel.AddCustomGame(c, nameBox.Text ?? "", c.Exes[idx]);
                    if (err == null) added++;
                    else errors.Add($"{(string.IsNullOrWhiteSpace(nameBox.Text) ? c.FolderPath : nameBox.Text.Trim())}: {err}");
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[MainWindow.ShowCustomGamesReviewAsync] Adding '{c.FolderPath}' failed — {ex.Message}");
                    errors.Add($"{c.FolderPath}: {ex.Message}");
                }
            }
            CrashReporter.Log($"[MainWindow.ShowCustomGamesReviewAsync] Added {added} of {picked.Count}; {errors.Count} error(s)");
            if (added > 0)
                ShowStatusBarMessage(added == 1 ? "사용자 지정 폴더에서 게임 1개 추가됨" : $"사용자 지정 폴더에서 게임 {added}개 추가됨", TimeSpan.FromSeconds(8));
            UpdateCustomGamesNotice();

            if (errors.Count > 0 && !IsShuttingDown && Content?.XamlRoot is { } root2)
            {
                await DialogService.ShowSafeAsync(new ContentDialog
                {
                    Title = added > 0 ? "일부 게임이 추가되지 않음" : "게임이 추가되지 않음",
                    Content = new ScrollViewer
                    {
                        MaxHeight = 360,
                        Content = new TextBlock { Text = string.Join("\n", errors), TextWrapping = TextWrapping.Wrap, FontSize = 13, IsTextSelectionEnabled = true },
                    },
                    CloseButtonText = "확인",
                    XamlRoot = root2,
                    Background = UIFactory.Brush(ResourceKeys.SurfaceToolbarBrush),
                    RequestedTheme = ElementTheme.Dark,
                });
            }
            return added;
        }
        finally
        {
            _customGamesReviewOpen = false;
        }
    }

    private static string EngineLabel(EngineType e) => e switch
    {
        EngineType.Unreal       => "Unreal Engine",
        EngineType.UnrealLegacy => "Unreal Engine (레거시)",
        EngineType.Unity        => "Unity",
        EngineType.REEngine     => "RE Engine",
        _                       => "알 수 없음",
    };

    private static string FormatExeSize(long bytes)
        => bytes >= 1024L * 1024 * 1024 ? $"{bytes / (1024.0 * 1024 * 1024):0.0} GB"
         : bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024):0.0} MB"
         : $"{Math.Max(1, bytes / 1024)} KB";
}
