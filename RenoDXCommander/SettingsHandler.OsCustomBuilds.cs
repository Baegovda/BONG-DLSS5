// SettingsHandler.OsCustomBuilds.cs -- "Custom OptiScaler builds" card at the end of Settings › Components:
// add any OptiScaler build from a download link (GitHub, Discord, anywhere), an archive, a folder or an
// OptiScaler.dll, then pick it per game in the OptiScaler ⚙ or as the Default version. Each build is a
// fixed copy RHI never updates (OsCustomBuildService). Built in C# (XAML constraint), inserted the first
// time Settings opens. Plan: docs/custom-optiscaler-builds-plan.md.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private StackPanel? _osCustomList;
    private TextBlock? _osCustomStatus;
    private TextBox? _osCustomLinkBox;
    private bool _osCustomBusy;
    private bool _osCustomCardFailed;

    private void EnsureOsCustomBuildsCard()
    {
        if (_osCustomList != null) { RefreshOsCustomBuildsCard(); return; }
        if (_osCustomCardFailed) return;
        try
        {
            StackPanel? section = null;
            foreach (var child in _window.SettingsCardsPanel.Children)
                if (child is StackPanel sp && sp.Tag is string t && t == "Section:Components") { section = sp; break; }
            if (section == null)
            {
                _osCustomCardFailed = true;
                CrashReporter.Log("[SettingsHandler.EnsureOsCustomBuildsCard] Components section not found — card not added");
                return;
            }

            var card = new Border { Style = FindStyle("SettingsCardStyle") };
            var body = new StackPanel { Spacing = 10 };
            card.Child = body;

            var head = new StackPanel { Spacing = 3 };
            head.Children.Add(new TextBlock { Text = "사용자 지정 OptiScaler 빌드", Style = FindStyle("SettingsCardTitleStyle") });
            head.Children.Add(new TextBlock
            {
                Text = "어떤 OptiScaler 빌드든 추가할 수 있습니다: 다운로드 링크(GitHub, Discord 등), zip, 7z, rar, 폴더 "
                     + "또는 OptiScaler.dll. 추가한 후 게임의 OptiScaler 설정이나 위의 기본 버전에서 선택하세요. "
                     + "BONG DLSS5는 다른 버전처럼 설치하지만 업데이트는 하지 않으므로, 새 버전이 나오면 직접 추가하세요. "
                     + "신뢰할 수 있는 사람의 빌드만 추가하세요.",
                Style = FindStyle("SettingsCardDescStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            body.Children.Add(head);

            _osCustomList = new StackPanel { Spacing = 8 };
            body.Children.Add(_osCustomList);

            // Link row: [ paste a link ............ ] [Add link]
            var linkRow = new Grid { ColumnSpacing = 8 };
            linkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _osCustomLinkBox = new TextBox
            {
                PlaceholderText = "다운로드 링크 붙여넣기 (GitHub 릴리스 파일, Discord 첨부 파일 등)",
                Style = FindStyle("SettingsTextBoxStyle"),
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_osCustomLinkBox, "사용자 지정 OptiScaler 다운로드 링크");
            var addLink = MakeCustomFoldersButton("링크 추가", primary: true,
                "빌드를 한 번 다운로드하여 추가합니다. 파일의 직접 링크를 사용하세요. Discord는 첨부 파일을 마우스 오른쪽 버튼으로 클릭 › 링크 복사.");
            addLink.Click += async (s, e) =>
            {
                var link = _osCustomLinkBox.Text.Trim();
                if (link.Length == 0) { SetOsCustomStatus("먼저 다운로드 링크를 붙여넣으세요.", error: true); return; }
                if (!OsCustomBuildService.IsUrl(link, out _)) { SetOsCustomStatus("웹 링크가 아닙니다 (https://로 시작해야 합니다).", error: true); return; }
                if (await AddOsCustomBuildAsync(link, replace: null)) _osCustomLinkBox.Text = "";
            };
            _osCustomLinkBox.KeyDown += (s, e) =>
            {
                if (e.Key != Windows.System.VirtualKey.Enter) return;
                e.Handled = true;
                // Buttons have no click helper: press it through its automation peer
                (Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(addLink)
                    as Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer)?.Invoke();
            };
            linkRow.Children.Add(_osCustomLinkBox);
            Grid.SetColumn(addLink, 1);
            linkRow.Children.Add(addLink);
            body.Children.Add(linkRow);

            var buttons = new WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
            var addFile = MakeCustomFoldersButton("파일 추가", primary: false,
                "OptiScaler 빌드가 들어 있는 zip, 7z, rar 파일 또는 OptiScaler.dll 단독 파일.");
            addFile.Click += async (s, e) =>
            {
                var file = await PickOsCustomFileAsync();
                if (file != null) await AddOsCustomBuildAsync(file, replace: null);
            };
            buttons.Children.Add(addFile);
            var addFolder = MakeCustomFoldersButton("폴더 추가", primary: false,
                "압축을 푼 OptiScaler 빌드가 있는 폴더 (OptiScaler.dll이 그 안 또는 최대 3단계 하위 폴더에 있어야 함).");
            addFolder.Click += async (s, e) =>
            {
                var folder = await _window.PickFolderAsync();
                if (!string.IsNullOrEmpty(folder)) await AddOsCustomBuildAsync(folder, replace: null);
            };
            buttons.Children.Add(addFolder);
            body.Children.Add(buttons);

            _osCustomStatus = new TextBlock
            {
                Style = FindStyle("SettingsNoteStyle"),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };
            body.Children.Add(_osCustomStatus);

            section.Children.Add(card);
            OsCustomBuildService.Changed += () => _window.DispatcherQueue.TryEnqueue(RefreshOsCustomBuildsCard);
            RefreshOsCustomBuildsCard();
        }
        catch (Exception ex)
        {
            _osCustomCardFailed = true;
            CrashReporter.Log($"[SettingsHandler.EnsureOsCustomBuildsCard] Failed — {ex.Message}");
        }
    }

    /// <summary>Installed games whose OptiScaler version is this build.</summary>
    private List<GameCardViewModel> GamesUsingOsBuild(OsCustomBuild build) =>
        ViewModel.AllCards
            .Where(c => c.IsOsInstalled && ViewModel.GetOsVariant(c.GameName, c.Source ?? "") == build.Id)
            .ToList();

    private void RefreshOsCustomBuildsCard()
    {
        if (_osCustomList == null) return;
        try
        {
            _osCustomList.Children.Clear();
            var builds = OsCustomBuildService.All;
            if (builds.Count == 0)
            {
                _osCustomList.Children.Add(new TextBlock { Text = "아직 사용자 지정 빌드가 없습니다.", Style = FindStyle("SettingsNoteStyle") });
                return;
            }
            foreach (var build in builds)
                _osCustomList.Children.Add(MakeOsCustomRow(build));
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.RefreshOsCustomBuildsCard] {ex.Message}");
        }
    }

    private FrameworkElement MakeOsCustomRow(OsCustomBuild build)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = build.Name,
            Style = FindStyle("SettingsLabelStyle"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        int using_ = GamesUsingOsBuild(build).Count;
        var facts = new List<string>();
        if (!string.IsNullOrWhiteSpace(build.DllVersion)) facts.Add(build.DllVersion!);
        facts.Add(build.IsOs10 ? "OptiScaler 10 구조" : "단일 폴더 구조");
        facts.Add("추가됨 " + build.AddedUtc.ToLocalTime().ToString("d MMM yyyy", System.Globalization.CultureInfo.CurrentCulture));
        if (using_ > 0) facts.Add(using_ == 1 ? "게임 1개에서 사용 중" : $"게임 {using_}개에서 사용 중");
        var detail = new TextBlock
        {
            Text = string.Join(" · ", facts),
            Style = FindStyle("SettingsHelpStyle"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ToolTipService.SetToolTip(detail, $"출처: {build.Origin}\n폴더: {build.Dir}"
            + (build.LooksLikeOptiScaler ? "" : "\n이 OptiScaler.dll은 자신을 OptiScaler로 식별하지 않습니다."));
        text.Children.Add(detail);
        row.Children.Add(text);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var nr = new CheckBox
        {
            Content = "DLSS NR DLL",
            IsChecked = build.NeedsNrDll,
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(nr,
            "이 빌드와 함께 NVIDIA의 nvngx_dlssnr.dll도 설치하고, OptiScaler ⚙에 Neural Rendering 설정을 표시합니다. "
          + "Neural Rendering 포크(DLSS NR, F5 등)에서 켜세요. 빌드에 F5 방식 포워더가 있으면 자동으로 체크됩니다. "
          + "다음 설치부터 적용됩니다.");
        nr.Click += (s, e) =>
        {
            if (!OsCustomBuildService.SetNeedsNrDll(build.Slug, nr.IsChecked == true))
                SetOsCustomStatus("변경 사항을 저장하지 못했습니다.", error: true);
        };
        actions.Children.Add(nr);

        var replace = MakeCustomFoldersButton("교체", primary: false,
            "이 빌드의 파일을 새 버전으로 교체합니다(링크 상자, 파일 또는 폴더에서). 이 빌드를 사용하는 게임은 바로 업데이트할 수 있습니다.");
        var flyout = new MenuFlyout();
        var fromLink = new MenuFlyoutItem { Text = "위의 링크에서" };
        fromLink.Click += async (s, e) =>
        {
            var link = _osCustomLinkBox?.Text.Trim() ?? "";
            if (!OsCustomBuildService.IsUrl(link, out _)) { SetOsCustomStatus("먼저 새 다운로드 링크를 상자에 붙여넣으세요.", error: true); return; }
            if (await AddOsCustomBuildAsync(link, replace: build) && _osCustomLinkBox != null) _osCustomLinkBox.Text = "";
        };
        var fromFile = new MenuFlyoutItem { Text = "파일에서..." };
        fromFile.Click += async (s, e) =>
        {
            var file = await PickOsCustomFileAsync();
            if (file != null) await AddOsCustomBuildAsync(file, replace: build);
        };
        var fromFolder = new MenuFlyoutItem { Text = "폴더에서..." };
        fromFolder.Click += async (s, e) =>
        {
            var folder = await _window.PickFolderAsync();
            if (!string.IsNullOrEmpty(folder)) await AddOsCustomBuildAsync(folder, replace: build);
        };
        flyout.Items.Add(fromLink);
        flyout.Items.Add(fromFile);
        flyout.Items.Add(fromFolder);
        replace.Flyout = flyout;
        actions.Children.Add(replace);

        var rename = MakeCustomFoldersButton("이름 바꾸기", primary: false, "버전 목록에 표시되는 이름을 변경합니다.");
        rename.Click += async (s, e) =>
        {
            var name = await AskOsCustomNameAsync("사용자 지정 빌드 이름 바꾸기", build.Name);
            if (name == null || name == build.Name) return;
            if (!OsCustomBuildService.Rename(build.Slug, name)) SetOsCustomStatus("이름을 바꾸지 못했습니다.", error: true);
        };
        actions.Children.Add(rename);

        var remove = MakeCustomFoldersButton("제거", primary: false, "BONG DLSS5에서 이 빌드를 삭제합니다. 이미 설치된 게임의 파일은 유지됩니다.");
        remove.Click += async (s, e) => await RemoveOsCustomBuildAsync(build);
        actions.Children.Add(remove);

        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);
        return row;
    }

    private void SetOsCustomStatus(string text, bool error)
    {
        if (_osCustomStatus == null) return;
        _osCustomStatus.Text = text;
        _osCustomStatus.Foreground = UIFactory.Brush(error ? ResourceKeys.AccentRedBrush : ResourceKeys.TextSecondaryBrush);
        _osCustomStatus.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task<string?> PickOsCustomFileAsync()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        return await Task.Run(() =>
        {
            var ofn = new NativeInterop.OpenFileName();
            ofn.structSize = System.Runtime.InteropServices.Marshal.SizeOf(ofn);
            ofn.hwndOwner = hwnd;
            ofn.filter = "OptiScaler 빌드 (*.zip;*.7z;*.rar;*.dll)\0*.zip;*.7z;*.rar;*.dll\0모든 파일 (*.*)\0*.*\0";
            ofn.file = new string(new char[260]);
            ofn.maxFile = ofn.file.Length;
            ofn.title = "OptiScaler 빌드 선택";
            ofn.flags = 0x00080000 | 0x00001000; // OFN_EXPLORER | OFN_FILEMUSTEXIST
            return NativeInterop.GetOpenFileName(ref ofn) ? ofn.file.TrimEnd('\0') : null;
        });
    }

    private Task<string?> AskOsCustomNameAsync(string title, string suggested) =>
        OsCustomBuildDialogs.AskNameAsync(_window.Content.XamlRoot, title, suggested);

    /// <summary>Adds (or, with <paramref name="replace"/>, replaces) a build. True when it worked.</summary>
    private async Task<bool> AddOsCustomBuildAsync(string source, OsCustomBuild? replace)
    {
        if (_osCustomBusy) { SetOsCustomStatus("이전 작업을 아직 처리 중입니다...", error: false); return false; }
        string name;
        if (replace != null) name = replace.Name;
        else
        {
            var asked = await AskOsCustomNameAsync("이 OptiScaler 빌드 이름 지정", OsCustomBuildService.SuggestName(source));
            if (asked == null) return false;
            name = asked;
        }

        _osCustomBusy = true;
        try
        {
            var progress = new Progress<string>(m => SetOsCustomStatus(m, error: false));
            SetOsCustomStatus(replace != null ? $"{replace.Name} 교체 중..." : $"{name} 추가 중...", error: false);
            var result = await Task.Run(() => OsCustomBuildService.ImportAsync(source, name, replace?.Slug, progress, _window.LifetimeToken));
            if (result.Build == null)
            {
                SetOsCustomStatus(result.Error ?? "추가하지 못했습니다.", error: true);
                return false;
            }
            var done = replace != null ? $"{result.Build.Name}을(를) 교체했습니다." : $"{result.Build.Name}을(를) 추가했습니다. 게임의 OptiScaler 설정에서 선택하세요.";
            SetOsCustomStatus(result.Note != null ? done + " " + result.Note : done, error: result.Note != null);
            if (replace != null) await OfferOsCustomRedeployAsync(result.Build);
            return true;
        }
        finally { _osCustomBusy = false; }
    }

    /// <summary>After a build is replaced: offer to put the new files into the games that use it.</summary>
    private async Task OfferOsCustomRedeployAsync(OsCustomBuild build)
    {
        var games = GamesUsingOsBuild(build).Where(c => !c.IsHidden && !string.IsNullOrEmpty(c.InstallPath)).ToList();
        if (games.Count == 0) return;
        var dialog = new ContentDialog
        {
            Title = "이 빌드를 사용하는 게임을 업데이트할까요?",
            Content = new TextBlock
            {
                Text = (games.Count == 1 ? $"{games[0].GameName}에" : $"게임 {games.Count}개에")
                     + $" {build.Name}이(가) 설치되어 있습니다. 지금 새 파일을 넣을까요? OptiScaler.ini 설정은 유지됩니다.",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "업데이트",
            CloseButtonText = "나중에",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _window.Content.XamlRoot,
        };
        if (await DialogService.ShowSafeAsync(dialog) != ContentDialogResult.Primary) return;
        var os = App.Services.GetRequiredService<IOptiScalerService>();
        int ok = 0;
        foreach (var card in games)
        {
            SetOsCustomStatus($"{card.GameName} 업데이트 중...", error: false);
            try
            {
                await os.UpdateAsync(card, variantHint: build.Id);
                ok++;
            }
            catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.OsCustomRedeploy] {card.GameName}: {ex.Message}"); }
        }
        ViewModel.SaveLibraryPublic();
        SetOsCustomStatus(ok == games.Count ? $"게임 {ok}개를 업데이트했습니다." : $"게임 {games.Count}개 중 {ok}개를 업데이트했습니다. 나머지는 로그를 확인하세요.",
            error: ok != games.Count);
    }

    private async Task RemoveOsCustomBuildAsync(OsCustomBuild build)
    {
        var games = GamesUsingOsBuild(build);
        var dialog = new ContentDialog
        {
            Title = $"{build.Name}을(를) 제거할까요?",
            Content = new TextBlock
            {
                Text = games.Count == 0
                    ? "BONG DLSS5에서 해당 파일이 삭제됩니다."
                    : $"게임 {games.Count}개에 설치되어 있습니다. 해당 게임의 파일은 유지되지만 이 빌드로 업데이트하거나 재설치할 수 없습니다. 각 게임의 OptiScaler 설정에서 다른 버전을 선택하세요.",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "제거",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _window.Content.XamlRoot,
        };
        if (await DialogService.ShowSafeAsync(dialog) != ContentDialogResult.Primary) return;
        // The Default version can't point at a build that's gone
        if (ViewModel.Settings.OsDefaultVariant == build.Id) ViewModel.SetOsDefaultVariant(OsVariants.Stable);
        if (!OsCustomBuildService.Remove(build.Slug)) SetOsCustomStatus("파일을 삭제하지 못했습니다(사용 중일 수 있음). 다시 시도하세요.", error: true);
        else SetOsCustomStatus($"{build.Name}을(를) 제거했습니다.", error: false);
    }
}
