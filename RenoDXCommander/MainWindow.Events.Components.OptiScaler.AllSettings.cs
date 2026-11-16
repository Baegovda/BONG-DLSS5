// MainWindow.Events.Components.OptiScaler.AllSettings.cs — "All Settings" section of the OptiScaler cog.
// Lists every setting in the game's OptiScaler.ini with the options the installed build documents in
// its own ini (OsIniEditor), so new values from custom builds can be picked without an RHI update.
// Changes wait for Apply (nothing is written by accident); applied changes are saved per game and
// written again after a reinstall or update.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private const int OsAllSearchLimit = 60;

    // Kept across the cog reopening after Apply, so the section opens where the user left it
    private string? _osAllOpenFor;
    private string? _osAllSection;
    private string? _osAllAppliedMessage;

    private void BuildOsAllSettingsSection(OsCogContext ctx)
    {
        var card = ctx.Card;
        var cog = ctx.Cog;
        var store = ctx.Store;
        if (!card.IsOsInstalled || string.IsNullOrEmpty(card.InstallPath)) return;
        var gameDir = card.InstallPath;
        if (!File.Exists(Path.Combine(gameDir, OptiScalerService.IniFileName))) return;
        var openKey = $"{card.GameName}|{store}";

        cog.AddSection("모든 설정");
        cog.AddNote("이 게임의 OptiScaler.ini에 있는 모든 설정과, 설치된 OptiScaler 빌드가 제공하는 옵션입니다. " +
                    "적용을 누르기 전에는 아무것도 기록되지 않습니다. 적용한 변경 사항은 OptiScaler를 재설치하거나 업데이트해도 유지됩니다.");

        var settings = new List<OsIniSetting>();
        var pending = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool loaded = false;

        var host = new StackPanel { Spacing = 10, Visibility = Visibility.Collapsed };
        var sectionCombo = CogDialog.MakeCombo(Array.Empty<string>(), -1);
        var search = CogDialog.MakeTextBox("", "모든 설정 검색");
        var filterRow = new Grid { ColumnSpacing = 12 };
        filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ToolTipService.SetToolTip(sectionCombo, "표시할 OptiScaler.ini 섹션입니다.");
        Grid.SetColumn(search, 1);
        filterRow.Children.Add(sectionCombo);
        filterRow.Children.Add(search);
        var listHost = new StackPanel { Spacing = 8 };
        var info = new TextBlock
        {
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
        };

        Button? applyBtn = null, revertBtn = null, forgetBtn = null;
        void UpdateButtons()
        {
            if (applyBtn != null)
            {
                applyBtn.Content = pending.Count == 0 ? "적용" : $"변경 사항 {pending.Count}개 적용";
                applyBtn.IsEnabled = pending.Count > 0;
            }
            if (revertBtn != null) revertBtn.IsEnabled = pending.Count > 0;
            var saved = ViewModel.GetOsIniEdits(card.GameName, store).Count;
            if (forgetBtn != null) forgetBtn.IsEnabled = saved > 0;
            info.Text = saved > 0
                ? $"이 게임에 적용한 편집 {saved}개가 저장되어 있으며 재설치나 업데이트 후 다시 기록됩니다. 설정 위에 마우스를 올리면 설명이 표시됩니다."
                : "설정 위에 마우스를 올리면 설명이 표시됩니다. 아직 파일에 없는 설정은 auto로 표시되며 변경하면 추가됩니다.";
        }

        // One cell per setting: dropdown for documented options, text box for anything else
        OsCell MakeCell(OsIniSetting s, bool showSection)
        {
            var id = s.Id;
            string Current() => pending.TryGetValue(id, out var p) ? p : s.Value;
            void Changed(string value)
            {
                if (string.Equals(value, s.Value, StringComparison.Ordinal)) pending.Remove(id);
                else pending[id] = value;
                UpdateButtons();
            }

            var help = s.Help;
            if (!string.IsNullOrEmpty(s.Default)) help += (help.Length > 0 ? "\n" : "") + $"기본값 (auto): {s.Default}";
            help += (help.Length > 0 ? "\n" : "") + $"[{s.Section}] {s.Key}" + (s.InFile ? "" : " (아직 파일에 없음)");
            var label = showSection ? $"{s.Key} ({s.Section})" : s.Key;

            FrameworkElement control;
            if (s.Kind == OsIniKind.Text)
            {
                var tb = CogDialog.MakeTextBox(Current(), "auto");
                tb.TextChanged += (_, _) => Changed(tb.Text.Trim());
                control = tb;
            }
            else
            {
                var values = s.Options.Select(o => o.Value).ToList();
                var labels = s.Options.Select(o => o.Label).ToList();
                // A value the build doesn't list (typed by hand, or from another build) stays selectable
                foreach (var v in new[] { s.Value, Current() })
                    if (!values.Any(x => x.Equals(v, StringComparison.OrdinalIgnoreCase)))
                    {
                        values.Add(v);
                        labels.Add(v.Length == 0 ? "(비어 있음)" : v);
                    }
                int sel = values.FindIndex(x => x.Equals(Current(), StringComparison.OrdinalIgnoreCase));
                var combo = CogDialog.MakeCombo(labels, sel);
                combo.SelectionChanged += (_, _) =>
                {
                    if (combo.SelectedIndex >= 0) Changed(values[combo.SelectedIndex]);
                };
                control = combo;
            }
            return new OsCell(label, help, control);
        }

        bool Matches(OsIniSetting s, string q) =>
            s.Key.Contains(q, StringComparison.OrdinalIgnoreCase)
            || s.Section.Contains(q, StringComparison.OrdinalIgnoreCase)
            || s.Help.Contains(q, StringComparison.OrdinalIgnoreCase);

        void Render()
        {
            listHost.Children.Clear();
            var q = search.Text.Trim();
            List<OsIniSetting> shown;
            bool searching = q.Length > 0;
            if (searching)
            {
                var all = settings.Where(s => Matches(s, q)).ToList();
                shown = all.Take(OsAllSearchLimit).ToList();
                listHost.Children.Add(MakeOsFooterNote(all.Count == 0
                    ? "일치하는 설정이 없습니다."
                    : all.Count > shown.Count
                        ? $"일치 항목 {all.Count}개 중 처음 {shown.Count}개를 표시합니다."
                        : $"일치 항목 {all.Count}개."));
            }
            else
            {
                var section = sectionCombo.SelectedItem as string;
                shown = settings.Where(s => s.Section.Equals(section, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            sectionCombo.IsEnabled = !searching;
            if (shown.Count > 0)
            {
                var grid = MakeOsGrid(null, shown.Select(s => (OsCell?)MakeCell(s, searching)).ToArray());
                listHost.Children.Add(grid);
                TabsTheme.Restyle(grid);
            }
        }

        async Task LoadAsync()
        {
            try
            {
                settings = await Task.Run(() => OsIniEditor.Load(gameDir));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CrashReporter.Log($"[MainWindow.BuildOsAllSettingsSection] Couldn't read OptiScaler.ini — {ex.Message}");
                cog.SetError($"OptiScaler.ini를 읽을 수 없습니다: {ex.Message}");
                return;
            }
            var sections = settings.Select(s => s.Section).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            sectionCombo.ItemsSource = sections;
            var start = sections.FirstOrDefault(s => s.Equals(_osAllSection, StringComparison.OrdinalIgnoreCase))
                        ?? sections.FirstOrDefault();
            sectionCombo.SelectedItem = start;
            loaded = true; // after the selection, so its SelectionChanged doesn't render a second time
            Render();
            UpdateButtons();
        }

        sectionCombo.SelectionChanged += (_, _) =>
        {
            if (!loaded) return;
            _osAllSection = sectionCombo.SelectedItem as string;
            Render();
        };

        DispatcherQueueTimer? searchTimer = null;
        search.TextChanged += (_, _) =>
        {
            if (!loaded) return;
            searchTimer ??= DispatcherQueue.CreateTimer();
            searchTimer.Interval = TimeSpan.FromMilliseconds(200);
            searchTimer.IsRepeating = false;
            searchTimer.Tick -= OnSearchTick;
            searchTimer.Tick += OnSearchTick;
            searchTimer.Stop();
            searchTimer.Start();
        };
        void OnSearchTick(DispatcherQueueTimer t, object o) => Render();

        applyBtn = cog.MakeButton("적용", CogButtonKind.Primary, async () =>
        {
            if (pending.Count == 0) return;
            var values = pending.Select(kv => { var (s, k) = OsIniEditor.SplitId(kv.Key); return (s, k, kv.Value); }).ToList();
            var err = await Task.Run(() => OsIniEditor.WriteValues(gameDir, values));
            if (err != null) { cog.SetError($"OptiScaler.ini에 쓸 수 없습니다: {err}"); return; }

            var saved = ViewModel.GetOsIniEdits(card.GameName, store);
            foreach (var kv in pending) saved[kv.Key] = kv.Value;
            ViewModel.SetOsIniEdits(card.GameName, saved, store);

            // The Frame Generation dropdowns above keep their own saved values; keep them in step
            foreach (var (s, k, v) in values)
            {
                if (!s.Equals("FrameGen", StringComparison.OrdinalIgnoreCase)) continue;
                if (k.Equals("FGInput", StringComparison.OrdinalIgnoreCase)) ViewModel.SetOsFgInput(card.GameName, v, store);
                else if (k.Equals("FGOutput", StringComparison.OrdinalIgnoreCase)) ViewModel.SetOsFgOutput(card.GameName, v, store);
                else if (k.Equals("FGNvngxReplacement", StringComparison.OrdinalIgnoreCase)) ViewModel.SetOsFgNvngxReplacement(card.GameName, v, store);
            }

            CrashReporter.Log($"[MainWindow.BuildOsAllSettingsSection] Applied {values.Count} OptiScaler.ini change(s) for '{card.GameName}'");
            _osAllOpenFor = openKey;
            _osAllAppliedMessage = $"OptiScaler.ini에 변경 사항 {values.Count}개를 적용했습니다.";
            ReopenOsCog(ctx); // the settings above show the new values too
        }, "변경한 설정을 이 게임의 OptiScaler.ini에 기록합니다. 기존 파일은 먼저 백업됩니다.");

        revertBtn = cog.MakeButton("되돌리기", CogButtonKind.Secondary, () =>
        {
            pending.Clear();
            Render();
            UpdateButtons();
        }, "아직 적용하지 않은 변경 사항을 취소합니다.");

        forgetBtn = cog.MakeButton("저장된 편집 삭제", CogButtonKind.Secondary, () =>
        {
            ViewModel.SetOsIniEdits(card.GameName, null, store);
            UpdateButtons();
            cog.SetSuccess("저장된 편집을 삭제했습니다. OptiScaler.ini는 변경되지 않았으며, 재설치 시 기본 설정이 사용됩니다.");
        }, "재설치나 업데이트 후 적용한 편집을 다시 기록하지 않습니다. 현재 OptiScaler.ini는 변경되지 않습니다.");

        var backupsBtn = cog.MakeButton("백업 열기", CogButtonKind.Secondary, async () =>
        {
            var dir = OsIniEditor.BackupDir(gameDir);
            if (!Directory.Exists(dir)) { cog.SetStatus("아직 백업이 없습니다. 적용을 누를 때마다 백업이 만들어집니다."); return; }
            await Windows.System.Launcher.LaunchFolderPathAsync(dir);
        }, "각 적용 전의 OptiScaler.ini 사본입니다(최신 5개).");

        var actions = new Controls.WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
        foreach (var b in new[] { applyBtn, revertBtn, forgetBtn, backupsBtn }) actions.Children.Add(b);
        host.Children.Add(filterRow);
        host.Children.Add(listHost);
        host.Children.Add(info);
        host.Children.Add(actions);

        Button? showBtn = null;
        async Task ToggleAsync()
        {
            bool open = host.Visibility != Visibility.Visible;
            host.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            showBtn!.Content = open ? "모든 설정 숨기기" : "모든 설정 표시";
            _osAllOpenFor = open ? openKey : null;
            if (open && !loaded) await LoadAsync();
        }
        showBtn = cog.MakeButton("모든 설정 표시", CogButtonKind.Secondary, ToggleAsync);
        cog.AddButtons(showBtn);
        cog.AddContent(host);
        UpdateButtons();

        // Reopened after Apply: open again and scroll to it
        if (_osAllOpenFor == openKey)
        {
            var message = _osAllAppliedMessage;
            _osAllAppliedMessage = null;
            host.Loaded += async (_, _) =>
            {
                try
                {
                    if (host.Visibility == Visibility.Visible) return;
                    await ToggleAsync();
                    host.StartBringIntoView();
                    if (message != null) cog.SetSuccess(message);
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[MainWindow.BuildOsAllSettingsSection] Reopen failed — {ex.Message}");
                }
            };
        }
    }
}
