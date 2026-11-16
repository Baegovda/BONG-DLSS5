// MainWindow.Events.Components.OptiScaler.Release.cs -- two parts of the OptiScaler ⚙:
//   * Release: pin a game to a release of Stable / DLSS NR / F5 / a recommended fork (Nightly keeps its own
//     Nightly Build picker). Shares the per-game OsNightlyBuild value; staged by OsReleaseService.
//   * The version line at the top: installed vs latest, Check now (asks GitHub straight away) and Update.
// Asked for in issue #163 (no way to force a check or pick a version).

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private const string LatestReleaseItem = "Latest (auto-update)";

    // Release lists already fetched this session, per version (the ⚙ is reopened often)
    private static readonly Dictionary<string, List<OsRelease>> OsReleaseLists = new(StringComparer.Ordinal);

    /// <summary>Release cell: pin a release (only changeable while OptiScaler isn't installed on this game).</summary>
    private OsCell BuildOsReleaseCell(OsCogContext ctx, string variant, bool isInstalled)
    {
        var card = ctx.Card;
        var store = ctx.Store;
        var pinned = ViewModel.GetOsPinnedRelease(card.GameName, store);

        var items = new List<string> { LatestReleaseItem };
        if (pinned != null) items.Add(pinned);
        var combo = CogDialog.MakeCombo(items, pinned != null ? 1 : 0, isEnabled: !isInstalled);
        combo.Opacity = isInstalled ? CogDialog.DisabledOpacity : 1.0;
        var cell = new OsCell("릴리스", isInstalled
            ? "릴리스를 변경하려면 먼저 OptiScaler를 제거하세요."
            : "이 게임을 특정 릴리스에 고정합니다. 고정된 게임은 업데이트에서 제외됩니다. Latest는 항상 최신 릴리스를 사용합니다.",
            combo, Ungated: true);

        bool filling = false;
        void Fill(List<OsRelease> releases)
        {
            filling = true;
            try
            {
                var tags = releases.Select(r => r.Tag).ToList();
                if (pinned != null && !tags.Contains(pinned)) tags.Insert(0, pinned);
                var list = new List<string> { LatestReleaseItem };
                list.AddRange(tags);
                combo.ItemsSource = list;
                combo.SelectedIndex = pinned != null ? 1 + tags.IndexOf(pinned) : 0;
            }
            finally { filling = false; }
        }

        if (OsReleaseLists.TryGetValue(variant, out var known)) Fill(known);
        else if (!isInstalled)
        {
            _ = Task.Run(async () =>
            {
                var releases = await OsReleaseService.ListAsync(variant).ConfigureAwait(false);
                if (releases == null) return;
                lock (OsReleaseLists) OsReleaseLists[variant] = releases;
                DispatcherQueue.TryEnqueue(() => Fill(releases));
            });
        }

        combo.SelectionChanged += async (s, ev) =>
        {
            if (filling || combo.SelectedItem is not string sel) return;
            try
            {
                if (sel == LatestReleaseItem)
                {
                    ViewModel.SetOsNightlyBuild(card.GameName, null, store);
                    CrashReporter.Log($"[OsCog] '{card.GameName}' follows the latest {variant} release");
                    return;
                }
                ViewModel.SetOsNightlyBuild(card.GameName, sel, store);
                CrashReporter.Log($"[OsCog] Pinned '{card.GameName}' to {variant} release {sel}");
                // Download it now so the install doesn't wait
                if (OsReleaseService.PinnedDir(variant, sel) == null)
                {
                    ctx.Cog.SetSuccess($"{sel} 다운로드 중...");
                    var dir = await Task.Run(() => OsReleaseService.EnsurePinnedAsync(variant, sel, null, LifetimeToken));
                    if (dir != null) ctx.Cog.SetSuccess($"{sel}을(를) 설치할 준비가 되었습니다.");
                    else ctx.Cog.SetError($"{sel}을(를) 다운로드할 수 없습니다. 설치할 때 다시 시도합니다.");
                }
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[OsCog] Release pin failed — {ex.Message}");
                ctx.Cog.SetError($"릴리스를 고정할 수 없습니다: {ex.Message}");
            }
        };
        return cell;
    }

    /// <summary>
    /// "Installed 0.1.29 · Latest 0.1.37" with Check now and Update. Checks once (cached) when the ⚙ opens;
    /// Check now asks GitHub again.
    /// </summary>
    private FrameworkElement BuildOsVersionLine(OsCogContext ctx, string variant, bool isInstalled)
    {
        var card = ctx.Card;
        var store = ctx.Store;
        var text = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = TabsTheme.B(TabsTheme.TextSecondary),
        };
        var check = CogDialog.StyledButton("지금 확인", CogButtonKind.Secondary, compact: true);
        ToolTipService.SetToolTip(check, "다음 자동 확인을 기다리지 않고 지금 GitHub에서 최신 릴리스를 확인합니다.");
        var update = CogDialog.StyledButton("업데이트", CogButtonKind.Primary, compact: true);
        update.Tag = card; // the install handler reads the card from the sender's Tag
        update.Visibility = Visibility.Collapsed;
        ToolTipService.SetToolTip(update, "이 게임에 최신 릴리스를 설치합니다. OptiScaler.ini 설정은 유지됩니다.");

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(text);
        Grid.SetColumn(check, 1);
        row.Children.Add(check);
        Grid.SetColumn(update, 2);
        row.Children.Add(update);
        ctx.Ungated.Add(row);

        string installed = isInstalled ? (card.OsInstalledVersion ?? "설치됨") : "설치 안 됨";
        var pinned = ViewModel.GetOsPinnedRelease(card.GameName, store);
        var nightlyPin = variant == OsVariants.Nightly ? ViewModel.GetOsNightlyBuild(card.GameName, store) : "";

        // Custom builds are never updated: no check
        if (OsVariants.IsCustom(variant))
        {
            text.Text = $"설치됨: {installed} · 사용자 지정 빌드, BONG DLSS5가 업데이트하지 않음 (설정 › 구성 요소에서 교체 사용).";
            check.Visibility = Visibility.Collapsed;
            return row;
        }

        void Show(string? latest, bool checkedNow)
        {
            if (latest == null)
            {
                text.Text = $"설치됨: {installed} · 최신: 확인할 수 없음{(checkedNow ? " (오프라인 또는 GitHub 제한, 설정 › 계정의 GitHub 토큰이 도움이 됩니다)" : "")}";
                update.Visibility = Visibility.Collapsed;
                return;
            }
            bool same = OsReleaseService.SameVersion(card.OsInstalledVersion, latest);
            string state;
            bool canUpdate = false;
            if (pinned != null || !string.IsNullOrEmpty(nightlyPin))
                state = $"{(pinned ?? nightlyPin)}에 고정되어 업데이트되지 않음";
            else if (!isInstalled) state = "이 릴리스를 설치함";
            else if (same) state = "최신 상태";
            else { state = "업데이트 있음"; canUpdate = true; }
            text.Text = $"설치됨: {installed} · 최신: {latest} ({state})";
            update.Visibility = canUpdate ? Visibility.Visible : Visibility.Collapsed;
            if (canUpdate && checkedNow)
            {
                card.OsStatus = GameStatus.UpdateAvailable;
                ViewModel.NotifyUpdateButtonChanged();
            }
        }

        async Task<string?> FindLatestAsync(bool force)
        {
            if (variant == OsVariants.Nightly)
            {
                if (force || _optiScalerService.AvailableNightlyBuilds.Count == 0)
                    await _optiScalerService.FetchAvailableNightlyBuildsAsync();
                return _optiScalerService.AvailableNightlyBuilds.FirstOrDefault();
            }
            if (OsReleaseService.SourceFor(variant) is not { } src) return null;
            if (force)
                App.Services.GetRequiredService<GitHubETagCache>().Invalidate($"https://api.github.com/repos/{src.Repo}/releases?per_page=30");
            var releases = await Task.Run(() => OsReleaseService.ListAsync(variant));
            if (releases == null) return null;
            lock (OsReleaseLists) OsReleaseLists[variant] = releases;
            // Also set the service's own flag, so Update / Update All pick up the new release
            if (force && isInstalled)
            {
                switch (variant)
                {
                    case OsVariants.Stable: await _optiScalerService.CheckForUpdateAsync(); break;
                    case OsVariants.DlssNr: await _optiScalerService.CheckForDlssNrUpdateAsync(); break;
                    case OsVariants.F5: await _optiScalerService.CheckForF5UpdateAsync(); break;
                    default: if (OsVariants.IsFork(variant)) await OsForkService.CheckForUpdateAsync(variant); break;
                }
            }
            return releases.FirstOrDefault()?.Tag;
        }

        text.Text = $"설치됨: {installed} · 최신: 확인 중...";
        _ = LoadAsync(false);
        async Task LoadAsync(bool force)
        {
            try
            {
                check.IsEnabled = false;
                if (force) text.Text = $"설치됨: {installed} · 최신: 확인 중...";
                var latest = await FindLatestAsync(force);
                Show(latest, force);
                CrashReporter.Log($"[OsCog] {card.GameName} {variant}: installed {card.OsInstalledVersion ?? "-"}, latest {latest ?? "?"}{(force ? " (Check now)" : "")}");
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[OsCog] Version check failed — {ex.Message}");
                Show(null, force);
            }
            finally { check.IsEnabled = true; }
        }

        check.Click += async (s, e) => await LoadAsync(true);
        update.Click += (s, e) =>
        {
            ctx.Cog.Hide(); // the install shows its progress on the game
            InstallOsButton_Click(update, e);
        };
        return row;
    }
}
