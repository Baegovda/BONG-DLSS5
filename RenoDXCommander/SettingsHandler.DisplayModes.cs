// SettingsHandler.DisplayModes.cs -- Settings › Display › "Display modes" card (built in C#, XAML constraint):
// the on/off switch for the game page mode switch, the modes (add / rename / reorder / delete, default values per
// mode, which every game in that mode gets) and "Switch all games". Appended to the Display section the first
// time Settings opens; rebuilt when modes change. Data: MainViewModel.DisplayModes.cs.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private StackPanel? _dmBody;
    private TextBlock? _dmStatus;
    private bool _dmCardFailed;
    private bool _dmBusy;

    private void EnsureDisplayModesCard()
    {
        if (_dmBody != null) { RefreshDisplayModesCard(); return; }
        if (_dmCardFailed) return;
        try
        {
            StackPanel? section = null;
            foreach (var child in _window.SettingsCardsPanel.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "Section:Display") { section = sp; break; }
            if (section == null)
            {
                _dmCardFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureDisplayModesCard] Display section not found — card not added");
                return;
            }

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var outer = new StackPanel { Spacing = 10 };
            card.Child = outer;
            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock { Text = "디스플레이 모드", Style = FindStyle("SettingsCardTitleStyle") });
            head.Children.Add(new TextBlock
            {
                Text = "RHI가 게임에 적용하는 설정 묶음을 저장하고 게임 페이지에서 전환할 수 있습니다. 예를 들어 모니터용 하나, TV용 하나를 두거나 "
                     + "여러 구성을 시험해 볼 때 사용하세요. 모드 자체가 디스플레이를 변경하지는 않습니다. 모드 값은 편집...에서 설정합니다 "
                     + "(최대 밝기, 프레임 제한, 단축키, DLSS 설정, DLDSR, 실행 시 HDR 및 해상도 등): 해당 모드로 전환한 모든 게임에 적용됩니다. "
                     + "특정 게임에만 다른 값을 쓰려면 모드 전환 옆의 ⚙로 사용자 지정 모드를 만드세요.",
                Style = FindStyle("SettingsCardDescStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            outer.Children.Add(head);
            outer.Children.Add(new TextBlock
            {
                Text = "모드가 설정된 게임은 모드가 바꾸는 항목에 대해 자체 값을 유지하므로, RHI의 다른 곳에서 모든 게임에 적용하는 설정"
                     + "(최대 밝기, 프레임 제한, 단축키)은 해당 게임을 건너뜁니다. 게임의 활성 모드를 다시 클릭하거나 꺼짐을 선택하면 모드가 꺼지고 "
                     + "전역 설정으로 돌아갑니다. 새 게임은 모드가 꺼진 상태로 시작합니다.",
                Style = FindStyle("SettingsNoteStyle"),
                Foreground = UIFactory.Brush(ResourceKeys.AccentAmberBrush),
                TextWrapping = TextWrapping.Wrap,
            });

            _dmBody = new StackPanel { Spacing = 10 };
            outer.Children.Add(_dmBody);
            _dmStatus = new TextBlock { Style = FindStyle("SettingsNoteStyle"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            outer.Children.Add(_dmStatus);

            section.Children.Add(card);
            ViewModel.DisplayModesChanged += () => _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (_window.SettingsPanel.Visibility == Visibility.Visible) RefreshDisplayModesCard();
            });
            RefreshDisplayModesCard();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            _dmCardFailed = true;
            _dmBody = null;
            CrashReporter.Log($"[SettingsHandler.EnsureDisplayModesCard] Failed — {ex.Message}");
        }
    }

    private void RefreshDisplayModesCard()
    {
        if (_dmBody == null) return;
        _dmBody.Children.Clear();
        var vm = ViewModel;
        var store = vm.DisplayModeStore;
        var modes = store.Modes;

        // ── On / off ──
        var onCombo = new ComboBox { Style = FindStyle("SettingsComboStyle") };
        onCombo.Items.Add(new ComboBoxItem { Content = "꺼짐" });
        onCombo.Items.Add(new ComboBoxItem { Content = "켜짐" });
        onCombo.SelectedIndex = vm.DisplayModesOn ? 1 : 0;
        ToolTipService.SetToolTip(onCombo, "켜짐이면 게임 페이지(Classic+, Tabs, Spotlight)의 HDR 및 해상도 버튼 옆과 "
            + "게임 메뉴에 모드 전환이 표시됩니다. 꺼짐이면 숨겨지며 게임은 현재 설정을 유지합니다.");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(onCombo, "디스플레이 모드");
        onCombo.SelectionChanged += (s, e) =>
        {
            if (onCombo.SelectedIndex < 0) return;
            vm.SetDisplayModesEnabled(onCombo.SelectedIndex == 1);
        };
        _dmBody.Children.Add(SettingRow("게임 페이지에 표시", onCombo));

        // ── Modes ──
        _dmBody.Children.Add(new TextBlock { Text = "모드", Style = FindStyle("SettingsHeadStyle") });
        for (int i = 0; i < modes.Count; i++)
        {
            var m = modes[i];
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int setCount = DisplayModeItems.All.Count(it => store.ModeDefault(m.Id, it.Key) != null);
            var label = new TextBlock
            {
                Text = setCount == 0 ? $"{m.Name}  (설정 없음)" : $"{m.Name}  (설정 {setCount}개)",
                Style = FindStyle("SettingsLabelStyle"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            row.Children.Add(label);
            var buttons = new Controls.WrapPanel { HorizontalSpacing = 6, VerticalSpacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
            var id = m.Id;
            var name = m.Name;
            buttons.Children.Add(DmButton("편집...", DmKind.Normal, $"{name} 모드에서 모든 게임에 적용되는 값입니다.",
                async () => await ShowModeDefaultsAsync(id)));
            buttons.Children.Add(DmButton("이름 바꾸기", DmKind.Normal, $"{name} 이름 바꾸기", async () =>
            {
                var newName = await PromptModeNameAsync("모드 이름 바꾸기", $"{name}의 새 이름:", "이름 바꾸기", name, id);
                if (newName != null && vm.RenameDisplayMode(id, newName)) SetDmStatus($"{name}의 이름을 {newName}(으)로 바꿨습니다.", false);
            }));
            // Both always shown (greyed at the ends) so the rows line up
            var up = DmButton("위로 이동", DmKind.Normal, "게임 페이지에서 이 모드를 왼쪽으로 이동", async () => { vm.MoveDisplayMode(id, -1); await Task.CompletedTask; });
            up.IsEnabled = i > 0;
            buttons.Children.Add(up);
            var down = DmButton("아래로 이동", DmKind.Normal, "게임 페이지에서 이 모드를 오른쪽으로 이동", async () => { vm.MoveDisplayMode(id, 1); await Task.CompletedTask; });
            down.IsEnabled = i < modes.Count - 1;
            buttons.Children.Add(down);
            if (modes.Count > 1)
                buttons.Children.Add(DmButton("삭제", DmKind.Danger, $"{name}을(를) 삭제합니다. 이 모드의 게임은 모드가 꺼집니다 (전역 설정으로 돌아감).", async () =>
                {
                    if (!await ConfirmAsync("모드 삭제",
                            $"{name}을(를) 삭제할까요? {name} 모드의 게임은 모드가 꺼지고 전역 설정으로 돌아갑니다 " +
                            $"(실행 중인 게임은 꺼짐으로만 설정됨). 게임이 {name}에 대해 기억한 값은 삭제됩니다.",
                            "삭제")) return;
                    var (deleted, turnedOff) = await vm.DeleteDisplayModeAsync(id);
                    if (deleted)
                        SetDmStatus(turnedOff > 0 ? $"{name}을(를) 삭제했습니다. 게임 {turnedOff}개의 모드가 꺼졌습니다." : $"{name}을(를) 삭제했습니다.", false);
                }));
            Grid.SetColumn(buttons, 1);
            row.Children.Add(buttons);
            _dmBody.Children.Add(row);
        }
        var addRow = new Controls.WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
        var addBtn = DmButton("모드 추가", DmKind.Primary, $"모드를 추가합니다 (최대 {DisplayModeStore.MaxModes}개). 예: 휴대용, 프로젝터.", async () =>
        {
            var newName = await PromptModeNameAsync("모드 추가", "새 모드의 이름:", "추가", "", null);
            if (newName == null) return;
            var newId = vm.AddDisplayMode(newName);
            if (newId != null) SetDmStatus($"{newName}을(를) 추가했습니다. 편집...에서 값을 설정하거나 게임 페이지에서 게임별 값을 지정하세요.", false);
        });
        addBtn.IsEnabled = modes.Count < DisplayModeStore.MaxModes;
        addRow.Children.Add(addBtn);
        _dmBody.Children.Add(addRow);

        // ── Switch all ──
        _dmBody.Children.Add(new TextBlock { Text = "모든 게임 전환", Style = FindStyle("SettingsHeadStyle") });
        var allCombo = new ComboBox { Style = FindStyle("SettingsComboStyle"), MinWidth = 140 };
        foreach (var m in modes) allCombo.Items.Add(new ComboBoxItem { Content = m.Name });
        allCombo.Items.Add(new ComboBoxItem { Content = "꺼짐" }); // last: turns every game's mode off
        allCombo.SelectedIndex = 0;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(allCombo, "모든 게임을 전환할 모드");
        var allBtn = DmButton("전환", DmKind.Primary, "모든 게임을 이 모드로 전환하거나(예: 저녁에 TV로 플레이할 때) 모든 게임의 모드를 끕니다. 실행 중이거나 숨긴 게임은 건너뜁니다.",
            async () =>
            {
                int i = allCombo.SelectedIndex;
                if (i < 0 || i > modes.Count) return;
                if (i == modes.Count) await SwitchAllAsync(null, "꺼짐");
                else await SwitchAllAsync(modes[i].Id, modes[i].Name);
            });
        allBtn.IsEnabled = vm.DisplayModesOn;
        var allGrid = new Grid { ColumnSpacing = 8 };
        allGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        allGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        allGrid.Children.Add(allCombo);
        Grid.SetColumn(allBtn, 1);
        allGrid.Children.Add(allBtn);
        _dmBody.Children.Add(SettingRow("모든 게임 전환 대상", allGrid, maxWidth: 300));
        _dmBody.Children.Add(new TextBlock
        {
            Text = vm.DisplayModesOn
                ? "각 게임의 해당 모드 값을 기록합니다 (꺼짐: 전역 설정). 실행 중이거나 숨긴 게임은 건너뜁니다."
                : "먼저 게임 페이지에 표시를 켜세요.",
            Style = FindStyle("SettingsHelpStyle"),
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private Grid SettingRow(string label, FrameworkElement control, double maxWidth = 210)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = maxWidth });
        row.Children.Add(new TextBlock { Text = label, Style = FindStyle("SettingsLabelStyle"), VerticalAlignment = VerticalAlignment.Center });
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    private enum DmKind { Normal, Primary, Danger }

    private Button DmButton(string text, DmKind kind, string tooltip, Func<Task> onClick)
    {
        var style = kind switch
        {
            DmKind.Primary => "SettingsPrimaryButtonStyle",
            DmKind.Danger => "SettingsDangerButtonStyle",
            _ => "SettingsButtonStyle",
        };
        var btn = new Button { Content = text, Style = FindStyle(style) };
        var key = kind switch
        {
            DmKind.Primary => "SettingsButtonBlueBrush",
            DmKind.Danger => ResourceKeys.AccentRedBrush,
            _ => ResourceKeys.SettingsLabelBrush,
        };
        if (Application.Current.Resources.TryGetValue(key, out var v) && v is Brush brush)
        {
            btn.Foreground = brush;
            // Keep the text colour on hover / press (WinUI hover colour sticks otherwise)
            btn.Resources["ButtonForegroundPointerOver"] = brush;
            btn.Resources["ButtonForegroundPressed"] = brush;
        }
        ToolTipService.SetToolTip(btn, tooltip);
        btn.Click += async (s, e) =>
        {
            if (_dmBusy) return;
            _dmBusy = true;
            try { await onClick(); }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            {
                CrashReporter.Log($"[SettingsHandler.DisplayModes] '{text}' failed — {ex.Message}");
                SetDmStatus($"{text} 실패: {ex.Message}", true);
            }
            finally { _dmBusy = false; }
        };
        return btn;
    }

    private void SetDmStatus(string text, bool error)
    {
        if (_dmStatus == null) return;
        _dmStatus.Text = text;
        _dmStatus.Foreground = UIFactory.Brush(error ? ResourceKeys.AccentRedBrush : ResourceKeys.TextSecondaryBrush);
        _dmStatus.Visibility = Visibility.Visible;
    }

    // ── Dialogs ───────────────────────────────────────────────────────────────

    /// <summary>A mode's default values: one row per item, grouped like the per-game list.</summary>
    private async Task ShowModeDefaultsAsync(string modeId)
    {
        var vm = ViewModel;
        var store = vm.DisplayModeStore;
        var name = store.NameOf(modeId);
        if (name == null) return;
        var cog = new CogDialog($"{name} 모드", _window.Content.XamlRoot);
        cog.AddIntro($"게임을 {name} 모드로 전환할 때 적용되는 값입니다. 어느 모드에서든 설정된 항목은 모든 게임에서 변경되며, " +
                     "해당 값이 없는 모드의 게임은 원래 값(전역 설정 또는 디스플레이 모드 이전의 값)으로 " +
                     "돌아갑니다. 비어 있음 = 설정 안 됨. 특정 게임에만 다른 값을 쓰려면 모드 전환 옆의 ⚙로 사용자 지정 모드를 만드세요. " +
                     "이미 이 모드인 게임은 다음에 전환할 때 변경 사항이 적용됩니다.");
        foreach (var group in DisplayModeItems.Groups)
        {
            cog.AddSection(group);
            foreach (var item in DisplayModeItems.All.Where(i => i.Group == group))
            {
                var key = item.Key;
                var editor = DisplayModeEditor.Build(item, store.ModeDefault(modeId, key), "설정 안 됨",
                    v => { vm.SetDisplayModeDefault(modeId, key, v); return Task.FromResult<string?>(null); },
                    (text, error) => { if (error) cog.SetError(text); else cog.SetSuccess(text); });
                cog.AddRow(item.Label, editor, item.Help);
            }
        }
        await cog.ShowAsync();
        RefreshDisplayModesCard();
    }

    /// <summary>Switches every game to a mode, or with a null <paramref name="modeId"/> turns every game's mode off.</summary>
    private async Task SwitchAllAsync(string? modeId, string modeName)
    {
        var vm = ViewModel;
        bool off = modeId == null;
        if (!await ConfirmAsync(off ? "모든 게임 모드 끄기" : "모든 게임 전환",
                off
                    ? "모든 게임의 디스플레이 모드를 끌까요? 각 게임은 전역 설정(전역 값이 없는 설정은 모드 이전의 값)으로 " +
                      "돌아갑니다. 현재 값은 현재 모드용으로 보존됩니다. 실행 중이거나 숨긴 게임은 건너뜁니다."
                    : $"모든 게임을 {modeName} 모드로 전환할까요? 각 게임에 {modeName}용 값이 적용됩니다 (현재 값은 현재 모드용으로 보존됨). " +
                      "실행 중이거나 숨긴 게임은 건너뜁니다.",
                off ? "끄기" : "전환")) return;

        var text = new TextBlock { Text = "시작 중...", TextWrapping = TextWrapping.Wrap };
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6 };
        var dialog = new ContentDialog
        {
            Title = off ? "디스플레이 모드 끄는 중" : $"{modeName}(으)로 전환 중",
            Content = new StackPanel { Spacing = 12, Width = 380, Children = { text, bar } },
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        (int Switched, int Running) result;
        await using (var session = await DialogService.ShowProgressAsync(dialog))
        {
            var progress = new Progress<(string msg, double pct)>(p => { text.Text = p.msg; bar.Value = p.pct; });
            result = await vm.SwitchAllDisplayModesAsync(modeId, progress, _window.LifetimeToken);
        }
        var msg = off
            ? $"게임 {result.Switched}개의 모드를 껐습니다."
            : $"게임 {result.Switched}개를 {modeName}(으)로 전환했습니다.";
        if (result.Running > 0) msg += $" 실행 중인 게임 {result.Running}개를 건너뛰었습니다.";
        SetDmStatus(msg, false);
        _window.ShowStatusBarMessage(msg, TimeSpan.FromSeconds(8));
    }

    private async Task<bool> ConfirmAsync(string title, string body, string primary)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 },
            PrimaryButtonText = primary,
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        return await DialogService.ShowSafeAsync(dialog) == ContentDialogResult.Primary;
    }

    /// <summary>Asks for a mode name, checked as it's typed. Null when cancelled.</summary>
    private async Task<string?> PromptModeNameAsync(string title, string prompt, string primary, string initial, string? renamingId)
    {
        var store = ViewModel.DisplayModeStore;
        var box = new TextBox { Text = initial, MaxLength = DisplayModeStore.MaxNameLength, Width = 340, PlaceholderText = "예: 휴대용" };
        var error = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = TabsTheme.B("#FF9AA8"), Visibility = Visibility.Collapsed };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(box);
        panel.Children.Add(error);
        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primary,
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        string? Check() => store.ValidateName(box.Text, renamingId);
        void ShowCheck(bool showEmpty)
        {
            var problem = Check();
            dialog.IsPrimaryButtonEnabled = problem == null;
            error.Text = problem ?? "";
            error.Visibility = problem != null && (showEmpty || !string.IsNullOrWhiteSpace(box.Text)) ? Visibility.Visible : Visibility.Collapsed;
        }
        box.TextChanged += (s, e) => ShowCheck(false);
        dialog.PrimaryButtonClick += (s, e) => { if (Check() != null) { e.Cancel = true; ShowCheck(true); } };
        box.Loaded += (s, e) => { box.Focus(FocusState.Programmatic); box.SelectAll(); };
        ShowCheck(false);
        if (await DialogService.ShowSafeAsync(dialog) != ContentDialogResult.Primary) return null;
        return box.Text.Trim();
    }
}
