// SettingsHandler.CustomFolders.cs -- "Custom game folders" card at the top of Settings › Tools & Maintenance:
// folders RHI scans for DRM-free / non-store games (MainViewModel.CustomFolders.cs). Lists the folders with
// Remove, plus Add folder, Scan now, Review (when games are waiting) and Forget ignored games.
// Built in C# (XAML constraint), inserted the first time Settings opens.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private StackPanel? _customFoldersList;
    private TextBlock? _customFoldersStatus;
    private Button? _customFoldersReviewBtn;
    private Button? _customFoldersForgetBtn;
    private Button? _customFoldersScanBtn;
    private bool _customFoldersCardFailed;

    private void EnsureCustomFoldersCard()
    {
        if (_customFoldersList != null) { RefreshCustomFoldersCard(); return; }
        if (_customFoldersCardFailed) return;
        try
        {
            StackPanel? section = null;
            foreach (var child in _window.SettingsCardsPanel.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "Section:Tools") { section = sp; break; }
            if (section == null)
            {
                _customFoldersCardFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureCustomFoldersCard] Tools section not found — card not added");
                return;
            }

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var body = new StackPanel { Spacing = 10 };
            card.Child = body;

            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock { Text = "사용자 지정 게임 폴더", Style = FindStyle("SettingsCardTitleStyle") });
            head.Children.Add(new TextBlock
            {
                Text = "DRM 없는 다운로드 등 지원되는 스토어 외부의 게임을 BONG DLSS5가 확인하는 폴더입니다. "
                     + "그 안의 각 폴더는 하나의 게임으로 처리됩니다. 찾은 게임은 추가할지 묻기만 하며, 자동으로 추가되지 않습니다.",
                Style = FindStyle("SettingsCardDescStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            body.Children.Add(head);

            _customFoldersList = new StackPanel { Spacing = 6 };
            body.Children.Add(_customFoldersList);

            var buttons = new WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
            var addBtn = MakeCustomFoldersButton("폴더 추가", primary: true,
                "게임이 들어 있는 폴더(하위 폴더당 게임 하나) 또는 단일 게임 폴더를 선택하세요.");
            addBtn.Click += async (s, e) =>
            {
                try { await AddCustomFolderAsync(); }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.CustomFolders] Add failed — {ex.Message}"); }
            };
            buttons.Children.Add(addBtn);

            _customFoldersScanBtn = MakeCustomFoldersButton("지금 검색", primary: false,
                "지금 사용자 지정 폴더에서 새 게임을 찾습니다. BONG DLSS5는 실행 시와 새로 고침 시에도 확인합니다.");
            _customFoldersScanBtn.Click += async (s, e) =>
            {
                try { await ScanCustomFoldersNowAsync(); }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.CustomFolders] Scan failed — {ex.Message}"); }
            };
            buttons.Children.Add(_customFoldersScanBtn);

            _customFoldersReviewBtn = MakeCustomFoldersButton("검토", primary: false,
                "사용자 지정 폴더에서 찾은 게임 중 추가할 게임을 선택하세요.");
            _customFoldersReviewBtn.Click += async (s, e) =>
            {
                try { await _window.ShowCustomGamesReviewAsync(); RefreshCustomFoldersCard(); }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.CustomFolders] Review failed — {ex.Message}"); }
            };
            buttons.Children.Add(_customFoldersReviewBtn);

            _customFoldersForgetBtn = MakeCustomFoldersButton("무시한 게임 잊기", primary: false,
                "무시했거나 사용자 지정 폴더에서 추가한 후 제거한 게임을 다음 검색 때 다시 제안합니다.");
            _customFoldersForgetBtn.Click += (s, e) =>
            {
                try
                {
                    ViewModel.ClearIgnoredCustomGameFolders();
                    SetCustomFoldersStatus("무시한 게임은 다음 검색 때 다시 제안됩니다.", error: false);
                    RefreshCustomFoldersCard();
                }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.CustomFolders] Forget ignored failed — {ex.Message}"); }
            };
            buttons.Children.Add(_customFoldersForgetBtn);
            body.Children.Add(buttons);

            _customFoldersStatus = new TextBlock
            {
                Style = FindStyle("SettingsNoteStyle"),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };
            body.Children.Add(_customFoldersStatus);

            // After the heading, before the Tools card
            section.Children.Insert(Math.Min(1, section.Children.Count), card);
            ViewModel.PendingCustomGamesChanged += () => { if (_customFoldersList != null) RefreshCustomFoldersCard(); };
            RefreshCustomFoldersCard();
        }
        catch (Exception ex)
        {
            _customFoldersCardFailed = true;
            CrashReporter.Log($"[SettingsHandler.EnsureCustomFoldersCard] Failed — {ex.Message}");
        }
    }

    private static Button MakeCustomFoldersButton(string text, bool primary, string tooltip)
    {
        var btn = new Button { Content = text, Style = FindStyle(primary ? "SettingsPrimaryButtonStyle" : "SettingsButtonStyle") };
        var key = primary ? "SettingsButtonBlueBrush" : ResourceKeys.SettingsLabelBrush;
        if (Application.Current.Resources.TryGetValue(key, out var v) && v is Brush brush)
        {
            btn.Foreground = brush;
            // Keep the text colour on hover / press (WinUI hover colour sticks otherwise)
            btn.Resources["ButtonForegroundPointerOver"] = brush;
            btn.Resources["ButtonForegroundPressed"] = brush;
        }
        ToolTipService.SetToolTip(btn, tooltip);
        return btn;
    }

    private void RefreshCustomFoldersCard()
    {
        if (_customFoldersList == null) return;
        try
        {
            _customFoldersList.Children.Clear();
            var folders = ViewModel.CustomGameFolders;
            if (folders.Count == 0)
            {
                _customFoldersList.Children.Add(new TextBlock
                {
                    Text = "아직 폴더가 없습니다.",
                    Style = FindStyle("SettingsNoteStyle"),
                });
            }
            foreach (var folder in folders)
            {
                var row = new Grid { ColumnSpacing = 10 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = new TextBlock
                {
                    Text = folder,
                    Style = FindStyle("SettingsLabelStyle"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsTextSelectionEnabled = true,
                };
                ToolTipService.SetToolTip(label, folder);
                row.Children.Add(label);
                var remove = MakeCustomFoldersButton("제거", primary: false,
                    "이 폴더 확인을 중지합니다. 이미 추가된 게임은 목록에 남습니다.");
                var captured = folder;
                remove.Click += (s, e) =>
                {
                    try
                    {
                        ViewModel.RemoveCustomGameFolder(captured);
                        SetCustomFoldersStatus($"{captured}을(를) 제거했습니다.", error: false);
                        RefreshCustomFoldersCard();
                    }
                    catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.CustomFolders] Remove '{captured}' failed — {ex.Message}"); }
                };
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                _customFoldersList.Children.Add(row);
            }

            int pending = ViewModel.PendingCustomGames.Count;
            if (_customFoldersReviewBtn != null)
            {
                _customFoldersReviewBtn.Content = pending == 1 ? "새 게임 1개 검토" : $"새 게임 {pending}개 검토";
                _customFoldersReviewBtn.Visibility = pending > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            int ignored = ViewModel.IgnoredCustomGameFolders.Count;
            if (_customFoldersForgetBtn != null)
            {
                _customFoldersForgetBtn.Content = $"무시한 게임 잊기 ({ignored})";
                _customFoldersForgetBtn.Visibility = ignored > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_customFoldersScanBtn != null)
                _customFoldersScanBtn.IsEnabled = folders.Count > 0;
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.RefreshCustomFoldersCard] {ex.Message}");
        }
    }

    private void SetCustomFoldersStatus(string text, bool error)
    {
        if (_customFoldersStatus == null) return;
        _customFoldersStatus.Text = text;
        _customFoldersStatus.Foreground = UIFactory.Brush(error ? ResourceKeys.AccentRedBrush : ResourceKeys.TextSecondaryBrush);
        _customFoldersStatus.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task AddCustomFolderAsync()
    {
        var folder = await _window.PickFolderAsync(ViewModel.CustomGameFolders.LastOrDefault());
        if (string.IsNullOrEmpty(folder)) return;
        var error = ViewModel.AddCustomGameFolder(folder);
        if (error != null)
        {
            SetCustomFoldersStatus(error, error: true);
            return;
        }
        RefreshCustomFoldersCard();
        SetCustomFoldersStatus($"{CustomFolderScanService.NormalizePath(folder)}을(를) 추가했습니다. 게임을 확인하는 중...", error: false);
        await ScanCustomFoldersNowAsync();
    }

    private async Task ScanCustomFoldersNowAsync()
    {
        if (ViewModel.CustomGameFolders.Count == 0)
        {
            SetCustomFoldersStatus("먼저 폴더를 추가하세요.", error: false);
            return;
        }
        if (_customFoldersScanBtn != null) { _customFoldersScanBtn.IsEnabled = false; _customFoldersScanBtn.Content = "검색 중..."; }
        IReadOnlyList<Models.CustomGameCandidate>? found;
        try
        {
            found = await ViewModel.ScanCustomFoldersAsync("ScanNow");
        }
        catch (OperationCanceledException) { return; }
        finally
        {
            if (_customFoldersScanBtn != null) { _customFoldersScanBtn.Content = "지금 검색"; _customFoldersScanBtn.IsEnabled = true; }
        }
        if (_window.IsShuttingDown) return;
        if (found == null)
        {
            SetCustomFoldersStatus("이미 검색이 진행 중입니다. 잠시 후 다시 시도하세요.", error: false);
            return;
        }
        if (found.Count == 0)
        {
            SetCustomFoldersStatus("사용자 지정 폴더에서 새 게임을 찾지 못했습니다.", error: false);
            RefreshCustomFoldersCard();
            return;
        }
        SetCustomFoldersStatus(found.Count == 1 ? "새 게임 1개를 찾았습니다." : $"새 게임 {found.Count}개를 찾았습니다.", error: false);
        RefreshCustomFoldersCard();
        // The user asked for this scan, so the review opens straight away
        await _window.ShowCustomGamesReviewAsync();
        RefreshCustomFoldersCard();
    }
}
