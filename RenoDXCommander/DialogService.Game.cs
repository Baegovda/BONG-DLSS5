using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

// Game-specific dialogs: foreign DLL confirmation, game notes, and UE-Extended warning.
public partial class DialogService
{
    // ── Foreign DLL Confirmation Dialogs ────────────────────────────────────────

    public async Task<bool> ShowForeignDxgiConfirmDialogAsync(GameCardViewModel card, string dxgiPath)
    {
        var fileSize = new System.IO.FileInfo(dxgiPath).Length;
        var sizeKB   = fileSize / 1024.0;

        var dlg = new ContentDialog
        {
            Title               = "⚠ 알 수 없는 dxgi.dll 감지됨",
            Content             = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground   = Brush(ResourceKeys.AccentAmberBrush),
                FontSize     = 13,
                Text         = $"다음 위치에서 dxgi.dll 파일이 발견되었습니다:\n{card.InstallPath}\n\n" +
                               $"파일 크기: {sizeKB:N0} KB\n\n" +
                               "BONG DLSS5가 이 파일을 ReShade나 Display Commander로 식별할 수 없습니다. " +
                               "다른 모드(예: DXVK, Special K, ENB)의 파일일 수 있습니다.\n\n" +
                               "덮어쓰면 기존 모드가 작동하지 않을 수 있습니다. 계속하시겠습니까?",
            },
            PrimaryButtonText   = "덮어쓰기",
            CloseButtonText     = "취소",
            XamlRoot            = _window.Content.XamlRoot,
            Background          = Brush(ResourceKeys.SurfaceOverlayBrush),
            RequestedTheme      = ElementTheme.Dark,
        };

