// MainWindow.Events.Components.OptiScaler.Presets.cs — Presets panel at the top of the OptiScaler cog:
// ready-made presets (optiscaler_presets.json) as buttons, and the 4 user slots in one dropdown.

using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using RenoDXCommander.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    /// <summary>The user slot last picked in the dropdown, kept while the app runs (the cog reopens after Apply).</summary>
    private static int _osSelectedSlot = -1;

    private const string OsPresetHint = "프리셋으로 시작한 후 아래 설정을 세부 조정하세요. 프리셋에 마우스를 올리면 설명이 표시됩니다.";

    private FrameworkElement BuildOsPresetsPanel(OsCogContext ctx)
    {
        var card = ctx.Card;
        var panel = new StackPanel { Spacing = 10 };
        // Translucent white rather than the theme's navy card colours: the cog is neutral grey,
        // so this reads as a slightly lighter grey panel on it.
        var border = new Border
        {
            Background = UIFactory.GetBrush(OsPresetPanelBg),
            BorderBrush = UIFactory.GetBrush(OsPresetPanelBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 2, 0, 6),
            Child = panel,
        };

        var status = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        void ShowStatus(string text, bool ok)
        {
            status.Text = text;
            status.Foreground = UIFactory.Brush(ok ? ResourceKeys.AccentGreenBrush : ResourceKeys.AccentRedBrush);
            status.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        panel.Children.Add(new TextBlock
        {
            Text = "프리셋",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
        });

        // ── Ready-made presets ──────────────────────────────────────────────
        // ANR FG is behind the manifest flag "anrFg" (kept where it's already installed)
        bool showAnr = FeatureFlags.AnrFg || AnrFgService.IsInstalledIn(card.InstallPath);
        var manifestPresets = OsPresetService.ManifestPresets
            .Where(p => p.InstallAnrFg != true || showAnr)
            .ToList();
        var active = manifestPresets.FirstOrDefault(p => IsOsPresetActive(card, p));

        var description = MakeOsPresetCaption(active != null
            ? $"사용 중: {active.Name}. {active.Description}".TrimEnd()
            : OsPresetHint);
        string restingDescription = description.Text;

        var pills = new WrapPanel { HorizontalSpacing = 6, VerticalSpacing = 6 };
        foreach (var preset in manifestPresets)
        {
            var p = preset;
            bool isActive = ReferenceEquals(p, active);
            var pill = MakeOsPresetPill(p.Name ?? "프리셋", isActive);
            ToolTipService.SetToolTip(pill, string.IsNullOrEmpty(p.Description) ? $"이 게임에 {p.Name} 적용" : p.Description);
            pill.PointerEntered += (_, _) => { if (!string.IsNullOrEmpty(p.Description)) description.Text = $"{p.Name}: {p.Description}"; };
            pill.PointerExited  += (_, _) => description.Text = restingDescription;
            pill.Click += async (_, _) =>
            {
                try
                {
                    // ANR FG is closed source: same notice as RTX Encore before its first install. In the Spotlight panel
                    // and Tabs the cog isn't a dialog, so the notice opens over it and it stays where it is. As a dialog
                    // (Classic+) it has to close first (only one dialog can be open; DialogService waits for that) and
                    // opens again afterwards.
                    var st = ViewModel.Settings;
                    var xamlRoot = pill.XamlRoot ?? Content.XamlRoot;
                    bool cogHidden = false;
                    if (p.InstallAnrFg == true && !AnrFgService.IsInstalledIn(card.InstallPath) && !st.AnrFgClosedSourceDismissed)
                    {
                        if (!ctx.Cog.IsEmbedded)
                        {
                            ctx.Cog.Hide();
                            cogHidden = true;
                        }
                        bool go = await DetailPanelBuilder.ShowClosedSourceNoticeAsync("ANR FG", "Apex Compute",
                            () => st.AnrFgClosedSourceDismissed, () => st.AnrFgClosedSourceDismissed = true, xamlRoot,
                            "https://github.com/apex-compute/ANR");
                        if (!go)
                        {
                            if (cogHidden) ReopenOsCog(ctx);
                            return;
                        }
                    }
                    foreach (var b in pills.Children.OfType<Button>()) b.IsEnabled = false;
                    ShowStatus($"{p.Name} 적용 중...", true);
                    ApplyOsPreset(card, p);
                    if (p.InstallUniversalMfg == true)
                    {
                        ShowStatus($"{p.Name} 적용 중: Universal MFG Unlocker 설치 중...", true);
                        var error = await ViewModel.EnsureUniversalMfgForPresetAsync(card);
                        RefreshExtrasIfSelected(card);
                        if (error != null)
                        {
                            ShowStatus($"{p.Name} 설정을 적용했지만 문제가 있습니다: {error}", false);
                            foreach (var b in pills.Children.OfType<Button>()) b.IsEnabled = true;
                            return;
                        }
                    }
                    if (p.InstallAnrFg == true)
                    {
                        ShowStatus($"{p.Name} 적용 중: ANR FG 다운로드 및 설치 중...", true);
                        string? error;
                        if (cogHidden)
                        {
                            // The cog closed for the notice: show the download in a progress dialog, errors in a dialog,
                            // then open the cog again
                            var progress = new ContentDialog
                            {
                                Title = "ANR FG 설치 중",
                                Content = new StackPanel
                                {
                                    Spacing = 12,
                                    Children =
                                    {
                                        new TextBlock { Text = $"{p.Name} 적용 중: ANR FG 다운로드 및 설치 중...", TextWrapping = TextWrapping.Wrap },
                                        new ProgressBar { IsIndeterminate = true, Height = 6 },
                                    },
                                },
                                XamlRoot = xamlRoot,
                                RequestedTheme = ElementTheme.Dark,
                            };
                            await using (await DialogService.ShowProgressAsync(progress))
                                error = await ViewModel.EnsureAnrFgForPresetAsync(card);
                            if (error != null)
                                await DialogService.ShowSafeAsync(new ContentDialog
                                {
                                    Title = "ANR FG가 설치되지 않음",
                                    Content = new TextBlock { Text = $"{p.Name} 설정을 적용했지만 문제가 있습니다: {error}", TextWrapping = TextWrapping.Wrap },
                                    CloseButtonText = "확인",
                                    XamlRoot = xamlRoot,
                                    RequestedTheme = ElementTheme.Dark,
                                });
                            ReopenOsCog(ctx);
                            if (error == null) CrashReporter.Log($"[OsPreset.ApplyManifest] '{p.Name}' applied to '{card.GameName}'");
                            return;
                        }
                        error = await ViewModel.EnsureAnrFgForPresetAsync(card);
                        if (error != null)
                        {
                            ShowStatus($"{p.Name} 설정을 적용했지만 문제가 있습니다: {error}", false);
                            foreach (var b in pills.Children.OfType<Button>()) b.IsEnabled = true;
                            return;
                        }
                    }
                    CrashReporter.Log($"[OsPreset.ApplyManifest] '{p.Name}' applied to '{card.GameName}'");
                    ReopenOsCog(ctx);
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[OsPreset.ApplyManifest] '{p.Name}' — {ex.Message}");
                    ShowStatus($"{p.Name}을(를) 적용하지 못했습니다: {ex.Message}", false);
                    foreach (var b in pills.Children.OfType<Button>()) b.IsEnabled = true;
                }
            };
            pills.Children.Add(pill);
        }
        panel.Children.Add(MakeOsPresetLabel("권장"));
        panel.Children.Add(pills);
        panel.Children.Add(description);

        panel.Children.Add(new Border { Height = 1, Background = UIFactory.GetBrush(OsPresetPanelBorder) });

        // ── User slots ──────────────────────────────────────────────────────
        panel.Children.Add(MakeOsPresetLabel("내 프리셋 (모든 게임에서 공유)"));
        panel.Children.Add(BuildOsUserSlotsRow(ctx, ShowStatus));
        panel.Children.Add(status);
        return border;
    }

    private Grid BuildOsUserSlotsRow(OsCogContext ctx, Action<string, bool> showStatus)
    {
        var slots = OsPresetService.Load();
        static string SlotLabel(OsPreset? p, int i) =>
            p == null ? $"슬롯 {i + 1} (비어 있음)" : string.IsNullOrWhiteSpace(p.Name) ? $"슬롯 {i + 1}" : p.Name!;

        if (_osSelectedSlot < 0 || _osSelectedSlot > 3)
            _osSelectedSlot = Math.Max(0, Array.FindIndex(slots, s => s != null));

        var combo = CogDialog.MakeCombo(Enumerable.Range(0, 4).Select(i => SlotLabel(slots[i], i)).ToList(), _osSelectedSlot);
        combo.HorizontalAlignment = HorizontalAlignment.Stretch;
        ToolTipService.SetToolTip(combo, "내 프리셋입니다. 슬롯을 선택한 후 이 게임에 적용하거나 현재 설정을 저장하세요.");

        var applyBtn = CogDialog.StyledButton("적용", CogButtonKind.Primary, compact: true);
        var saveBtn  = CogDialog.StyledButton("현재 설정 저장", CogButtonKind.Secondary, compact: true);
        var moreBtn  = CogDialog.StyledButton("...", CogButtonKind.Secondary, compact: true);
        ToolTipService.SetToolTip(moreBtn, "이 슬롯 이름 바꾸기 또는 비우기");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(moreBtn, "이 슬롯 이름 바꾸기 또는 비우기");

        void Refresh()
        {
            int i = Math.Clamp(combo.SelectedIndex, 0, 3);
            bool has = slots[i] != null;
            applyBtn.IsEnabled = has;
            applyBtn.Opacity = has ? 1.0 : CogDialog.DisabledOpacity;
            saveBtn.Content = has ? "덮어쓰기" : "현재 설정 저장";
            ToolTipService.SetToolTip(applyBtn, has ? "이 게임에 이 프리셋 적용" : "빈 슬롯입니다. 먼저 현재 설정을 저장하세요.");
            ToolTipService.SetToolTip(saveBtn, has
                ? "이 슬롯을 현재 설정으로 교체 (이름 유지)"
                : "현재 설정을 이 슬롯에 저장");
        }
        void RelabelSelected()
        {
            int i = combo.SelectedIndex;
            var items = Enumerable.Range(0, 4).Select(n => SlotLabel(slots[n], n)).ToList();
            combo.ItemsSource = items;
            combo.SelectedIndex = i;
        }

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0) _osSelectedSlot = combo.SelectedIndex;
            Refresh();
        };

        applyBtn.Click += (_, _) =>
        {
            int i = Math.Clamp(combo.SelectedIndex, 0, 3);
            var p = slots[i];
            if (p == null) return;
            try { ApplyOsPreset(ctx.Card, p); }
            catch (Exception ex)
            {
                CrashReporter.Log($"[OsPreset.Apply] Slot {i + 1} — {ex.Message}");
                showStatus($"프리셋을 적용하지 못했습니다: {ex.Message}", false);
                return;
            }
            ReopenOsCog(ctx);
        };

        saveBtn.Click += (_, _) =>
        {
            int i = Math.Clamp(combo.SelectedIndex, 0, 3);
            try
            {
                slots[i] ??= new OsPreset();
                CaptureOsPreset(ctx, slots[i]!); // keeps the name
                OsPresetService.Save(slots);
                RelabelSelected();
                Refresh();
                showStatus($"현재 설정을 {SlotLabel(slots[i], i)}에 저장했습니다.", true);
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"[OsPreset.Save] Slot {i + 1} — {ex.Message}");
                showStatus($"프리셋을 저장하지 못했습니다: {ex.Message}", false);
            }
        };

        // "..." flyout: rename + clear
        var nameBox = CogDialog.MakeTextBox("", "프리셋 이름");
        nameBox.MinWidth = 200;
        var renameBtn = CogDialog.StyledButton("이름 바꾸기", CogButtonKind.Primary, compact: true);
        var clearBtn  = CogDialog.StyledButton("슬롯 비우기", CogButtonKind.Danger, compact: true);
        var flyoutPanel = new StackPanel { Spacing = 8 };
        flyoutPanel.Children.Add(CogDialog.Inline(nameBox, renameBtn));
        flyoutPanel.Children.Add(clearBtn);
        var flyout = new Flyout { Content = flyoutPanel, Placement = FlyoutPlacementMode.BottomEdgeAlignedRight };
        flyout.Opening += (_, _) =>
        {
            int i = Math.Clamp(combo.SelectedIndex, 0, 3);
            nameBox.Text = slots[i]?.Name ?? "";
            clearBtn.IsEnabled = slots[i] != null;
        };
        moreBtn.Flyout = flyout;

        void Rename()
        {
            int i = Math.Clamp(combo.SelectedIndex, 0, 3);
            slots[i] ??= new OsPreset();
            slots[i]!.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? null : nameBox.Text.Trim();
            OsPresetService.Save(slots);
            RelabelSelected();
            Refresh();
            flyout.Hide();
        }
        renameBtn.Click += (_, _) => Rename();
        nameBox.KeyDown += (_, e) =>
        {
            if (e.Key != Windows.System.VirtualKey.Enter) return;
            e.Handled = true; // Enter must not close the cog
            Rename();
        };
        clearBtn.Click += (_, _) =>
        {
            int i = Math.Clamp(combo.SelectedIndex, 0, 3);
            slots[i] = null;
            OsPresetService.Save(slots);
            RelabelSelected();
            Refresh();
            flyout.Hide();
            showStatus($"슬롯 {i + 1}을(를) 비웠습니다.", true);
        };

        Refresh();
        var row = CogDialog.Inline(combo, applyBtn, saveBtn, moreBtn);
        row.ColumnSpacing = 6;
        return row;
    }

    /// <summary>True when the game's current settings match every frame generation setting the preset sets.</summary>
    private bool IsOsPresetActive(GameCardViewModel card, OsPreset p)
    {
        if (!card.IsOsInstalled || string.IsNullOrEmpty(card.InstallPath)) return false;
        var store = card.Source ?? "";
        static bool Same(string? want, string? have, string fallback) =>
            want == null || string.Equals(want, string.IsNullOrEmpty(have) || have.Equals("auto", StringComparison.OrdinalIgnoreCase) ? fallback : have,
                StringComparison.OrdinalIgnoreCase);

        if (!Same(p.FgEnabled, ReadOsIniSectionValue(card.InstallPath, "FrameGen", "Enabled", ""), "false")) return false;
        if (!Same(p.FgInput, ViewModel.GetOsFgInput(card.GameName, store), "auto")) return false;
        if (!Same(p.FgOutput, ViewModel.GetOsFgOutput(card.GameName, store), "auto")) return false;
        if (OptiScalerService.UsesNvngxReplacement(p.FgInput, p.FgOutput)
            && !Same(p.FgNvngxReplacement, ViewModel.GetOsFgNvngxReplacement(card.GameName, store), "None")) return false;
        if (!Same(p.HudFix, ReadOsIniSectionValue(card.InstallPath, "OptiFG", "HUDFix", ""), "auto")) return false;
        if (!Same(p.ForceReflex, ReadOsIniSectionValue(card.InstallPath, "fakenvapi", "ForceReflex", ""), "0")) return false;
        if (!Same(p.UseGamesReflexMarkers, ReadOsIniSectionValue(card.InstallPath, "DLSSG", "UseGamesReflexMarkers", ""), "true")) return false;
        if (!Same(p.DxgiSpoofing, ReadOsIniSectionValue(card.InstallPath, "Spoofing", "Dxgi", ""), "auto")) return false;
        if (p.DeployStreamline.HasValue && p.DeployStreamline.Value != ViewModel.GetOsDeployStreamline(card.GameName, store)) return false;
        if (p.InstallUniversalMfg == true && !card.Rtx40MfgInstalled) return false;
        if (p.InstallAnrFg == true && !AnrFgService.IsInstalledIn(card.InstallPath)) return false;
        return true;
    }

    /// <summary>Rebuilds the Extras section if this game is on screen (Universal MFG Unlocker row changed).</summary>
    private void RefreshExtrasIfSelected(GameCardViewModel card) => _detailPanelBuilder.RequestExtrasRebuild(card);

    private static Button MakeOsPresetPill(string name, bool isActive)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (isActive)
            content.Children.Add(new FontIcon { Glyph = "", FontSize = 11 });
        content.Children.Add(new TextBlock { Text = name, FontSize = 12 });
        return new Button
        {
            Content = content,
            Height = 30,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(15),
            BorderThickness = new Thickness(1),
            Background  = isActive ? UIFactory.Brush(ResourceKeys.AccentBlueBgBrush) : UIFactory.GetBrush(OsPresetPillBg),
            Foreground  = UIFactory.Brush(isActive ? ResourceKeys.AccentBlueBrush : ResourceKeys.TextPrimaryBrush),
            BorderBrush = isActive ? UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush) : UIFactory.GetBrush(OsPresetPillBorder),
        };
    }

    // Grey layers for the presets panel (white at low opacity over the cog's grey)
    private const string OsPresetPanelBg     = "#0AFFFFFF";
    private const string OsPresetPanelBorder = "#1AFFFFFF";
    private const string OsPresetPillBg      = "#12FFFFFF";
    private const string OsPresetPillBorder  = "#26FFFFFF";

    private static TextBlock MakeOsPresetLabel(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
    };

    private static TextBlock MakeOsPresetCaption(string text) => new()
    {
        Text = text,
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
    };
}
