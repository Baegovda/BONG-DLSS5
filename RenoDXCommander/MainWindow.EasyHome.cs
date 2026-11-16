// MainWindow.EasyHome.cs -- BONG DLSS5's simple home screen: pick a game, press one button, DLSS 5 is set up.
// A page laid over the game page (same grid cell as GameViewPanel), shown while the game page is shown and
// Settings.EasyHome is on. The button runs DetailPanelBuilder.RunNrInstallCoreAsync with the recommended method:
// the same steps as the Neural Rendering section's Install button. "상세 설정" hides it to show the full page.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private Grid? _easyRoot;
    private StackPanel? _easyList;
    private TextBox? _easySearch;
    private StackPanel? _easyDetail;
    private Button? _easyToggle;
    private GameCardViewModel? _easyCard;
    private bool _easyBusy;
    private readonly Dictionary<GameCardViewModel, bool> _easyNrState = new();

    private static Brush Res(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b ? b : UIFactory.GetBrush("#20FFFFFF");

    // ── Setup / visibility ────────────────────────────────────────────────────

    /// <summary>Builds the home page once and hooks its visibility to the game page. Safe to call again.</summary>
    private void SetUpEasyHome()
    {
        if (_easyRoot != null) return;
        if (GameViewPanel.Parent is not Panel host) return;

        _easyRoot = BuildEasyHome();
        Grid.SetRow(_easyRoot, Grid.GetRow(GameViewPanel));
        Grid.SetColumn(_easyRoot, Grid.GetColumn(GameViewPanel));
        Grid.SetRowSpan(_easyRoot, Math.Max(1, Grid.GetRowSpan(GameViewPanel)));
        Grid.SetColumnSpan(_easyRoot, Math.Max(1, Grid.GetColumnSpan(GameViewPanel)));
        host.Children.Add(_easyRoot);

        GameViewPanel.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (s, e) => UpdateEasyHomeVisibility());
        ViewModel.DisplayedGames.CollectionChanged += (s, e) => DispatcherQueue.TryEnqueue(RefreshEasyList);
        UpdateEasyHomeVisibility();
        RefreshEasyList();
    }

    private void UpdateEasyHomeVisibility()
    {
        if (_easyRoot == null) return;
        bool show = ViewModel.Settings.EasyHome && GameViewPanel.Visibility == Visibility.Visible;
        _easyRoot.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        // The home page is see-through, so the full game page under it must not show
        GameViewPanel.Opacity = show ? 0 : 1;
        if (_easyToggle != null)
        {
            _easyToggle.Content = ViewModel.Settings.EasyHome ? "상세 화면" : "간편 화면";
            ToolTipService.SetToolTip(_easyToggle, ViewModel.Settings.EasyHome
                ? "모든 설정이 있는 상세 화면으로 바꿔요. 세세하게 조절하고 싶을 때 쓰세요."
                : "게임을 고르고 버튼 하나로 DLSS 5를 적용하는 간편 화면으로 돌아가요.");
        }
        if (show && _easyCard == null) PickDefaultEasyGame();
    }

    private void SetEasyHome(bool on)
    {
        ViewModel.Settings.EasyHome = on;
        ViewModel.SaveSettingsPublic();
        if (!on && _easyCard != null && ViewModel.DisplayedGames.Contains(_easyCard))
            GameList.SelectedItem = _easyCard;
        UpdateEasyHomeVisibility();
    }

    /// <summary>Top bar button that switches between the simple home and the full game page.</summary>
    private Button MakeEasyHomeToggle()
    {
        _easyToggle = new Button { Padding = new Thickness(14, 0, 14, 0) };
        TabsTheme.Apply(_easyToggle, TabsTheme.IconButton, height: 38, fontSize: 14);
        _easyToggle.Content = ViewModel.Settings.EasyHome ? "상세 화면" : "간편 화면";
        _easyToggle.Click += (s, e) =>
        {
            if (ViewModel.CurrentPage != AppPage.GameView) ViewModel.NavigateToGameView();
            SetEasyHome(!ViewModel.Settings.EasyHome);
        };
        return _easyToggle;
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private Grid BuildEasyHome()
    {
        var root = new Grid { Background = UIFactory.GetBrush("#00000000"), // see-through: the window glass shows; the game page under it is hidden by Opacity
            Padding = new Thickness(24, 20, 24, 20), ColumnSpacing = 20 };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left: game picker
        var left = new Grid { RowSpacing = 10 };
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        left.Children.Add(new TextBlock { Text = "게임 고르기", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Res("TextPrimaryBrush") });
        _easySearch = new TextBox { PlaceholderText = "게임 이름 검색", CornerRadius = new CornerRadius(8) };
        _easySearch.TextChanged += (s, e) => RefreshEasyList();
        Grid.SetRow(_easySearch, 1); left.Children.Add(_easySearch);
        _easyList = new StackPanel { Spacing = 4 };
        var listCard = Card(new ScrollViewer { Content = _easyList, Padding = new Thickness(0, 0, 8, 0) }, new Thickness(8));
        Grid.SetRow(listCard, 2); left.Children.Add(listCard);
        root.Children.Add(left);

        // Right: the selected game
        _easyDetail = new StackPanel { Spacing = 14, MaxWidth = 860, HorizontalAlignment = HorizontalAlignment.Left };
        var scroll = new ScrollViewer { Content = _easyDetail, Padding = new Thickness(0, 0, 12, 0) };
        Grid.SetColumn(scroll, 1); root.Children.Add(scroll);
        return root;
    }

    private static Border Card(UIElement child, Thickness? padding = null) => new()
    {
        Background = Res("GlassCardFillBrush"),
        BorderBrush = Res("GlassCardStrokeBrush"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = padding ?? new Thickness(20, 18, 20, 18),
        Child = child,
    };

    private static TextBlock Text(string text, double size = 14, bool bold = false, string brush = "TextSecondaryBrush") => new()
    {
        Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap,
        FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        Foreground = Res(brush),
    };

    // ── Game list ─────────────────────────────────────────────────────────────

    private IEnumerable<GameCardViewModel> EasyGames()
    {
        var q = _easySearch?.Text?.Trim() ?? "";
        return ViewModel.AllCards
            .Where(c => !c.IsHidden && !string.IsNullOrEmpty(c.InstallPath))
            .Where(c => q.Length == 0 || c.GameName.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.GameName, StringComparer.OrdinalIgnoreCase);
    }

    private void RefreshEasyList()
    {
        if (_easyList == null) return;
        _easyList.Children.Clear();
        var games = EasyGames().ToList();
        if (games.Count == 0)
        {
            _easyList.Children.Add(Text(ViewModel.AllCards.Count == 0 ? "게임을 찾는 중이에요..." : "찾는 게임이 없어요.", 13));
            return;
        }
        foreach (var card in games)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = card.GameName, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Foreground = Res("TextPrimaryBrush") });
            if (_easyNrState.TryGetValue(card, out var on) && on)
            {
                var badge = new Border
                {
                    Background = UIFactory.GetBrush("#1E3DDC97"), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 2, 8, 2),
                    Child = new TextBlock { Text = "DLSS 5", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = UIFactory.GetBrush("#3DDC97") },
                };
                ToolTipService.SetToolTip(badge, "이 게임에는 DLSS 5가 적용되어 있어요.");
                Grid.SetColumn(badge, 1); row.Children.Add(badge);
            }
            bool selected = card == _easyCard;
            var btn = new Button
            {
                Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 9, 12, 9), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
                Background = selected ? UIFactory.GetBrush("#334C8AFE") : UIFactory.GetBrush("#00000000"),
                BorderBrush = selected ? UIFactory.GetBrush("#664C8AFE") : UIFactory.GetBrush("#00000000"),
            };
            btn.Resources["ButtonBackgroundPointerOver"] = Res("ControlFillHoverBrush");
            btn.Click += (s, e) => SelectEasyGame(card);
            _easyList.Children.Add(btn);
        }
        _ = ScanEasyNrStateAsync(games);
    }

    /// <summary>Checks which listed games have DLSS 5 (file checks, off the UI thread) and redraws badges that changed.</summary>
    private async Task ScanEasyNrStateAsync(List<GameCardViewModel> games)
    {
        var todo = games.Where(g => !_easyNrState.ContainsKey(g)).ToList();
        if (todo.Count == 0) return;
        var found = await Task.Run(() => todo.Select(g =>
        {
            try { return (g, DetailPanelBuilder.IsNrInstalledIn(g)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return (g, false); }
        }).ToList());
        bool any = false;
        foreach (var (g, on) in found) { _easyNrState[g] = on; any |= on; }
        if (any) RefreshEasyList();
    }

    private void PickDefaultEasyGame()
    {
        var pick = ViewModel.SelectedGame is { } sel && !string.IsNullOrEmpty(sel.InstallPath) ? sel : EasyGames().FirstOrDefault();
        if (pick != null) SelectEasyGame(pick);
    }

    private void SelectEasyGame(GameCardViewModel card)
    {
        _easyCard = card;
        RefreshEasyList();
        ShowEasyDetail(card, null);
        if (!_easyNrState.ContainsKey(card))
            _ = Task.Run(() => DetailPanelBuilder.IsNrInstalledIn(card)).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                    DispatcherQueue.TryEnqueue(() => { _easyNrState[card] = t.Result; if (_easyCard == card && !_easyBusy) ShowEasyDetail(card, null); RefreshEasyList(); });
            }, TaskScheduler.Default);
    }

    // ── Selected game ─────────────────────────────────────────────────────────

    private static string ApiWords(GraphicsApiType api) => api switch
    {
        GraphicsApiType.DirectX12 => "DirectX 12",
        GraphicsApiType.DirectX11 => "DirectX 11",
        GraphicsApiType.DirectX9  => "DirectX 9",
        GraphicsApiType.Vulkan    => "Vulkan",
        GraphicsApiType.OpenGL    => "OpenGL",
        _                         => "그래픽 방식 확인 중",
    };

    /// <param name="progress">Text of the step running now, or null when idle.</param>
    private void ShowEasyDetail(GameCardViewModel card, string? progress, string? result = null, bool failed = false)
    {
        if (_easyDetail == null) return;
        _easyDetail.Children.Clear();
        bool known = _easyNrState.TryGetValue(card, out var nrOn);

        // Hero
        var hero = new StackPanel { Spacing = 6 };
        hero.Children.Add(Text("선택한 게임", 12, false, "TextTertiaryBrush"));
        hero.Children.Add(Text(card.GameName, 28, true, "TextPrimaryBrush"));
        var facts = new List<string> { ApiWords(card.GraphicsApi), card.Is32Bit ? "32비트" : "64비트" };
        facts.Add(card.HasAnyDlssStreamline ? "DLSS 지원 게임" : "DLSS 미지원 게임");
        var factLine = Text(string.Join("  ·  ", facts), 13, false, "TextTertiaryBrush");
        ToolTipService.SetToolTip(factLine, "게임이 어떤 방식으로 그려지는지예요. BONG DLSS5가 이걸 보고 가장 잘 맞는 적용 방법을 알아서 골라요.");
        hero.Children.Add(factLine);

        // Status
        var status = new Border
        {
            CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left,
            Background = UIFactory.GetBrush(nrOn ? "#1E3DDC97" : "#14FFFFFF"),
            Child = new TextBlock
            {
                Text = !known ? "확인 중..." : nrOn ? "DLSS 5 적용됨" : "DLSS 5 아직 적용 안 됨",
                FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = nrOn ? UIFactory.GetBrush("#3DDC97") : Res("TextSecondaryBrush"),
            },
        };
        hero.Children.Add(status);

        // The one button
        var method = ViewModel.GetNrMethodOverride(card.GameName, card.Source ?? "") ?? DetailPanelBuilder.RecommendedNrMethod(card);
        var apply = new Button
        {
            Content = progress ?? (nrOn ? "DLSS 5 다시 적용하기" : "DLSS 5 한 번에 적용하기"),
            Height = 56, MinWidth = 300, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 14, 0, 0),
            Background = Res("AccentGradientBrush"), Foreground = Res("OnAccentTextBrush"), BorderBrush = Res("GlassCardStrokeBrush"),
            IsEnabled = progress == null && known && !_easyBusy,
        };
        apply.Resources["ButtonBackgroundPointerOver"] = Res("AccentGradientHoverBrush");
        apply.Resources["ButtonBackgroundPressed"] = Res("AccentGradientBrush");
        apply.Resources["ButtonForegroundPointerOver"] = Res("OnAccentTextBrush");
        apply.Resources["ButtonForegroundPressed"] = Res("OnAccentTextBrush");
        apply.Resources["ButtonBackgroundDisabled"] = Res("AccentGradientBrush");
        apply.Resources["ButtonForegroundDisabled"] = Res("OnAccentTextBrush");
        ToolTipService.SetToolTip(apply,
            "이 버튼 하나면 끝나요. 필요한 프로그램(ReShade)을 설치하고, DLSS 5 파일을 받아서, 이 게임에 맞는 방법으로 붙여 줘요. "
            + $"이 게임에는 '{DetailPanelBuilder.NrMethodLabel(method)}' 방법을 써요. 원래 파일은 백업되니 언제든 되돌릴 수 있어요.");
        apply.Click += async (s, e) => await EasyApplyAsync(card);
        hero.Children.Add(apply);

        if (result != null)
            hero.Children.Add(Text(result, 14, true, failed ? "AccentRedBrush" : "AccentGreenBrush"));
        _easyDetail.Children.Add(Card(hero, new Thickness(24, 22, 24, 22)));

        // Warnings
        if (card.UseNormalReShade)
            _easyDetail.Children.Add(Notice("이 게임은 ReShade가 '애드온 없음' 방식으로 설정돼 있어요",
                "DLSS 5는 ReShade 애드온으로 붙어서, 이 상태로는 적용할 수 없어요. 상세 화면의 '게임 재정의 > ReShade 채널'을 Stable로 바꾼 뒤 다시 눌러 주세요.", "#FF9F43"));
        if (card.GraphicsApi == GraphicsApiType.DirectX11 && method == "Uplift")
            _easyDetail.Children.Add(Notice("DirectX 11 게임은 컴퓨터가 멈출 수 있어요",
                "DirectX 11 게임에서는 DLSS 5를 우회 방식으로 돌려요. 그래픽카드 드라이버가 멈추면서 컴퓨터 전체가 멈춘 경우가 있었어요(Against the Storm). "
                + "적용한 뒤에는 꺼진 상태로 두었다가, 게임 안에서 잠깐만 켜 보세요. 멈추면 재부팅 후 아래 'DLSS 5 끄기'를 누르세요.", "#FF9F43"));
        if (card.IsDxvkBlacklisted)
            _easyDetail.Children.Add(Notice("온라인 게임일 수 있어요",
                "해킹 방지 프로그램(안티치트)이 있는 게임에 화면 모드를 넣으면 계정이 정지될 수 있어요. 혼자 하는 게임에만 쓰세요.", "#FF5D5D"));

        // What happens
        var how = new StackPanel { Spacing = 8 };
        how.Children.Add(Text("버튼을 누르면 이렇게 진행돼요", 15, true, "TextPrimaryBrush"));
        how.Children.Add(Step("1", "ReShade 설치", "게임 화면에 효과를 덧입히는 프로그램이에요. 이미 있으면 건너뛰어요.", progress?.Contains("ReShade") == true));
        how.Children.Add(Step("2", "DLSS 5 파일 받기", "엔비디아 DLSS 5 파일을 내려받아요. 처음 한 번만 받고, 다음부터는 저장해 둔 걸 써요.", progress != null && !progress.Contains("ReShade")));
        how.Children.Add(Step("3", "게임에 연결", $"이 게임에 가장 잘 맞는 방법({DetailPanelBuilder.NrMethodLabel(method)})으로 DLSS 5를 붙여요. 원래 파일은 백업해요.", false));
        how.Children.Add(Text("끝나면 게임을 실행하세요. 게임 안에서 Home 키를 누르면 ReShade 메뉴가 열리고, 여기서 DLSS 5 효과를 켜고 끌 수 있어요.", 13, false, "TextTertiaryBrush"));
        _easyDetail.Children.Add(Card(how));

        // Secondary actions
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var launch = SecondaryButton("게임 실행", "이 게임을 바로 실행해요.");
        launch.Click += async (s, e) => { try { await LaunchGameAsync(card); } catch (Exception ex) { CrashReporter.Log($"[EasyHome.Launch] {ex.Message}"); } };
        var detail = SecondaryButton("상세 설정 열기", "DLSS 5 방식 선택, 끄기(제거), 다른 모드 설치 같은 세부 설정 화면을 열어요.");
        detail.Click += (s, e) =>
        {
            SetEasyHome(false);
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => _detailPanelBuilder?.Layout.ShowSection("NeuralRendering"));
        };
        actions.Children.Add(launch);
        if (nrOn && method == "Uplift")
        {
            var off = SecondaryButton("DLSS 5 끄기", "다음에 게임을 켤 때 DLSS 5가 돌지 않게 꺼요. 파일은 그대로 두니 게임 안 GITC Uplift 탭에서 다시 켤 수 있어요. 완전히 지우려면 상세 설정에서 제거하세요.");
            off.Click += async (s, e) =>
            {
                if (card.IsRunning) { ShowEasyDetail(card, null, "게임을 끈 다음 눌러 주세요.", failed: true); return; }
                bool ok = await Task.Run(() => TurnOffUplift(card.InstallPath));
                ShowEasyDetail(card, null, ok ? "DLSS 5를 껐어요. 다음에 게임을 켜면 DLSS 5 없이 시작해요." : "끄지 못했어요. 상세 설정에서 제거해 주세요.", failed: !ok);
            };
            actions.Children.Add(off);
        }
        actions.Children.Add(detail);
        _easyDetail.Children.Add(actions);
    }

    private static Border Notice(string title, string body, string colour)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = UIFactory.GetBrush(colour), TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(Text(body, 13));
        return new Border
        {
            Background = UIFactory.GetBrush("#14FFFFFF"), BorderBrush = UIFactory.GetBrush(colour), BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 12, 16, 12), Child = sp,
        };
    }

    private static Grid Step(string number, string title, string body, bool active)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(new Border
        {
            Width = 26, Height = 26, CornerRadius = new CornerRadius(13), VerticalAlignment = VerticalAlignment.Top,
            Background = active ? Res("AccentGradientBrush") : Res("ControlFillBrush"),
            Child = new TextBlock { Text = number, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Res("TextPrimaryBrush") },
        });
        var t = new StackPanel { Spacing = 2 };
        t.Children.Add(Text(title, 14, true, "TextPrimaryBrush"));
        t.Children.Add(Text(body, 13));
        Grid.SetColumn(t, 1); g.Children.Add(t);
        return g;
    }

    private static Button SecondaryButton(string text, string tip)
    {
        var b = new Button { Content = text, Padding = new Thickness(16, 0, 16, 0) };
        TabsTheme.Apply(b, TabsTheme.IconButton, height: 40, fontSize: 14);
        ToolTipService.SetToolTip(b, tip);
        return b;
    }

    // ── One click ─────────────────────────────────────────────────────────────

    private async Task EasyApplyAsync(GameCardViewModel card)
    {
        if (_easyBusy || _detailPanelBuilder == null) return;
        if (card.UseNormalReShade)
        {
            ShowEasyDetail(card, null, "ReShade 채널을 먼저 바꿔 주세요(아래 안내 참고).", failed: true);
            return;
        }
        if (card.IsRunning)
        {
            ShowEasyDetail(card, null, "게임이 실행 중이에요. 게임을 끈 다음 다시 눌러 주세요.", failed: true);
            return;
        }
        if (card.IsDxvkBlacklisted)
        {
            var confirm = new ContentDialog
            {
                Title = "온라인 게임에 적용할까요?",
                Content = "이 게임에는 해킹 방지 프로그램(안티치트)이 있을 수 있어요. 화면 모드를 넣으면 계정이 정지될 수 있어요.",
                PrimaryButtonText = "그래도 적용", CloseButtonText = "취소", DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        }

        var chosen = ViewModel.GetNrMethodOverride(card.GameName, card.Source ?? "") ?? DetailPanelBuilder.RecommendedNrMethod(card);
        if (card.GraphicsApi == GraphicsApiType.DirectX11 && chosen == "Uplift")
        {
            var confirm11 = new ContentDialog
            {
                Title = "DirectX 11 게임이에요",
                Content = "이 게임은 DLSS 5를 우회 방식으로 돌려서, 그래픽 드라이버가 멈추고 컴퓨터가 멈출 수 있어요. "
                        + "DLSS 5는 꺼진 채로 설치되니, 게임 안에서 잠깐만 켜 보세요. 계속할까요?",
                PrimaryButtonText = "설치만 하기", CloseButtonText = "취소", DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };
            if (await confirm11.ShowAsync() != ContentDialogResult.Primary) return;
        }

        _easyBusy = true;
        var method = ViewModel.GetNrMethodOverride(card.GameName, card.Source ?? "") ?? DetailPanelBuilder.RecommendedNrMethod(card);
        var status = new Button(); // receives the install steps' progress text
        status.RegisterPropertyChangedCallback(ContentControl.ContentProperty, (s, e) =>
        {
            if (_easyBusy && status.Content is string p && p.Length > 0) ShowEasyDetail(card, EasyProgressWords(p));
        });
        ShowEasyDetail(card, "준비 중...");
        CrashReporter.Log($"[EasyHome.Apply] '{card.GameName}' method={method}");
        string? error = null;
        try
        {
            await _detailPanelBuilder.RunNrInstallCoreAsync(card, method, status, new ComboBox(), new ComboBox(), null, requireReShade: true);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            CrashReporter.Log($"[EasyHome.Apply] Failed for '{card.GameName}' — {ex.Message}");
        }

        bool on = false;
        try { on = await Task.Run(() => DetailPanelBuilder.IsNrInstalledIn(card)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { CrashReporter.Log($"[EasyHome.Apply] state check: {ex.Message}"); }

        DispatcherQueue.TryEnqueue(() =>
        {
            _easyBusy = false;
            _easyNrState[card] = on;
            RefreshEasyList();
            if (error == null && on)
                ShowEasyDetail(card, null, method == "Uplift"
                    ? "설치 완료! DLSS 5는 꺼진 채로 설치돼요. 게임 안에서 Home 키 > GITC Uplift 탭 > Enable을 켜면 시작돼요. 처음엔 잠깐만 켜 보고 이상하면 바로 끄세요."
                    : "완료! 이제 게임을 실행해 보세요.");
            else
                ShowEasyDetail(card, null, "적용하지 못했어요. " + EasyErrorWords(error), failed: true);
            // The full page shows the same game: rebuild it so its sections match
            if (ViewModel.SelectedGame == card) _detailPanelBuilder?.BuildOverridesPanel(card);
        });
    }

    /// <summary>
    /// Switches GITC Uplift's NR off in the game's reshade.ini ([Uplift] Enabled=0, StartEnabled=0), so the next start
    /// runs without it. Used by the home page's "DLSS 5 끄기" (on a DirectX 11 game NR froze the whole PC: Uplift ran it
    /// on the adapter's shared Direct3D 12 device and the driver hung, nvlddmkm event 153). False when it couldn't.
    /// </summary>
    private static bool TurnOffUplift(string installPath)
    {
        var ini = Path.Combine(installPath, "reshade.ini");
        try
        {
            if (!File.Exists(ini)) return false;
            Dlssg20_30Service.WriteIniValue(ini, "Uplift", "Enabled", "0");
            Dlssg20_30Service.WriteIniValue(ini, "Uplift", "StartEnabled", "0");
            CrashReporter.Log($"[EasyHome.TurnOffUplift] Enabled=0, StartEnabled=0 in '{ini}'");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashReporter.Log($"[EasyHome.TurnOffUplift] '{ini}' — {ex.Message}");
            return false;
        }
    }

    /// <summary>Install errors (often English, from the services) said simply, with what to do next.</summary>
    private static string EasyErrorWords(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return "상세 설정에서 다른 방법을 골라 보세요.";
        if (error.Contains("download", StringComparison.OrdinalIgnoreCase) || error.Contains("다운로드"))
            return "인터넷에서 필요한 파일을 받지 못했어요. 인터넷 연결을 확인하고 잠시 뒤 다시 눌러 주세요. "
                 + "자주 그러면 설정 > 계정 및 데이터에서 GitHub 토큰을 넣어 주세요(받을 수 있는 횟수가 크게 늘어나요).";
        if (error.Contains("access", StringComparison.OrdinalIgnoreCase) || error.Contains("denied", StringComparison.OrdinalIgnoreCase) || error.Contains("권한"))
            return "게임 폴더에 파일을 쓸 권한이 없어요. 설정에서 관리자 모드를 켜고 다시 해 보세요.";
        if (error.Contains("being used", StringComparison.OrdinalIgnoreCase) || error.Contains("사용 중"))
            return "게임이나 다른 프로그램이 파일을 쓰고 있어요. 게임을 완전히 끈 다음 다시 눌러 주세요.";
        return error;
    }

    /// <summary>The install steps' button texts, said simply.</summary>
    private static string EasyProgressWords(string raw) =>
        raw.Contains("ReShade") ? "1/3  ReShade 설치 중..." :
        raw.Contains("다운로드") || raw.Contains("Download", StringComparison.OrdinalIgnoreCase) ? "2/3  DLSS 5 파일 받는 중..." :
        raw.Contains("실패") ? raw :
        "3/3  게임에 연결하는 중...";
}

public sealed partial class MainWindow
{
    private bool _plainTipsPending;

    /// <summary>Adds the easy-words tooltips to the game page once the current burst of building has finished.</summary>
    internal void SchedulePlainTips()
    {
        if (_plainTipsPending) return;
        _plainTipsPending = true;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            _plainTipsPending = false;
            PlainTips.Attach(GameViewPanel);
        });
    }
}