        var result = await DialogService.ShowSafeAsync(dlg);
        return result == ContentDialogResult.Primary;
    }

    // ── Source engine mod install dialog ────────────────────────────────────────

    /// <summary>
    /// Shown before the RenoDX Source engine mod's first install: what RHI sets up, the VAC warning for
    /// -insecure (only used when the game starts from RHI), and the in-game HDR setting. True = install.
    /// </summary>
    public async Task<bool> ShowSourceEngineInstallDialogAsync(GameCardViewModel card)
    {
        var settings = _window.ViewModel.Settings;
        if (settings.SourceEngineNoticeDismissed) return true;

        var panel = new StackPanel { Spacing = 10, MaxWidth = 520 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 13,
            Text         = $"BONG DLSS5가 {card.GameName}에 RenoDX의 Source 엔진 모드를 설정합니다:\n" +
                           "• ReShade를 d3d9.dll로 설치하고 ReShade 프록시를 통해 DXVK(안정) 로드\n" +
                           "• 게임 기본 폴더에 dxvk.conf\n" +
                           "• BONG DLSS5의 게임 실행 옵션에 -insecure 추가",
        });
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 13,
            Foreground   = Brush(ResourceKeys.AccentAmberBrush),
            Text         = "-insecure는 모드에 필요한 VAC 끄기 옵션입니다(싱글플레이에서도 VAC가 켜져 있을 수 있음). " +
                           "모드가 설치된 동안 VAC 보안 서버에 접속하지 마세요. VAC 차단을 받을 수 있습니다.\n\n" +
                           "BONG DLSS5는 BONG DLSS5에서 게임을 시작할 때만 -insecure를 추가합니다. Steam에서 시작한다면 " +
                           "Steam의 게임 실행 옵션에도 -insecure를 추가하세요.",
        });
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 13,
            Text         = "게임의 고급 비디오 설정에서 High Dynamic Range를 Full로 설정하세요.",
        });
        var dontShow = new CheckBox { Content = "다시 표시 안 함", FontSize = 12 };
        panel.Children.Add(dontShow);

        var dlg = new ContentDialog
        {
            Title               = "Source 엔진 HDR 모드",
            Content             = panel,
            PrimaryButtonText   = "설치",
            CloseButtonText     = "취소",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = _window.Content.XamlRoot,
            Background          = Brush(ResourceKeys.SurfaceOverlayBrush),
            RequestedTheme      = ElementTheme.Dark,
        };
        var result = await DialogService.ShowSafeAsync(dlg);
        bool install = result == ContentDialogResult.Primary;
        // Only remembered when installing: cancelling with it ticked shouldn't skip the dialog next time
        if (install && dontShow.IsChecked == true)
        {
            settings.SourceEngineNoticeDismissed = true;
            _window.ViewModel.SaveSettingsPublic();
        }
        return install;
    }

    // ── UE Extended Warning Dialog ──────────────────────────────────────────────

    /// <summary>
    /// Determines the AddonType from the Info button sender based on its x:Name or DataContext.
    /// Detail view buttons use x:Name; Card flyout buttons store AddonType in DataContext.
    /// </summary>
    private static AddonType? GetAddonTypeFromInfoButton(object sender)
    {
        if (sender is not Button btn) return null;

        // Card flyout Info buttons store AddonType directly in DataContext
        if (btn.DataContext is AddonType addonType)
            return addonType;

        // Detail view Info buttons use x:Name
        return btn.Name switch
        {
            "DetailRefInfoBtn"  => AddonType.REFramework,
            "DetailRsInfoBtn"   => AddonType.ReShade,
            "DetailRdxInfoBtn"  => AddonType.RenoDX,
            "DetailLumaInfoBtn" => AddonType.Luma,
            "DetailUlInfoBtn"   => AddonType.ReLimiter,
            "DetailDcInfoBtn"   => AddonType.DisplayCommander,
            "DetailOsInfoBtn"   => AddonType.OptiScaler,
            _ => null
        };
    }

    /// <summary>
    /// Shows the per-addon Info dialog for the given button sender.
    /// Resolves content via AddonInfoResolver and displays it in a styled ContentDialog.
    /// Handles RenoDX, Luma, and OptiScaler-specific content rendering.
    /// </summary>
    public async Task ShowAddonInfoDialogAsync(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null) return;

        var addonType = GetAddonTypeFromInfoButton(sender);
        if (addonType == null) return;

        try
        {
            var manifest = _window.ViewModel.Manifest;
            var osWikiData = _optiScalerWikiService.CachedData;
            var hdrDatabase = _hdrDatabaseService.CachedData;
            var nexusService = App.Services.GetRequiredService<INexusUpdateService>() as NexusUpdateService;
            var nexusSummaries = nexusService?.ModSummaries;

            // On-demand fetch: if this is a RenoDX game with a Nexus URL and no cached summary, fetch it now
            if (addonType == AddonType.RenoDX && nexusService != null
                && (nexusSummaries == null || !nexusSummaries.ContainsKey(card.GameName)))
            {
                var nexusUrl = card.ExternalUrl;
                if (string.IsNullOrEmpty(nexusUrl) || !nexusUrl.Contains("nexusmods.com", StringComparison.OrdinalIgnoreCase))
                    nexusUrl = card.NexusUrl;
                if (!string.IsNullOrEmpty(nexusUrl) && nexusUrl.Contains("nexusmods.com", StringComparison.OrdinalIgnoreCase))
                {
                    await nexusService.FetchSummaryAsync(card.GameName, nexusUrl);
                    nexusSummaries = nexusService.ModSummaries;
                }
            }
            var resolver = new AddonInfoResolver();
            var result = resolver.Resolve(card, addonType.Value, manifest, osWikiData, hdrDatabase, nexusSummaries);

            var textColour = Brush(ResourceKeys.TextSecondaryBrush);
            var linkColour = Brush(ResourceKeys.AccentBlueBrush);
            var dimColour  = Brush(ResourceKeys.TextTertiaryBrush);
            var outerPanel = new StackPanel { Spacing = 10 };

            // ── Addon-specific content rendering ──────────────────────────────
            switch (addonType.Value)
            {
                case AddonType.RenoDX:
                    BuildRenoDXContent(outerPanel, card, result, manifest, textColour, linkColour, dimColour);
                    break;

                case AddonType.Luma:
                    BuildLumaContent(outerPanel, card, result, manifest, textColour, linkColour, dimColour);
                    break;

                case AddonType.OptiScaler:
                    BuildOptiScalerContent(outerPanel, result, textColour, linkColour, dimColour);
                    break;

                case AddonType.ReLimiter:
                    await _window.ViewModel.EnsureUlReleaseBodyAsync(card.UlInstalledVersion);
                    BuildReleaseNotesContent(outerPanel, result, card.UlInstalledVersion,
                        _window.ViewModel.LatestUlReleaseBody,
                        "https://github.com/RankFTW/ReLimiter/releases",
                        textColour, linkColour, dimColour);
                    break;

                case AddonType.DisplayCommander:
                    await _window.ViewModel.EnsureDcReleaseBodyAsync(card.DcInstalledVersion);
                    BuildReleaseNotesContent(outerPanel, result, card.DcInstalledVersion,
                        _window.ViewModel.LatestDcReleaseBody,
                        "https://github.com/pmnoxx/display-commander/releases",
                        textColour, linkColour, dimColour);
                    break;

                default:
                    BuildGenericContent(outerPanel, result, textColour, linkColour);
                    break;
            }

            var scrollContent = new ScrollViewer
            {
                Content   = outerPanel,
                MaxHeight = 440,
                Padding   = new Thickness(0, 4, 12, 0),
            };

            var addonName = GetAddonDisplayName(addonType.Value);

            var dialog = new ContentDialog
            {
                Title           = $"{addonName} — {card.GameName}",
                Content         = scrollContent,
                CloseButtonText = "닫기",
                XamlRoot        = _window.Content.XamlRoot,
                Background      = Brush(ResourceKeys.SurfaceToolbarBrush),
                RequestedTheme  = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(dialog);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[DialogService.ShowAddonInfoDialogAsync] Failed for '{card.GameName}' — {ex.Message}");
        }
    }

    /// <summary>Returns the human-readable display name for an addon type.</summary>
    private static string GetAddonDisplayName(AddonType addon) => addon switch
    {
        AddonType.REFramework     => "RE Framework",
        AddonType.ReShade         => "ReShade",
        AddonType.RenoDX          => "RenoDX",
        AddonType.ReLimiter       => "ReLimiter",
        AddonType.DisplayCommander => "Display Commander",
        AddonType.OptiScaler      => "OptiScaler",
        AddonType.Luma            => "Luma",
        _                         => "Info"
    };

    // ── RenoDX-specific dialog content (8.2) ─────────────────────────────────────

    /// <summary>
    /// Builds RenoDX-specific dialog content: wiki status badge, wiki notes,
    /// manifest gameNotes, and wiki page link as clickable hyperlink.
    /// Reuses the badge rendering pattern from the old NotesButton_Click.
    /// </summary>
    private void BuildRenoDXContent(
        StackPanel panel,
        GameCardViewModel card,
        AddonInfoResult result,
        RemoteManifest? manifest,
        SolidColorBrush textColour,
        SolidColorBrush linkColour,
        SolidColorBrush dimColour)
    {
        // ── RTX HDR mode — show dedicated content instead of RenoDX wiki ──────
        if (card.IsRtxHdrEnabled)
        {
            // RTX HDR badge (green, same style as Luma badge)
            var rtxHdrBadge = new Border
            {
                CornerRadius        = new CornerRadius(6),
                Padding             = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background          = Brush(ResourceKeys.AccentGreenBgBrush),
                BorderBrush         = Brush(ResourceKeys.AccentGreenBorderBrush),
                BorderThickness     = new Thickness(1),
                Child = new TextBlock
                {
                    Text       = "RTX HDR 켜짐",
                    FontSize   = 12,
                    Foreground = Brush(ResourceKeys.AccentGreenBrush),
                }
            };
            panel.Children.Add(rtxHdrBadge);

            panel.Children.Add(new TextBlock
            {
                Text = "RTX HDR은 NVIDIA 드라이버 수준의 HDR 주입으로 SDR 게임을 HDR로 업그레이드합니다. " +
                       "게임에 DLL을 주입하지 않고 GPU 수준에서 작동하므로 안티치트 시스템과 호환됩니다.\n\n" +
                       "요구 사항: NVIDIA 앱 설치, Game Filter/Freestyle 사용, RTX GPU, 드라이버 550 이상.",
                TextWrapping = TextWrapping.Wrap,
                Foreground   = textColour,
                FontSize     = 13,
                LineHeight   = 22,
            });

            AddHyperlinkBlock(panel, "RTX HDR 보정 가이드", manifest?.RtxHdrInfoUrl ?? "https://www.reddit.com/r/nvidia/comments/1b03yfg/rtx_hdr_paper_white_gamma_reference_settings/", linkColour);

            // Still show HDR Gaming Database link if available
            if (!string.IsNullOrEmpty(result.HdrAnalysisUrl))
            {
                AddHyperlinkBlock(panel, "HDR 분석 — HDR Gaming Database", result.HdrAnalysisUrl, linkColour);
            }

            return; // Skip regular RenoDX content
        }

        // ── Wiki notes text ──────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(result.Content))
        {
            panel.Children.Add(new TextBlock
            {
                Text         = result.Content,
                TextWrapping = TextWrapping.Wrap,
                Foreground   = textColour,
                FontSize     = 13,
                LineHeight   = 22,
            });
        }

        // ── Manifest gameNotes supplement (when wiki is the source, manifest
        //    gameNotes may still have additional per-game notes) ───────────────
        if (result.Source == InfoSourceType.Wiki && manifest?.GameNotes != null
            && manifest.GameNotes.TryGetValue(card.GameName, out var gameNote)
            && !string.IsNullOrWhiteSpace(gameNote.Notes))
        {
            AddTextOrHyperlink(panel, gameNote.Notes, gameNote.NotesUrl,
                gameNote.NotesUrlLabel, textColour, linkColour);
        }

        // ── DB Comments supplement for Unity games ───────────────────────────
        // card.Notes carries the DB comment ("📋 Game-specific settings:\n{comment}")
        // set by BuildNotes() but never read by the Tier-2 wiki path in AddonInfoResolver.
        if (!string.IsNullOrWhiteSpace(card.Notes) && card.Mod?.IsGenericUnity == true)
        {
            panel.Children.Add(new TextBlock
            {
                Text         = card.Notes,
                TextWrapping = TextWrapping.Wrap,
                Foreground   = textColour,
                FontSize     = 13,
                LineHeight   = 22,
                Margin       = new Thickness(0, 8, 0, 0),
            });
        }

        // ── Wiki page link (NameUrl) as clickable hyperlink ──────────────────
        if (!string.IsNullOrEmpty(result.Url))
        {
            AddHyperlinkBlock(panel, result.UrlLabel ?? "위키 페이지 보기", result.Url, linkColour);
        }

        // ── "Also available on Nexus" link (Snapshot+Nexus games only) ───────
        if (!card.IsExternalOnly && card.Mod?.SnapshotUrl != null && !string.IsNullOrEmpty(card.NexusUrl))
        {
            AddHyperlinkBlock(panel, "Nexus Mods에서도 이용 가능", card.NexusUrl, linkColour);
        }

        // ── HDR Gaming Database link (supplementary) ─────────────────────────
        if (!string.IsNullOrEmpty(result.HdrAnalysisUrl))
        {
            AddHyperlinkBlock(panel, "HDR 분석 — HDR Gaming Database", result.HdrAnalysisUrl, linkColour);
        }

        // ── Fallback if no content at all ────────────────────────────────────
        if (panel.Children.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text       = "이 게임에 대한 추가 RenoDX 메모가 없습니다.",
                Foreground = dimColour,
                FontSize   = 13,
            });
        }
    }

    // ── Luma-specific dialog content (8.3) ───────────────────────────────────────

    /// <summary>
    /// Builds Luma-specific dialog content: Luma badge, LumaMod wiki notes
    /// (SpecialNotes + FeatureNotes), and lumaGameNotes manifest content.
    /// Reuses the Luma notes rendering pattern from the old NotesButton_Click.
    /// </summary>
    private void BuildLumaContent(
        StackPanel panel,
        GameCardViewModel card,
        AddonInfoResult result,
        RemoteManifest? manifest,
        SolidColorBrush textColour,
        SolidColorBrush linkColour,
        SolidColorBrush dimColour)
    {
        // ── Luma badge — only show for named mods (generic mods use DLSS/HDR ticks instead) ──
        if (card.LumaMod?.IsGenericLuma != true)
        {
            var lumaLabel = card.LumaMod != null
                ? $"Luma — {card.LumaMod.Status} {card.LumaMod.Author}".TrimEnd()
                : "Luma";
            var lumaBadge = new Border
            {
                CornerRadius        = new CornerRadius(6),
                Padding             = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background          = Brush(ResourceKeys.AccentGreenBgBrush),
                BorderBrush         = Brush(ResourceKeys.AccentGreenBorderBrush),
                BorderThickness     = new Thickness(1),
                Child = new TextBlock
                {
                    Text       = lumaLabel,
                    FontSize   = 12,
                    Foreground = Brush(ResourceKeys.AccentGreenBrush),
                }
            };
            panel.Children.Add(lumaBadge);
        }

        // ── Feature flags (HDR / DLSS+FSR) for generic Luma games ─────────────
        if (card.LumaMod?.IsGenericLuma == true && (card.LumaHdrSupported || card.LumaDlssFsrSupported))
        {
            var flagPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 4, 0, 0) };

            if (card.LumaDlssFsrSupported)
                flagPanel.Children.Add(new TextBlock
                {
                    Text = "✅ DLSS / FSR",
                    FontSize = 12,
                    Foreground = Brush(ResourceKeys.AccentGreenBrush),
                });

            if (card.LumaHdrSupported)
                flagPanel.Children.Add(new TextBlock
                {
                    Text = "✅ HDR",
                    FontSize = 12,
                    Foreground = Brush(ResourceKeys.AccentGreenBrush),
                });

            panel.Children.Add(flagPanel);
        }

        // ── LumaMod wiki notes (SpecialNotes + FeatureNotes) ─────────────────
        var lumaNotesText = "";
        if (card.LumaMod != null)
        {
            if (!string.IsNullOrWhiteSpace(card.LumaMod.SpecialNotes))
                lumaNotesText += card.LumaMod.SpecialNotes;
            if (!string.IsNullOrWhiteSpace(card.LumaMod.FeatureNotes))
            {
                if (lumaNotesText.Length > 0) lumaNotesText += "\n\n";
                lumaNotesText += card.LumaMod.FeatureNotes;
            }
        }

        if (!string.IsNullOrWhiteSpace(lumaNotesText))
        {
            panel.Children.Add(new TextBlock
            {
                Text         = lumaNotesText,
                TextWrapping = TextWrapping.Wrap,
                Foreground   = textColour,
                FontSize     = 13,
                LineHeight   = 22,
            });
        }

        // ── Manifest lumaGameNotes content ───────────────────────────────────
        if (!string.IsNullOrWhiteSpace(card.LumaNotes))
        {
            AddTextOrHyperlink(panel, card.LumaNotes, card.LumaNotesUrl,
                card.LumaNotesUrlLabel, textColour, linkColour);
        }
        else if (manifest?.LumaGameNotes != null
            && manifest.LumaGameNotes.TryGetValue(card.GameName, out var lumaNote)
            && !string.IsNullOrWhiteSpace(lumaNote.Notes))
        {
            AddTextOrHyperlink(panel, lumaNote.Notes, lumaNote.NotesUrl,
                lumaNote.NotesUrlLabel, textColour, linkColour);
        }

        // ── Manifest-source content (Tier 1 override) ───────────────────────
        if (result.Source == InfoSourceType.Manifest && !string.IsNullOrWhiteSpace(result.Content))
        {
            // When manifest is the source, the result.Content is the manifest entry.
            // Only add if not already shown via LumaNotes above.
            if (result.Content != card.LumaNotes)
            {
                panel.Children.Add(new TextBlock
                {
                    Text         = result.Content,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground   = textColour,
                    FontSize     = 13,
                    LineHeight   = 22,
                });
            }
            if (!string.IsNullOrEmpty(result.Url))
                AddHyperlinkBlock(panel, result.UrlLabel ?? result.Url, result.Url, linkColour);
        }

        // ── HDR Gaming Database link (supplementary) ─────────────────────────
        if (!string.IsNullOrEmpty(result.HdrAnalysisUrl))
        {
            AddHyperlinkBlock(panel, "HDR 분석 — HDR Gaming Database", result.HdrAnalysisUrl, linkColour);
        }

        // ── Fallback if no content at all ────────────────────────────────────
        if (panel.Children.Count == 0) // no content at all
        {
            if (result.Source == InfoSourceType.Fallback && !string.IsNullOrWhiteSpace(result.Content))
            {
                panel.Children.Add(new TextBlock
                {
                    Text         = result.Content,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground   = textColour,
                    FontSize     = 13,
                    LineHeight   = 22,
                });
            }
            else if (panel.Children.Count <= 1)
            {
                panel.Children.Add(new TextBlock
                {
                    Text       = "이 게임에 대한 추가 Luma 메모가 없습니다.",
                    Foreground = dimColour,
                    FontSize   = 13,
                });
            }
        }
    }

    // ── OptiScaler-specific dialog content (8.4) ─────────────────────────────────

    /// <summary>
    /// Builds OptiScaler-specific dialog content: compatibility status, supported
    /// upscalers, notes, and separate "OptiScaler Compatibility" / "FSR4 Compatibility"
    /// sections when both exist. Detail page URLs rendered as clickable hyperlinks.
    /// </summary>
    private void BuildOptiScalerContent(
        StackPanel panel,
        AddonInfoResult result,
        SolidColorBrush textColour,
        SolidColorBrush linkColour,
        SolidColorBrush dimColour)
    {
        if (result.Source == InfoSourceType.Wiki)
        {
            // ── Standard OptiScaler Compatibility section ─────────────────────
            if (result.OptiScalerCompat != null)
            {
                BuildOptiScalerSection(panel, "OptiScaler 호환성",
                    result.OptiScalerCompat, textColour, linkColour);
            }

            // ── FSR4 Compatibility section ────────────────────────────────────
            if (result.OptiScalerFsr4Compat != null)
            {
                // Add spacing between sections
                if (result.OptiScalerCompat != null)
                    panel.Children.Add(new Border { Height = 4 });

                BuildOptiScalerSection(panel, "FSR4 호환성",
                    result.OptiScalerFsr4Compat, textColour, linkColour);
            }

            // ── General notes from formatted content (if not already shown) ──
            // The result.Content contains the full formatted text; individual
            // sections above handle structured display. Add any remaining notes.
            if (result.OptiScalerCompat == null && result.OptiScalerFsr4Compat == null
                && !string.IsNullOrWhiteSpace(result.Content))
            {
                panel.Children.Add(new TextBlock
                {
                    Text         = result.Content,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground   = textColour,
                    FontSize     = 13,
                    LineHeight   = 22,
                });
                // Show URL only for the unstructured fallback path (no per-section links)
                if (!string.IsNullOrEmpty(result.Url))
                    AddHyperlinkBlock(panel, result.UrlLabel ?? "위키 페이지 보기", result.Url, linkColour);
            }
        }
        else
        {
            // Manifest or fallback content
            BuildGenericContent(panel, result, textColour, linkColour);
        }

        if (panel.Children.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text       = "이 게임에 대한 OptiScaler 호환성 데이터가 없습니다.",
                Foreground = dimColour,
                FontSize   = 13,
            });
        }
    }

    /// <summary>
    /// Builds a single OptiScaler compatibility section (standard or FSR4).
    /// Shows status, upscaler list, and notes.
    /// </summary>
    private static void BuildOptiScalerSection(
        StackPanel panel,
        string sectionTitle,
        OptiScalerCompatEntry entry,
        SolidColorBrush textColour,
        SolidColorBrush linkColour)
    {
        var upscalers = entry.Upscalers.Count > 0
            ? string.Join(", ", entry.Upscalers)
            : "목록 없음";

        // Section header with status
        panel.Children.Add(new TextBlock
        {
            Text         = $"{sectionTitle}: {entry.Status}",
            TextWrapping = TextWrapping.Wrap,
            Foreground   = textColour,
            FontSize     = 13,
            FontWeight   = Microsoft.UI.Text.FontWeights.SemiBold,
            LineHeight   = 22,
        });

        // Upscaler list
        panel.Children.Add(new TextBlock
        {
            Text         = $"업스케일러: {upscalers}",
            TextWrapping = TextWrapping.Wrap,
            Foreground   = textColour,
            FontSize     = 13,
            LineHeight   = 22,
        });

        // Notes
        if (!string.IsNullOrWhiteSpace(entry.Notes))
        {
            panel.Children.Add(new TextBlock
            {
                Text         = entry.Notes,
                TextWrapping = TextWrapping.Wrap,
                Foreground   = textColour,
                FontSize     = 13,
                LineHeight   = 22,
            });
        }

        // Detail page URL
        if (!string.IsNullOrEmpty(entry.DetailPageUrl))
        {
            var link = new Microsoft.UI.Xaml.Documents.Hyperlink
            {
                NavigateUri = new Uri(entry.DetailPageUrl),
                Foreground  = linkColour,
            };
            link.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
            {
                Text     = "자세히 보기",
                FontSize = 13,
            });
            var para = new Microsoft.UI.Xaml.Documents.Paragraph();
            para.Inlines.Add(link);
            var rtb = new RichTextBlock { IsTextSelectionEnabled = true };
            rtb.Blocks.Add(para);
            panel.Children.Add(rtb);
        }
    }

    // ── Generic content rendering ────────────────────────────────────────────────

    /// <summary>
    /// Builds generic dialog content for addons without special rendering needs.
    /// Shows text content and optional URL hyperlink.
    /// </summary>
    private static void BuildGenericContent(
        StackPanel panel,
        AddonInfoResult result,
        SolidColorBrush textColour,
        SolidColorBrush linkColour)
    {
        if (!string.IsNullOrWhiteSpace(result.Content))
        {
            panel.Children.Add(new TextBlock
            {
                Text         = result.Content,
                TextWrapping = TextWrapping.Wrap,
                Foreground   = textColour,
                FontSize     = 13,
                LineHeight   = 22,
            });
        }

        if (!string.IsNullOrEmpty(result.Url))
            AddHyperlinkBlock(panel, result.UrlLabel ?? result.Url, result.Url, linkColour);

        // ── HDR Gaming Database link (supplementary) ─────────────────────────
        if (!string.IsNullOrEmpty(result.HdrAnalysisUrl))
            AddHyperlinkBlock(panel, "HDR 분석 — HDR Gaming Database", result.HdrAnalysisUrl, linkColour);
    }

    // ── Release notes content rendering (ReLimiter, Display Commander) ───────────

    /// <summary>
    /// Builds dialog content showing the GitHub release notes for the installed version.
    /// Falls back to the generic addon description if release notes are not available.
    /// </summary>
    private static void BuildReleaseNotesContent(
        StackPanel panel,
        AddonInfoResult result,
        string? installedVersion,
        string? releaseBody,
        string releasesPageUrl,
        SolidColorBrush textColour,
        SolidColorBrush linkColour,
        SolidColorBrush dimColour)
    {
        // Show installed version header if available
        if (!string.IsNullOrWhiteSpace(installedVersion))
        {
            panel.Children.Add(new TextBlock
            {
                Text       = $"설치됨: {installedVersion}",
                Foreground = dimColour,
                FontSize   = 12,
                Margin     = new Thickness(0, 0, 0, 4),
            });
        }

        // Show manifest game-specific note first (above release notes) if present
        if (result.Source == InfoSourceType.Manifest && !string.IsNullOrWhiteSpace(result.Content))
        {
            panel.Children.Add(new TextBlock
            {
                Text         = result.Content,
                TextWrapping = TextWrapping.Wrap,
                Foreground   = textColour,
                FontSize     = 13,
                LineHeight   = 22,
                Margin       = new Thickness(0, 0, 0, 12),
            });
            // Add a separator before release notes
            panel.Children.Add(new Border
            {
                Height      = 1,
                Background  = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255)),
                Margin      = new Thickness(0, 0, 0, 12),
            });
        }

        // Show release notes as markdown if available
        if (!string.IsNullOrWhiteSpace(releaseBody))
        {
            var markdown = new CommunityToolkit.WinUI.Controls.MarkdownTextBlock
            {
                Text              = releaseBody,
                Background        = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Foreground        = textColour,
                FontSize          = 12,
                UseEmphasisExtras = true,
                UseListExtras     = true,
                UseTaskLists      = true,
            };

            TabsTheme.StyleMarkdown(markdown);
            var markdownContainer = new Grid { RequestedTheme = ElementTheme.Dark };
            markdownContainer.Children.Add(markdown);
            panel.Children.Add(markdownContainer);
        }
        else if (result.Source != InfoSourceType.Manifest)
        {
            // Fall back to generic addon description (only if no manifest note already shown)
            if (!string.IsNullOrWhiteSpace(result.Content))
            {
                panel.Children.Add(new TextBlock
                {
                    Text         = result.Content,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground   = textColour,
                    FontSize     = 13,
                    LineHeight   = 22,
                });
            }
        }

        // Always show link to the releases page
        AddHyperlinkBlock(panel, "GitHub에서 모든 릴리스 보기", releasesPageUrl, linkColour);
    }

    // ── Shared helpers for dialog content ────────────────────────────────────────

    /// <summary>
    /// Adds a text block with an optional hyperlink. If a URL is present, renders
    /// the text followed by a clickable link on the next line.
    /// </summary>
    private static void AddTextOrHyperlink(
        StackPanel panel,
        string text,
        string? url,
        string? urlLabel,
        SolidColorBrush textColour,
        SolidColorBrush linkColour)
    {
        if (!string.IsNullOrEmpty(url))
        {
            var para = new Microsoft.UI.Xaml.Documents.Paragraph();
            para.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
            {
                Text       = text,
                Foreground = textColour,
                FontSize   = 13,
            });
            para.Inlines.Add(new Microsoft.UI.Xaml.Documents.LineBreak());
            var link = new Microsoft.UI.Xaml.Documents.Hyperlink
            {
                NavigateUri = new Uri(url),
                Foreground  = linkColour,
            };
            link.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
            {
                Text     = urlLabel ?? url,
                FontSize = 13,
            });
            para.Inlines.Add(link);
            var rtb = new RichTextBlock { IsTextSelectionEnabled = true };
            rtb.Blocks.Add(para);
            panel.Children.Add(rtb);
        }
        else
        {
            panel.Children.Add(new TextBlock
            {
                Text         = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground   = textColour,
                FontSize     = 13,
                LineHeight   = 22,
            });
        }
    }

    /// <summary>
    /// Adds a standalone clickable hyperlink block to the panel.
    /// </summary>
    private static void AddHyperlinkBlock(
        StackPanel panel,
        string label,
        string url,
        SolidColorBrush linkColour)
    {
        var link = new Microsoft.UI.Xaml.Documents.Hyperlink
        {
            NavigateUri = new Uri(url),
            Foreground  = linkColour,
        };
        link.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
        {
            Text     = label,
            FontSize = 13,
        });
        var para = new Microsoft.UI.Xaml.Documents.Paragraph();
        para.Inlines.Add(link);
        var rtb = new RichTextBlock { IsTextSelectionEnabled = true };
        rtb.Blocks.Add(para);
        panel.Children.Add(rtb);
    }

    // ── Vulkan Admin Required Dialog ────────────────────────────────────────────

    /// <summary>
    /// Shows a dialog when Vulkan ReShade install fails because the app isn't elevated.
    /// Offers to enable Admin Mode (persistent), restart once as admin, or cancel.
    /// </summary>
    public async Task ShowVulkanAdminRequiredDialogAsync()
    {
        try
        {
            while (_window.Content.XamlRoot == null)
                await Task.Delay(100);

            var dlg = new ContentDialog
            {
                Title = "관리자 권한 필요",
                Content = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                    Text = "Vulkan ReShade 레이어를 설치하려면 C:\\ProgramData\\ReShade\\에 쓰고 " +
                           "시스템 레지스트리 키를 수정해야 하므로 관리자 권한이 필요합니다.\n\n" +
                           "관리자 모드 사용 — BONG DLSS5가 항상 관리자 권한으로 실행됩니다(설정 후 UAC 메시지 없음).\n\n" +
                           "관리자로 다시 시작 — 이 설치를 완료하기 위해 한 번만 관리자 권한으로 다시 시작합니다.",
                },
                PrimaryButtonText = "관리자 모드 사용",
                SecondaryButtonText = "관리자로 다시 시작",
                CloseButtonText = "취소",
                XamlRoot = _window.Content.XamlRoot,
                Background = Brush(ResourceKeys.SurfaceOverlayBrush),
                RequestedTheme = ElementTheme.Dark,
            };

            var result = await ShowSafeAsync(dlg);

            if (result == ContentDialogResult.Primary)
            {
                // Enable Admin Mode (creates scheduled task + restarts)
                try
                {
                    CreateAdminTaskAndRestart();
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[DialogService.ShowVulkanAdminRequiredDialog] Admin mode setup failed — {ex.Message}");
                }
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // One-time restart as admin
                RestartAsAdmin();
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[DialogService.ShowVulkanAdminRequiredDialog] Failed — {ex.Message}");
        }
    }

    /// <summary>
    /// Creates the Admin Mode scheduled task (UAC prompt) and restarts RHI through it.
    /// </summary>
    private static void CreateAdminTaskAndRestart()
    {
        var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        var taskName = "RHI Admin Mode";

        // Create the task (triggers UAC)
        var createArgs = $"/Create /TN \"{taskName}\" /TR \"\\\"{exePath}\\\"\" /SC ONCE /ST 00:00 /SD 01/01/2000 /RL HIGHEST /F";
        var psi = new System.Diagnostics.ProcessStartInfo("schtasks.exe", createArgs)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
        };
        using var createProc = System.Diagnostics.Process.Start(psi);
        createProc?.WaitForExit(15000);
        if (createProc?.ExitCode != 0)
            throw new InvalidOperationException($"schtasks /Create failed (exit {createProc?.ExitCode})");

        CrashReporter.Log("[DialogService.CreateAdminTaskAndRestart] Admin task created, restarting via task...");

        // Run the task to relaunch elevated (no UAC this time)
        var runPsi = new System.Diagnostics.ProcessStartInfo("schtasks.exe", $"/Run /TN \"{taskName}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        System.Diagnostics.Process.Start(runPsi);

        // Exit current instance
        Environment.Exit(0);
    }

    /// <summary>
    /// Restarts RHI as admin via a one-time UAC prompt (no scheduled task).
    /// </summary>
    private static void RestartAsAdmin()
    {
        var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        var psi = new System.Diagnostics.ProcessStartInfo(exePath)
        {
            UseShellExecute = true,
            Verb = "runas",
        };

        try
        {
            System.Diagnostics.Process.Start(psi);
            CrashReporter.Log("[DialogService.RestartAsAdmin] Restarting as admin...");
            Environment.Exit(0);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled UAC prompt — do nothing
            CrashReporter.Log("[DialogService.RestartAsAdmin] User cancelled UAC prompt");
        }
    }
}
