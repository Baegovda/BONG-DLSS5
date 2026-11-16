// MainWindow.DisplayModes.cs -- Display modes on the game page: the mode switch (Classic+ header beside HDR / RES;
// Tabs and Spotlight header under the HDR / Resolution toggles, built by DetailLayoutHost.Header), the game menu's
// "Display mode" submenu, the HDR / RES buttons' state when a mode sets them, and the per-game dialog (what the game
// swaps, and its values per mode). Shown only while Settings › Display › Display modes is on.
// Data and file work: MainViewModel.DisplayModes.cs, Services/DisplayMode*.cs.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private StackPanel? _dmClassicHost;
    private bool _dmSwitching;

    /// <summary>Items written to the NVIDIA profile: the DLSS / Driver sections are rebuilt after a switch that wrote one.</summary>
    private static readonly HashSet<string> DmDriverItems = new(StringComparer.OrdinalIgnoreCase)
    {
        DisplayModeItemKeys.DriverFpsCap, DisplayModeItemKeys.MfgMode, DisplayModeItemKeys.DlssSrPreset,
        DisplayModeItemKeys.DlssSrScale, DisplayModeItemKeys.DlssRrPreset, DisplayModeItemKeys.DlssRrScale,
        DisplayModeItemKeys.RtxHdrPeak,
    };

    // ── HDR / RES header buttons ─────────────────────────────────────────────

    /// <summary>Colours and tooltips of the Classic header's HDR / RES buttons (the Tabs header copies them).</summary>
    internal void RefreshHeaderLaunchToggles(GameCardViewModel card)
    {
        var (hdrOn, hdrSource) = ResolveHdrButton(card);
        // Settings › Display can hide either button; what it's set to still applies at launch
        DetailHdrToggleBtn.Visibility = ViewModel.Settings.ShowHdrLaunchButton ? Visibility.Visible : Visibility.Collapsed;
        DetailHdrToggleBtn.Tag = card;
        DetailHdrToggleText.Text = "HDR";
        StyleClassicToggle(DetailHdrToggleBtn, DetailHdrToggleText, hdrOn);
        ToolTipService.SetToolTip(DetailHdrToggleBtn, DetailPanelBuilder.HdrToggleTooltip(hdrOn, hdrSource));

        if (FeatureFlags.ResolutionControl && ViewModel.Settings.ShowResLaunchButton)
        {
            var (resOn, resSource) = ResolveResButton(card);
            DetailResToggleBtn.Visibility = Visibility.Visible;
            DetailResToggleBtn.Tag = card;
            DetailResToggleText.Text = "RES";
            StyleClassicToggle(DetailResToggleBtn, DetailResToggleText, resOn);
            ToolTipService.SetToolTip(DetailResToggleBtn, DetailPanelBuilder.ResToggleTooltip(resOn, resSource));
        }
        else
        {
            DetailResToggleBtn.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Settings › Display showed or hid the HDR / Resolution buttons: update the shown game's header.</summary>
    internal void RefreshLaunchButtonsShown()
    {
        if (ViewModel.SelectedGame is not { } card) return;
        RefreshHeaderLaunchToggles(card);
        _detailPanelBuilder?.Layout.RefreshHeader();
    }

    private static void StyleClassicToggle(Button btn, TextBlock text, bool active)
    {
        btn.Background = UIFactory.Brush(active ? ResourceKeys.AccentPurpleBgBrush : ResourceKeys.SurfaceOverlayBrush);
        btn.BorderBrush = UIFactory.Brush(active ? ResourceKeys.AccentPurpleBorderBrush : ResourceKeys.BorderSubtleBrush);
        text.Foreground = UIFactory.Brush(active ? ResourceKeys.AccentPurpleBrush : ResourceKeys.ChipTextBrush);
    }

    /// <summary>HDR on launch for the game and where that comes from (its display mode, its own setting, or Settings).</summary>
    private (bool On, string Source) ResolveHdrButton(GameCardViewModel card)
    {
        if (ViewModel.DisplayModeOwnsHdr(card) && ViewModel.DisplayModeHdrOn(card) is bool modeOn)
            return (modeOn, $"{ViewModel.ActiveDisplayModeName(card)} 디스플레이 모드에서 설정됨");
        var ov = _gameNameService.HdrToggleOverrides.TryGetValue(card.GameName, out var v) ? v : null;
        return ov != null
            ? (string.Equals(ov, "On", StringComparison.OrdinalIgnoreCase), "이 게임에 설정됨")
            : (ViewModel.Settings.HdrAutoToggle, "설정 › 디스플레이 및 HDR을 따름");
    }

    private (bool On, string Source) ResolveResButton(GameCardViewModel card)
    {
        if (ViewModel.DisplayModeOwnsResolution(card) && ViewModel.DisplayModeResolution(card) is { } mv)
        {
            var name = ViewModel.ActiveDisplayModeName(card);
            if (mv.Equals("Off", StringComparison.OrdinalIgnoreCase)) return (false, $"{name} 디스플레이 모드에서 설정됨");
            if (mv.Equals("On", StringComparison.OrdinalIgnoreCase)) return (true, $"{name} 디스플레이 모드에서 설정됨");
            var r = ResolutionToggleService.DisplayResolution.Parse(mv);
            return (true, $"{name} 디스플레이 모드에서 설정됨: {r?.Label ?? mv}");
        }
        var ov = _gameNameService.ResToggleOverrides.TryGetValue(card.GameName, out var v) ? v : null;
        return ov != null
            ? (string.Equals(ov, "On", StringComparison.OrdinalIgnoreCase), "이 게임에 설정됨")
            : (ViewModel.Settings.ResolutionAutoToggle, "설정 › 디스플레이 및 HDR을 따름");
    }

    /// <summary>HDR button click while the game's display mode sets HDR: flips it for the current mode. False = not handled.</summary>
    private bool ToggleHdrForDisplayMode(GameCardViewModel card)
    {
        if (!ViewModel.DisplayModeOwnsHdr(card)) return false;
        bool on = ResolveHdrButton(card).On;
        if (ViewModel.SetDisplayModeLaunchValue(card, DisplayModeItemKeys.Hdr, on ? "Off" : "On") is { } why)
            ShowStatusBarMessage(why, TimeSpan.FromSeconds(10));
        RefreshHeaderLaunchToggles(card);
        return true;
    }

    private bool ToggleResForDisplayMode(GameCardViewModel card)
    {
        if (!ViewModel.DisplayModeOwnsResolution(card)) return false;
        bool on = ResolveResButton(card).On;
        if (ViewModel.SetDisplayModeLaunchValue(card, DisplayModeItemKeys.Resolution, on ? "Off" : "On") is { } why)
            ShowStatusBarMessage(why, TimeSpan.FromSeconds(10));
        RefreshHeaderLaunchToggles(card);
        return true;
    }

    // ── Mode switch ──────────────────────────────────────────────────────────

    /// <summary>
    /// Classic+ header: mode buttons + ⚙ at the end of the second row (after the mod file badge), built in C#
    /// (XAML constraint). Not in the first row: Launch, HDR, RES and the links already fill it, and with Ultra+ /
    /// UW Fix links the extra chips made the links run under the store / engine badges.
    /// </summary>
    internal void RefreshClassicDisplayModeSwitch(GameCardViewModel card)
    {
        try
        {
            if (_dmClassicHost == null)
            {
                if (DetailInstalledFileBadge.Parent is not Panel row) return;
                _dmClassicHost = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(_dmClassicHost);
                // Modes added / renamed / switched (Settings card, game menu, Switch all): redraw the shown game's switch
                ViewModel.DisplayModesChanged += () => DispatcherQueue.TryEnqueue(() => RefreshDisplayModeUi());
            }
            _dmClassicHost.Children.Clear();
            if (!ViewModel.DisplayModesOn)
            {
                _dmClassicHost.Visibility = Visibility.Collapsed;
                return;
            }
            _dmClassicHost.Visibility = Visibility.Visible;
            // Divider between the mod badges and the mode switch
            _dmClassicHost.Children.Add(new Border { Width = 1, Height = 16, Background = UIFactory.Brush(ResourceKeys.BorderSubtleBrush), Margin = new Thickness(0, 0, 4, 0) });

            var modes = ViewModel.DisplayModeStore.ModesFor(MainViewModel.DisplayModeKey(card));
            var active = ViewModel.ActiveDisplayModeId(card);
            if (modes.Count <= 4)
            {
                foreach (var m in modes)
                {
                    bool on = m.Id.Equals(active, StringComparison.OrdinalIgnoreCase);
                    var text = new TextBlock { Text = m.Name, FontSize = 11 };
                    var b = MakeClassicChip(text);
                    StyleClassicToggle(b, text, on);
                    ToolTipService.SetToolTip(b, ModeButtonTooltip(card, m.Name, on));
                    string? id = on ? null : m.Id; // the active mode again = turn it off
                    b.Click += async (s, e) => await SwitchDisplayModeFromUiAsync(card, id);
                    _dmClassicHost.Children.Add(b);
                }
            }
            else
            {
                var text = new TextBlock { Text = ViewModel.ActiveDisplayModeName(card), FontSize = 11 };
                var b = MakeClassicChip(text);
                StyleClassicToggle(b, text, active != null);
                ToolTipService.SetToolTip(b, "디스플레이 모드. 클릭하여 전환합니다.");
                b.Click += (s, e) => ShowDisplayModeMenu(card, b);
                _dmClassicHost.Children.Add(b);
            }
            var cogText = new TextBlock { Text = "⚙", FontSize = 11 };
            var cog = MakeClassicChip(cogText);
            StyleClassicToggle(cog, cogText, false);
            ToolTipService.SetToolTip(cog, "이 게임의 디스플레이 모드 설정: 현재 모드와 이 게임 전용 사용자 지정 모드");
            cog.Click += async (s, e) => await ShowDisplayModeGameDialogAsync(card);
            _dmClassicHost.Children.Add(cog);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            CrashReporter.Log($"[MainWindow.RefreshClassicDisplayModeSwitch] {ex.Message}");
        }
    }

    private static Button MakeClassicChip(TextBlock content) => new()
    {
        Content = content,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(6, 4, 6, 4), // compact: the Classic+ header row is already full
        MinWidth = 0,
        MinHeight = 0,
        FontSize = 11,
    };

    /// <summary>Tabs / Spotlight header row: one button per mode (purple = active) and a ⚙. Null when modes are off.</summary>
    internal Grid? BuildTabsDisplayModeSwitch(GameCardViewModel card, Action afterChange)
    {
        if (!ViewModel.DisplayModesOn) return null;
        var modes = ViewModel.DisplayModeStore.ModesFor(MainViewModel.DisplayModeKey(card));
        var active = ViewModel.ActiveDisplayModeId(card);
        var grid = new Grid { ColumnSpacing = 8 };

        void AddButton(Button b, bool star)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = star ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            Grid.SetColumn(b, grid.ColumnDefinitions.Count - 1);
            grid.Children.Add(b);
        }

        if (modes.Count <= 4)
        {
            foreach (var m in modes)
            {
                bool on = m.Id.Equals(active, StringComparison.OrdinalIgnoreCase);
                var b = new Button
                {
                    Content = new TextBlock { Text = m.Name, TextTrimming = TextTrimming.CharacterEllipsis },
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(8, 0, 8, 0),
                };
                TabsTheme.Apply(b, DetailLayoutHost.ToggleLook(on), height: 36, fontSize: 12);
                ToolTipService.SetToolTip(b, ModeButtonTooltip(card, m.Name, on));
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, $"{m.Name} 디스플레이 모드");
                string? id = on ? null : m.Id; // the active mode again = turn it off
                b.Click += async (s, e) => { await SwitchDisplayModeFromUiAsync(card, id); afterChange(); };
                AddButton(b, true);
            }
        }
        else
        {
            var b = new Button { Content = $"디스플레이 모드: {ViewModel.ActiveDisplayModeName(card)}", HorizontalAlignment = HorizontalAlignment.Stretch };
            TabsTheme.Apply(b, DetailLayoutHost.ToggleLook(active != null), height: 36, fontSize: 12);
            ToolTipService.SetToolTip(b, "디스플레이 모드. 클릭하여 전환합니다.");
            b.Click += (s, e) => ShowDisplayModeMenu(card, b, afterChange);
            AddButton(b, true);
        }

        var cog = new Button { Width = 36, Padding = new Thickness(0), Content = TabsTheme.Icon("", 15) };
        TabsTheme.Apply(cog, TabsTheme.IconButton with { Fg = TabsTheme.TextSecondary }, height: 36);
        ToolTipService.SetToolTip(cog, "이 게임의 디스플레이 모드 설정: 현재 모드와 이 게임 전용 사용자 지정 모드");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(cog, "디스플레이 모드 설정");
        cog.Click += async (s, e) => { await ShowDisplayModeGameDialogAsync(card); afterChange(); };
        AddButton(cog, false);
        return grid;
    }

    private string ModeButtonTooltip(GameCardViewModel card, string modeName, bool on)
    {
        var items = ViewModel.DisplayModeStore.ItemsFor(MainViewModel.DisplayModeKey(card))
            .Select(DisplayModeItems.Get).Where(i => i != null).Select(i => i!.Label).ToList();
        var what = items.Count == 0 ? "아직 없음 (설정 › 디스플레이 › 디스플레이 모드에서, 또는 게임별로 ⚙에서 모드가 바꿀 항목을 지정하세요)" : string.Join(", ", items);
        return on
            ? $"이 게임에 {modeName} 모드가 활성화되어 있습니다.\n변경 항목: {what}.\n클릭하여 끄면 게임이 전역 설정으로 돌아갑니다."
            : $"이 게임을 {modeName} 모드로 전환합니다.\n변경 항목: {what}.";
    }

    private void ShowDisplayModeMenu(GameCardViewModel card, FrameworkElement anchor, Action? afterChange = null)
    {
        var menu = new MenuFlyout();
        var active = ViewModel.ActiveDisplayModeId(card);
        var off = new ToggleMenuFlyoutItem { Text = "꺼짐 (전역 설정)", IsChecked = active == null };
        off.Click += async (s, e) => { await SwitchDisplayModeFromUiAsync(card, null); afterChange?.Invoke(); };
        menu.Items.Add(off);
        foreach (var m in ViewModel.DisplayModeStore.ModesFor(MainViewModel.DisplayModeKey(card)))
        {
            var item = new ToggleMenuFlyoutItem { Text = m.Name, IsChecked = m.Id.Equals(active, StringComparison.OrdinalIgnoreCase) };
            var id = m.Id;
            item.Click += async (s, e) => { await SwitchDisplayModeFromUiAsync(card, id); afterChange?.Invoke(); };
            menu.Items.Add(item);
        }
        menu.ShowAt(anchor);
    }

    /// <summary>Switches the game (null = turn its mode off) and reports the result (status bar, controller toast). Refreshes the header.</summary>
    internal async Task SwitchDisplayModeFromUiAsync(GameCardViewModel card, string? modeId)
    {
        if (_dmSwitching) return;
        _dmSwitching = true;
        try
        {
            string? refusal = null;
            var result = await ViewModel.SwitchDisplayModeAsync(card, modeId, r => refusal = r);
            var message = refusal ?? result?.Summary;
            if (message != null)
            {
                ShowStatusBarMessage(message, TimeSpan.FromSeconds(10));
                if (_padHints?.Visibility == Visibility.Visible) ShowPadToast(message, 5000);
            }
            if (result == null) return;
            RefreshDisplayModeUi(card);
            if (result.Items.Any(i => i.Written && DmDriverItems.Contains(i.Item.Key))
                && ReferenceEquals(ViewModel.SelectedGame, card))
                _detailPanelBuilder.BuildOverridesPanel(card); // DLSS / Driver sections show the new values
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            CrashReporter.Log($"[MainWindow.SwitchDisplayModeFromUiAsync] '{card.GameName}' — {ex.Message}");
            ShowStatusBarMessage($"디스플레이 모드를 전환할 수 없습니다: {ex.Message}", TimeSpan.FromSeconds(8));
        }
        finally
        {
            _dmSwitching = false;
        }
    }

    /// <summary>Redraws the mode switch and HDR / RES buttons for the shown game.</summary>
    internal void RefreshDisplayModeUi(GameCardViewModel? card = null)
    {
        card ??= ViewModel.SelectedGame;
        if (card == null || !ReferenceEquals(ViewModel.SelectedGame, card)) return;
        RefreshHeaderLaunchToggles(card);
        RefreshClassicDisplayModeSwitch(card);
        _detailPanelBuilder.Layout.RefreshHeader();
    }

    /// <summary>The game menu's "Display mode" submenu (null while modes are off).</summary>
    private MenuFlyoutSubItem? BuildDisplayModeMenu(GameCardViewModel card)
    {
        if (!ViewModel.DisplayModesOn) return null;
        var sub = new MenuFlyoutSubItem { Text = "디스플레이 모드" };
        var active = ViewModel.ActiveDisplayModeId(card);
        var off = new ToggleMenuFlyoutItem { Text = "꺼짐 (전역 설정)", IsChecked = active == null };
        off.Click += async (s, e) =>
        {
            try { await SwitchDisplayModeFromUiAsync(card, null); }
            catch (Exception ex) when (ex is InvalidOperationException) { CrashReporter.Log($"[MainWindow.DisplayModeMenu] {ex.Message}"); }
        };
        sub.Items.Add(off);
        foreach (var m in ViewModel.DisplayModeStore.ModesFor(MainViewModel.DisplayModeKey(card)))
        {
            var item = new ToggleMenuFlyoutItem { Text = m.Name, IsChecked = m.Id.Equals(active, StringComparison.OrdinalIgnoreCase) };
            var id = m.Id;
            item.Click += async (s, e) =>
            {
                try { await SwitchDisplayModeFromUiAsync(card, id); }
                catch (Exception ex) when (ex is InvalidOperationException) { CrashReporter.Log($"[MainWindow.DisplayModeMenu] {ex.Message}"); }
            };
            sub.Items.Add(item);
        }
        sub.Items.Add(new MenuFlyoutSeparator());
        var settings = new MenuFlyoutItem { Text = "디스플레이 모드 설정..." };
        settings.Click += async (s, e) => await ShowDisplayModeGameDialogAsync(card);
        sub.Items.Add(settings);
        return sub;
    }

    // ── Per-game dialog ──────────────────────────────────────────────────────

    /// <summary>The game's mode, and its own Custom mode (made here, shown only for this game).</summary>
    internal async Task ShowDisplayModeGameDialogAsync(GameCardViewModel card)
    {
        try
        {
            var vm = ViewModel;
            var store = vm.DisplayModeStore;
            var key = MainViewModel.DisplayModeKey(card);
            var items = DisplayModeItems.All;

            var ctx = vm.BuildDisplayModeContext(card);
            var reasons = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string?> inGame = new(StringComparer.OrdinalIgnoreCase);
            await Task.Run(() =>
            {
                foreach (var i in items) reasons[i.Key] = DisplayModeService.Unavailable(ctx, i);
                inGame = vm.DisplayModeService.ReadCurrent(ctx, items);
            });

            var cog = new CogDialog("디스플레이 모드 설정", Content.XamlRoot);
            cog.AddIntro($"{card.GameName}의 디스플레이 모드를 선택하세요. 설정 › 디스플레이 › 디스플레이 모드의 모드는 모든 게임에 적용됩니다. " +
                         "아래에서 이 게임 전용 사용자 지정 모드를 만들 수 있습니다. 이 모드는 여기서만 다른 모드 옆에 표시되며, " +
                         "체크한 항목만 변경합니다. 꺼짐 = 모드 없음: 게임이 전역 설정을 사용합니다.");
            if (!vm.DisplayModesOn)
                cog.AddWarning("디스플레이 모드가 꺼져 있습니다. 게임 페이지에 전환 버튼을 표시하려면 설정 › 디스플레이에서 켜세요.");
            if (card.IsRunning)
                cog.AddWarning("게임이 실행 중입니다. 모드를 전환하기 전에 게임을 종료하세요.");

            // ── Mode ──
            cog.AddSection("모드");
            var modes = store.ModesFor(key).ToList();
            bool rebuildingCombo = false;
            ComboBox? modeCombo = null;
            var customPanel = new StackPanel { Spacing = 8 };
            List<string> ModeChoices() => new[] { "꺼짐" }.Concat(modes.Select(m => m.Name)).ToList();
            int ActiveIndex() => modes.FindIndex(m => m.Id.Equals(store.ActiveModeId(key) ?? "", StringComparison.OrdinalIgnoreCase)) + 1;
            modeCombo = cog.AddCombo("현재 모드",
                "전환하면 해당 모드의 값이 게임에 기록됩니다. 다시 적용하려면 현재 모드를 한 번 더 선택하세요. " +
                "꺼짐은 전역 설정을 다시 기록합니다 (전역 값이 없는 설정은 모드 사용 전 게임 값으로 복원).",
                ModeChoices(), ActiveIndex(), i =>
                {
                    if (rebuildingCombo) return true;
                    if (!vm.DisplayModesOn) { cog.SetError("먼저 설정 › 디스플레이에서 디스플레이 모드를 켜세요."); return false; }
                    _ = SwitchFromDialogAsync(i == 0 ? null : modes[i - 1].Id);
                    return true;
                });

            void RefreshModeCombo()
            {
                modes = store.ModesFor(key).ToList();
                if (modeCombo == null) return;
                rebuildingCombo = true;
                modeCombo.ItemsSource = ModeChoices();
                modeCombo.SelectedIndex = ActiveIndex();
                rebuildingCombo = false;
            }

            async Task SwitchFromDialogAsync(string? id)
            {
                string? refusal = null;
                var result = await vm.SwitchDisplayModeAsync(card, id, r => refusal = r);
                if (refusal != null) { cog.SetError(refusal); return; }
                if (result == null) return;
                cog.SetSuccess(result.Summary);
                RefreshDisplayModeUi(card);
                inGame = await Task.Run(() => vm.DisplayModeService.ReadCurrent(ctx, items));
                BuildCustom();
            }

            // ── Custom mode for this game ──
            cog.AddSection($"{card.GameName} 전용 사용자 지정 모드");
            cog.AddContent(customPanel);

            void Report(string text, bool error) { if (error) cog.SetError(text); else cog.SetSuccess(text); }

            void BuildCustom()
            {
                customPanel.Children.Clear();
                if (!store.HasCustom(key))
                {
                    customPanel.Children.Add(MutedLine("이 게임 전용 모드를 만듭니다. 변경할 설정을 체크하고 값을 지정하세요. " +
                        "이 게임에서만 다른 모드 옆에 사용자 지정으로 표시되며, 선택되어 있는 동안 다른 모드보다 우선합니다."));
                    customPanel.Children.Add(cog.MakeButton("사용자 지정 모드 만들기", CogButtonKind.Primary, () =>
                    {
                        vm.CreateCustomDisplayMode(card);
                        RefreshModeCombo();
                        BuildCustom();
                        RefreshDisplayModeUi(card);
                        cog.SetSuccess("사용자 지정 모드를 만들었습니다. 변경할 항목을 체크한 다음 현재 모드로 사용자 지정을 선택하세요.");
                    }));
                    return;
                }

                bool inCustom = DisplayModeStore.IsCustom(store.ActiveModeId(key));
                customPanel.Children.Add(MutedLine(inCustom
                    ? "게임이 사용자 지정 모드입니다: 여기서 변경한 내용이 바로 기록됩니다. 체크하지 않은 설정은 게임의 일반 값을 사용합니다."
                    : "사용자 지정 모드가 변경할 설정을 체크하고 값을 지정하세요. 사용자 지정 모드에서 체크하지 않은 설정은 게임의 일반 값을 사용합니다. 사용하려면 위에서 현재 모드로 사용자 지정을 선택하세요."));

                foreach (var group in DisplayModeItems.Groups)
                {
                    var head = TabsTheme.SectionHeading(group);
                    head.FontSize = 11;
                    head.Margin = new Thickness(0, 6, 0, 0);
                    customPanel.Children.Add(head);
                    foreach (var item in items.Where(i => i.Group == group))
                        customPanel.Children.Add(CustomRow(item, inCustom));
                }

                var delete = cog.MakeButton("사용자 지정 모드 삭제", CogButtonKind.Danger, async () =>
                {
                    await vm.DeleteCustomDisplayModeAsync(card);
                    RefreshModeCombo();
                    inGame = await Task.Run(() => vm.DisplayModeService.ReadCurrent(ctx, items));
                    BuildCustom();
                    RefreshDisplayModeUi(card);
                    cog.SetSuccess(inCustom ? "사용자 지정 모드를 삭제했습니다. 게임의 모드가 꺼져 전역 설정으로 돌아갔습니다." : "사용자 지정 모드를 삭제했습니다.");
                }, "이 게임의 사용자 지정 모드를 제거합니다. 게임이 해당 모드에 있으면 먼저 모드를 끕니다.");
                delete.Margin = new Thickness(0, 8, 0, 0);
                customPanel.Children.Add(delete);
            }

            // One setting: tick | name | its value (only while ticked)
            FrameworkElement CustomRow(DisplayModeItem item, bool inCustom)
            {
                bool on = store.InCustom(key, item.Key);
                var reason = reasons.TryGetValue(item.Key, out var r) ? r : null;
                var grid = new Grid { ColumnSpacing = CogDialog.ColumnGap };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CogDialog.LabelColumnWidth + 60) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var cb = new CheckBox
                {
                    Content = new TextBlock { Text = item.Label, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                    IsChecked = on,
                    MinWidth = 0,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ToolTipService.SetToolTip(cb, reason == null ? item.Help : $"{item.Help}\n\n현재 이 게임에서 사용할 수 없음: {reason}.");
                cb.Checked += async (_, _) => await TickAsync(item, true);
                cb.Unchecked += async (_, _) => await TickAsync(item, false);
                grid.Children.Add(cb);

                FrameworkElement right;
                if (on)
                {
                    var unset = item.AtLaunch ? "설정 안 됨 (게임 자체 설정)" : "설정 안 됨 (일반 값)";
                    right = DisplayModeEditor.Build(item, store.CustomValue(key, item.Key), unset,
                        v => vm.SetCustomDisplayModeItemAsync(card, item, true, v), Report);
                    right.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
                else
                {
                    string note = reason != null ? $"현재 사용할 수 없음: {reason}"
                        : item.AtLaunch ? "변경 안 함 (RHI에서 게임을 실행할 때 사용)"
                        : inGame.TryGetValue(item.Key, out var now) && now != null ? $"변경 안 함 (현재 {item.Format(now)})"
                        : "변경 안 함";
                    right = new TextBlock
                    {
                        Text = note,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
                    };
                }
                right.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(right, 1);
                grid.Children.Add(right);
                return grid;
            }

            async Task TickAsync(DisplayModeItem item, bool on)
            {
                // A newly ticked setting starts at what the game has now, so ticking alone changes nothing
                var value = store.CustomValue(key, item.Key)
                    ?? (inGame.TryGetValue(item.Key, out var now) ? now : null);
                var problem = await vm.SetCustomDisplayModeItemAsync(card, item, on, value);
                if (problem != null) cog.SetError(problem);
                BuildCustom();
                RefreshDisplayModeUi(card);
            }

            BuildCustom();

            await cog.ShowAsync();
            RefreshDisplayModeUi(card);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            CrashReporter.Log($"[MainWindow.ShowDisplayModeGameDialogAsync] '{card.GameName}' — {ex.Message}");
        }
    }

    /// <summary>A row like CogDialog.AddRow, for rows rebuilt inside a panel.</summary>
    internal static FrameworkElement MakeValueRow(string label, FrameworkElement control, string? note, string? tooltip)
    {
        var row = new StackPanel { Spacing = 3 };
        var grid = new Grid { ColumnSpacing = CogDialog.ColumnGap };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CogDialog.LabelColumnWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var lbl = new TextBlock
        {
            Text = label,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
        };
        if (!string.IsNullOrEmpty(tooltip)) ToolTipService.SetToolTip(lbl, tooltip);
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        control.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(lbl);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        row.Children.Add(grid);
        if (!string.IsNullOrWhiteSpace(note))
            row.Children.Add(new TextBlock
            {
                Text = note,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(CogDialog.LabelColumnWidth + CogDialog.ColumnGap, 0, 0, 2),
                Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
            });
        return row;
    }

    private static TextBlock MutedLine(string text) => new()
    {
        Text = text,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
    };
}
