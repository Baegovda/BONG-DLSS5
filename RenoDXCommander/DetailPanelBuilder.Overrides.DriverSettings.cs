// DetailPanelBuilder.Overrides.DriverSettings.cs — Driver Settings section (independent panel).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DetailPanelBuilder
{
    // Used by BuildNvidiaProfileBody to update the DLSS collapsed summary (still needed)
    private StackPanel? _nvHeaderRow;

    // Separate header row reference for the Driver Settings section summary
    private StackPanel? _driverHeaderRow;

    // Snapshot of all NVAPI values needed to build the driver profile section — fetched off the UI thread.
    private sealed record DriverProfileData(
        uint VSyncMode, uint? GlobalVSyncMode,
        uint VSyncTearControl,
        uint LowLatencyMode,
        uint SmoothMotionEnable,
        uint SmoothMotionApis,
        uint SmoothMotionFlipPacingFs,
        uint PowerManagementMode,
        bool PerGameGSyncEnabled,
        ulong ReBarSizeLimit,
        uint ReBarEnableMode,
        uint ReBarMode,
        ulong GlobalReBarSizeLimit,
        uint? PerGameFpsLimit, uint GlobalFpsLimit,
        bool IsAdmin);

    private const string DriverSummaryTag = "DriverSummary";   // collapsed-only header summary

    /// <summary>
    /// Footer line under the driver settings: "Driver 617.42 · ✓ Running as admin — …".
    /// The driver version is shown here when expanded; when collapsed it's in the header summary.
    /// </summary>
    private TextBlock MakeDriverFooterText(bool elevated, Thickness margin)
    {
        var tb = new TextBlock
        {
            FontSize          = 10,
            TextWrapping      = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = margin,
        };
        var driverVer = _dlssPresetService.DriverVersionString;
        if (!string.IsNullOrEmpty(driverVer))
            tb.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
            {
                Text       = $"Driver {driverVer}  ·  ",
                Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
            });
        tb.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
        {
            Text = elevated
                ? "✓ 관리자 권한으로 실행 중 — 모든 드라이버 프로필 설정을 변경할 수 있습니다."
                : "⚠ 드라이버 프로필 설정을 변경하려면 관리자 권한이 필요합니다. 설정에서 관리자 모드를 켜거나 관리자 권한으로 다시 시작하세요.",
            Foreground = UIFactory.Brush(elevated ? ResourceKeys.TextTertiaryBrush : ResourceKeys.AccentAmberDimBrush),
        });
        return tb;
    }

    private void BuildDriverProfileSection(GameCardViewModel card, string capturedName)
    {
        // ══════════════════════════════════════════════════════════════════════
        // Driver Settings — independent collapsible section
        // VSync, Low Latency, Smooth Motion, Power/G-Sync, ReBAR
        // ══════════════════════════════════════════════════════════════════════

        _window.NvidiaProfileDriverPanel.Children.Clear();

        const string driverSectionKey = "NvidiaProfileDriver";
        bool driverCollapsed = Layout.IsCollapsed(driverSectionKey);

        // Driver version goes in the collapsed summary, not the title, so the
        // title matches the other sections.
        var driverVer = _dlssPresetService.DriverVersionString;
        var driverHeaderText = "드라이버 설정";

        var driverArrow = new TextBlock
        {
            Text              = driverCollapsed ? "▶" : "▼",
            FontSize          = 10,
            Foreground        = UIFactory.Brush(ResourceKeys.TextTertiaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 6, 0),
        };
        var driverTitle = new TextBlock
        {
            Text              = driverHeaderText,
            FontSize          = 13,
            FontWeight        = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground        = UIFactory.Brush(ResourceKeys.TextPrimaryBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var driverHeaderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        ToolTipService.SetToolTip(driverArrow, SectionHeaderTooltip);
        ToolTipService.SetToolTip(driverTitle, SectionHeaderTooltip);
        driverHeaderRow.Children.Add(MakeDragHandle(_window.NvidiaProfileDriverContainer));
        driverHeaderRow.Children.Add(driverArrow);
        driverHeaderRow.Children.Add(driverTitle);
        _window.NvidiaProfileDriverPanel.Children.Add(driverHeaderRow);
        Layout.RegisterHeader(driverSectionKey, driverHeaderRow);

        var driverBody = new StackPanel { Spacing = 6, Visibility = driverCollapsed ? Visibility.Collapsed : Visibility.Visible };
        _window.NvidiaProfileDriverPanel.Children.Add(driverBody);

        driverHeaderRow.PointerEntered += (s, e) => driverTitle.Foreground = UIFactory.Brush(ResourceKeys.AccentTealBrush);
        driverHeaderRow.PointerExited  += (s, e) => driverTitle.Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush);
        var driverHandCursor  = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
        var driverArrowCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
        var driverCursorProp  = DetailPanelBuilder.CursorProp;
        driverHeaderRow.PointerEntered += (s, e) => driverCursorProp?.SetValue(driverHeaderRow, driverHandCursor);
        driverHeaderRow.PointerExited  += (s, e) => driverCursorProp?.SetValue(driverHeaderRow, driverArrowCursor);

        driverHeaderRow.PointerPressed += (s, e) =>
        {
            bool nowCollapsed = driverBody.Visibility == Visibility.Visible;
            driverBody.Visibility = nowCollapsed ? Visibility.Collapsed : Visibility.Visible;
            driverArrow.Text = nowCollapsed ? "▶" : "▼";
            // Summary shows only while collapsed
            if (_driverHeaderRow != null)
                foreach (var child in _driverHeaderRow.Children)
                    if (child is TextBlock { Tag: DriverSummaryTag } tb)
                        tb.Visibility = nowCollapsed ? Visibility.Visible : Visibility.Collapsed;
            Layout.SetCollapsed(driverSectionKey, nowCollapsed);
        };

        _driverHeaderRow = driverHeaderRow;

        if (!_dlssPresetService.IsSupported)
        {
            // No profile data to read: the summary is just the driver version
            if (!string.IsNullOrEmpty(driverVer)
                && DetailPanelBuilder.MakeSectionSummaryInlines(new[] { ("드라이버", (string?)driverVer) },
                    "NVIDIA 드라이버 버전.") is { } verTb)
            {
                verTb.Tag = DriverSummaryTag;
                verTb.Visibility = driverCollapsed ? Visibility.Visible : Visibility.Collapsed;
                driverHeaderRow.Children.Add(verTb);
            }

            bool elevated = VulkanLayerService.IsRunningAsAdmin();
            driverBody.Children.Add(MakeDriverFooterText(elevated, new Thickness(0, 4, 0, 0)));
            return;
        }

        var gameName    = card.GameName;
        var installPath = card.InstallPath ?? "";
        var gameSource  = card.Source ?? "";
        var targetCard  = card;
        var svc         = _dlssPresetService;

        // Dedicated container for the driver grid — swapped atomically by TryEnqueue
        var driverContainer = new StackPanel();
        driverBody.Children.Add(driverContainer);

        var scanToken = _panelScanCts.Token;
        _ = Task.Run(async () =>
        {
            if (_window.ViewModel.SelectedGame?.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase) != true
                || _window.ViewModel.SelectedGame?.Source != gameSource)
                return;

            CrashReporter.Log($"[BuildDriverProfileSection] Waiting for semaphore: '{gameName}'");
            try { await _panelScanSemaphore.WaitAsync(scanToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { CrashReporter.Log($"[BuildDriverProfileSection] Semaphore cancelled: '{gameName}'"); return; }
            CrashReporter.Log($"[BuildDriverProfileSection] Semaphore acquired, reading NVAPI: '{gameName}'");
            DriverProfileData? data = null;
            try
            {
                using var scanCts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                var scanCt = scanCts.Token;
                var nvapiTask = Task.Run(() =>
                {
                    svc.PrimeProfileCache(gameName, installPath, scanCt);
                    return new DriverProfileData(
                        VSyncMode:                svc.GetVSyncMode(gameName, installPath),
                        GlobalVSyncMode:          svc.GetGlobalVSyncMode(),
                        VSyncTearControl:         svc.GetVSyncTearControl(gameName, installPath),
                        LowLatencyMode:           svc.GetLowLatencyMode(gameName, installPath),
                        SmoothMotionEnable:       svc.GetSmoothMotionEnable(gameName, installPath),
                        SmoothMotionApis:         svc.GetSmoothMotionApis(gameName, installPath),
                        SmoothMotionFlipPacingFs: svc.GetSmoothMotionFlipPacingFs(gameName, installPath),
                        PowerManagementMode:      svc.GetPowerManagementMode(gameName, installPath),
                        PerGameGSyncEnabled:      svc.GetPerGameGSyncEnabled(gameName, installPath),
                        ReBarSizeLimit:           svc.GetReBarSizeLimit(gameName, installPath),
                        ReBarEnableMode:          svc.GetReBarEnableMode(gameName, installPath),
                        ReBarMode:                svc.GetReBarMode(gameName, installPath),
                        GlobalReBarSizeLimit:     svc.GetGlobalReBarSizeLimit(),
                        PerGameFpsLimit:          svc.GetPerGameFpsLimit(gameName, installPath),
                        GlobalFpsLimit:           svc.GetGlobalFpsLimit(),
                        IsAdmin:                  VulkanLayerService.IsRunningAsAdmin());
                }, scanCt);
                using var delayCts = new CancellationTokenSource();
                var delayTask = Task.Delay(5000, delayCts.Token);
                var completed = await Task.WhenAny(nvapiTask, delayTask).ConfigureAwait(false);
                delayCts.Cancel();
                if (completed == nvapiTask)
                    data = await nvapiTask.ConfigureAwait(false);
                else
                    CrashReporter.Log($"[BuildDriverProfileSection] NVAPI reads timed out for '{gameName}' — using defaults");
            }
            finally
            {
                CrashReporter.Log($"[BuildDriverProfileSection] Semaphore releasing: '{gameName}'");
                _panelScanSemaphore.Release();
            }

            _window.DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (scanToken.IsCancellationRequested) return;
                if (_window.SettingsPanel.Visibility == Microsoft.UI.Xaml.Visibility.Visible || DialogService.IsDialogOpen)
                {
                    CrashReporter.Log($"[BuildDriverProfileSectionWithData] Skipped AddToTree for '{gameName}' — Settings panel is open");
                    return;
                }
                var currentCard = _window.ViewModel.SelectedGame;
                if (currentCard == null || !currentCard.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase)
                    || currentCard.Source != gameSource)
                    return;

                _window.ViewModel.SetLastUiAction($"BuildDriverProfileSectionWithData({gameName})");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var availW = driverContainer.ActualWidth > 0 ? driverContainer.ActualWidth : _window.NvidiaProfileDriverPanel.ActualWidth;
                var tempDriver = new StackPanel();
                BuildDriverProfileSectionWithData(targetCard, capturedName, svc, tempDriver, data, availW);
                sw.Stop();
                if (sw.ElapsedMilliseconds > 50)
                    CrashReporter.Log($"[BuildDriverProfileSectionWithData] SLOW build: '{gameName}' took {sw.ElapsedMilliseconds}ms");
                _window.ViewModel.SetLastUiAction($"BuildDriverProfileSectionWithData:AddToTree({gameName})");
                var sw2 = System.Diagnostics.Stopwatch.StartNew();
                driverContainer.Children.Clear();
                driverContainer.Children.Add(tempDriver);
                sw2.Stop();
                if (sw2.ElapsedMilliseconds > 50)
                    CrashReporter.Log($"[BuildDriverProfileSectionWithData] SLOW AddToTree: '{gameName}' took {sw2.ElapsedMilliseconds}ms on UI thread");

                // Update collapsed summary — appended at index 3 of driverHeaderRow
                if (data != null)
                {
                    _driverSummaryData = data;
                    _driverSummaryVer = driverVer;
                    _driverSummaryBody = driverBody;
                    UpdateDriverSummary();
                }
            });
        });
    }

    // Last values shown in the Driver Settings summary, so a change made in the panel can update it
    private DriverProfileData? _driverSummaryData;
    private string? _driverSummaryVer;
    private StackPanel? _driverSummaryBody;

    /// <summary>
    /// (Re)builds the Driver Settings header summary from <see cref="_driverSummaryData"/>. The same
    /// summary feeds Classic's collapsed header, Tabs' At a glance and Spotlight's Tune tile.
    /// </summary>
    private void UpdateDriverSummary()
    {
        var data = _driverSummaryData;
        if (_driverHeaderRow == null || data == null) return;
        try
        {
            for (int i = _driverHeaderRow.Children.Count - 1; i >= 3; i--)
                if (_driverHeaderRow.Children[i] is TextBlock { Tag: DriverSummaryTag })
                    _driverHeaderRow.Children.RemoveAt(i);

            var vsyncName = DlssPresetService.VSyncModeOptions
                .FirstOrDefault(o => o.Value == data.VSyncMode).Name ?? "기본값";
            var smoothName = data.SmoothMotionEnable != 0 ? "켜짐" : null;
            // ReBarEnableMode: 0=Off, 1=Auto (default), 2=On
            var rebarName = data.ReBarEnableMode == 2 ? "켜짐"
                          : data.ReBarEnableMode == 0 ? "꺼짐"
                          : null; // Auto = default, omit from summary

            var summaryEntries = new System.Collections.Generic.List<(string, string?)>();
            if (!string.IsNullOrEmpty(_driverSummaryVer))
                summaryEntries.Add(("드라이버", _driverSummaryVer));
            summaryEntries.Add(("VSync", vsyncName));
            if (smoothName != null)
                summaryEntries.Add(("Smooth", smoothName));
            if (rebarName != null)
                summaryEntries.Add(("ReBAR", rebarName));
            if (!data.PerGameGSyncEnabled)
                summaryEntries.Add(("G-Sync", "꺼짐"));
            if (data.PerGameFpsLimit is uint capFps)
                summaryEntries.Add(("FPS 제한", capFps == 0 ? "꺼짐" : $"{capFps}"));
            var summaryTb = DetailPanelBuilder.MakeSectionSummaryInlines(summaryEntries,
                "NVIDIA 드라이버 버전과 이 게임의 드라이버 프로필 설정입니다.\n"
                + "VSync는 항상 표시됩니다. Smooth(Smooth Motion), ReBAR, G-Sync, FPS 제한은 기본값과 다를 때만 표시됩니다.");
            if (summaryTb != null)
            {
                summaryTb.Tag = DriverSummaryTag;
                summaryTb.Visibility = _driverSummaryBody?.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
                _driverHeaderRow.Children.Add(summaryTb);
                Layout.SummaryChanged(); // Tabs / Spotlight: refresh the tile
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[DetailPanelBuilder.UpdateDriverSummary] {ex.Message}");
        }
    }

    private void BuildDriverProfileSectionWithData(GameCardViewModel card, string capturedName,
        DlssPresetService nvidiaPresetService, StackPanel? nvBody, DriverProfileData d,
        double containerWidth = 0)
    {
        // ══════════════════════════════════════════════════════════════════════
        // Driver Settings — VSync, Latency, Smooth Motion, Power/CPU, ReBAR
        // ══════════════════════════════════════════════════════════════════════
        if (nvidiaPresetService.IsSupported)
        {
            bool isAdmin = d.IsAdmin;

            var nvidiaGrid = new Grid { ColumnSpacing = 12, Opacity = isAdmin ? 1.0 : 0.4, IsHitTestVisible = isAdmin };
            // Use fixed-pixel column widths to avoid the WinUI infinite layout loop.
            // Star columns inside a StackPanel inside ScrollViewer cause an infinite measurement cycle.
            // We compute equal column widths from the container width: 4 columns + 3 x 1px dividers.
            // Fall back to 200px per column if container width isn't available yet.
            const double DividerWidth = 1.0;
            const int DividerCount   = 3;
            const int ColCount       = 4;
            const double ColSpacing  = 12.0; // nvidiaGrid.ColumnSpacing
            // Total width consumed: ColumnSpacing between all 7 columns (6 gaps) + 3 divider columns
            double overhead = (ColCount + DividerCount - 1) * ColSpacing + DividerCount * DividerWidth;
            double colW = containerWidth > overhead
                ? (containerWidth - overhead) / ColCount
                : 200.0;

            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colW) });
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DividerWidth) });
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colW) });
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DividerWidth) });
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colW) });
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DividerWidth) });
            nvidiaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colW) });

            var installPathSafe = card.InstallPath ?? "";

            // ── Column 0: VSync ──
            var vsyncCol = new StackPanel { Spacing = 4 };
            var vsyncLabel = new TextBlock { Text = "VSync", FontSize = 11, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) };
            ToolTipService.SetToolTip(vsyncLabel, "수직 동기화 설정 — 드라이버가 프레임 렌더링을 디스플레이 주사율에 맞추는 방식을 제어합니다.");
            vsyncCol.Children.Add(vsyncLabel);

            // VSync Mode
            {
                vsyncCol.Children.Add(new TextBlock { Text = "모드", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var options = DlssPresetService.VSyncModeOptions;
                uint current = d.VSyncMode;
                var globalVSync = d.GlobalVSyncMode;

                var itemsList = new List<string>();
                if (globalVSync.HasValue)
                {
                    var globalName = options.FirstOrDefault(o => o.Value == globalVSync.Value).Name ?? "앱 제어";
                    itemsList.Add($"Global ({globalName})");
                }
                itemsList.AddRange(options.Select(o => o.Name));
                var items = itemsList.ToArray();

                // Determine selected index
                int idx;
                if (globalVSync.HasValue)
                {
                    bool perGameMatchesGlobal = current == globalVSync.Value;
                    var perGameIdx = Array.FindIndex(options, o => o.Value == current);
                    idx = perGameMatchesGlobal ? 0 : (perGameIdx >= 0 ? perGameIdx + 1 : 0);
                }
                else
                {
                    idx = Array.FindIndex(options, o => o.Value == current);
                    if (idx < 0) idx = 0;
                }

                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    MaxDropDownHeight = 300,
                };
                ToolTipService.SetToolTip(combo, globalVSync.HasValue
                    ? "Global = 전역 설정을 따릅니다. 앱 제어: 게임이 결정합니다. 강제 끄기: VSync를 끕니다. 강제 켜기: 주사율에 고정합니다. Fast Sync: 자유롭게 렌더링하고 가장 최근에 완성된 프레임을 표시합니다."
                    : "VSync 모드 — 앱 제어: 게임이 결정합니다. 강제 끄기: VSync를 완전히 끕니다. 강제 켜기: 주사율에 고정합니다. Fast Sync: 자유롭게 렌더링하고 가장 최근에 완성된 프레임을 표시합니다.");
                var init = true;
                combo.SelectionChanged += (s, ev) =>
                {
                    if (init) return;
                    int i = combo.SelectedIndex;
                    if (i < 0 || i >= items.Length) return;
                    var selected = items[i];
                    if (selected.StartsWith("Global"))
                    {
                        // Inherit from global — write the global value
                        var valueToWrite = globalVSync ?? options[0].Value;
                        _ = Task.Run(() => nvidiaPresetService.SetVSyncMode(capturedName, installPathSafe, valueToWrite));
                    }
                    else
                    {
                        int optIdx = globalVSync.HasValue ? i - 1 : i;
                        if (optIdx >= 0 && optIdx < options.Length)
                        {
                            var valueToWrite = options[optIdx].Value;
                            _ = Task.Run(() => nvidiaPresetService.SetVSyncMode(capturedName, installPathSafe, valueToWrite));
                        }
                    }
                };
                vsyncCol.Children.Add(combo);
                init = false;
            }

            // VSync Tear Control
            {
                vsyncCol.Children.Add(new TextBlock { Text = "티어링 제어", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var options = DlssPresetService.VSyncTearControlOptions;
                uint current = d.VSyncTearControl;
                var items = options.Select(o => o.Name).ToArray();
                int idx = Array.FindIndex(options, o => o.Value == current);
                if (idx < 0) idx = 0;
                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    MaxDropDownHeight = 300,
                };
                ToolTipService.SetToolTip(combo, "VSync 티어링 제어 — 표준: 일반 VSync 동작. 적응형: FPS가 주사율 이상이면 VSync 켜짐, 미만이면 꺼짐 (낮은 FPS에서 끊김 감소).");
                var init = true;
                combo.SelectionChanged += (s, ev) =>
                {
                    if (init) return;
                    int i = combo.SelectedIndex;
                    if (i < 0 || i >= options.Length) return;
                    var valueToWrite = options[i].Value;
                    _ = Task.Run(() => nvidiaPresetService.SetVSyncTearControl(capturedName, installPathSafe, valueToWrite));
                };
                vsyncCol.Children.Add(combo);
                init = false;
            }

            // Low Latency Mode (in VSync column)
            {
                var lowLatencyLabel = new TextBlock { Text = "저지연", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) };
                vsyncCol.Children.Add(lowLatencyLabel);
                var options = DlssPresetService.LowLatencyModeOptions;
                uint current = d.LowLatencyMode;
                var items = options.Select(o => o.Name).ToArray();
                int idx = Array.FindIndex(options, o => o.Value == current);
                if (idx < 0) idx = 0;
                // Locked to Ultra while Smooth Motion is enabled — must turn off Smooth Motion first
                bool smoothOn = d.SmoothMotionEnable != 0;
                bool latencyLocked = smoothOn;
                // The combo is disabled while locked and disabled controls show no tooltip,
                // so the reason goes on the visible label above it.
                if (latencyLocked)
                    ToolTipService.SetToolTip(lowLatencyLabel,
                        "Smooth Motion이 켜져 있는 동안 저지연은 Ultra로 고정됩니다. 변경하려면 Smooth Motion을 끄세요.");
                var combo2 = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    MaxDropDownHeight = 300,
                    IsEnabled = !latencyLocked,
                    Opacity = latencyLocked ? 0.4 : 1.0,
                };
                ToolTipService.SetToolTip(combo2, latencyLocked
                    ? "Smooth Motion이 켜져 있는 동안 저지연은 Ultra로 고정됩니다. 이 설정을 변경하려면 Smooth Motion을 끄세요."
                    : "저지연 모드 — 꺼짐: 게임이 프레임 대기열을 제어합니다. 켜짐: 미리 렌더링된 프레임을 1개로 제한합니다 (지연 감소). Ultra: 적시 프레임 제출 (최저 지연, FPS가 약간 감소할 수 있음).");
                var init2 = true;
                combo2.SelectionChanged += (s, ev) =>
                {
                    if (init2) return;
                    int i = combo2.SelectedIndex;
                    if (i < 0 || i >= options.Length) return;
                    var valueToWrite = options[i].Value;
                    _ = Task.Run(() => nvidiaPresetService.SetLowLatencyMode(capturedName, installPathSafe, valueToWrite));
                };
                vsyncCol.Children.Add(combo2);
                init2 = false;
            }

            Grid.SetColumn(vsyncCol, 0);
            nvidiaGrid.Children.Add(vsyncCol);
            nvidiaGrid.Children.Add(MakeDlssDivider(1));
            _window.ViewModel.SetLastUiAction($"BuildDriverProfileSectionWithData:VSync done({capturedName})");

            // ── Column 4: Smooth Motion ──
            var smoothCol = new StackPanel { Spacing = 4 };
            var smoothLabel = new TextBlock { Text = "Smooth Motion", FontSize = 11, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) };
            ToolTipService.SetToolTip(smoothLabel, "NVIDIA Smooth Motion — 드라이버 수준 프레임 생성. 보간 프레임을 추가해 화면을 더 부드럽게 만듭니다. RTX 40 시리즈 이상 필요.");
            smoothCol.Children.Add(smoothLabel);

            // Enable
            bool smoothMotionEnabled;
            {
                smoothCol.Children.Add(new TextBlock { Text = "사용", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var options = DlssPresetService.SmoothMotionEnableOptions;
                uint current = d.SmoothMotionEnable;
                smoothMotionEnabled = current != 0;
                var items = options.Select(o => o.Name).ToArray();
                int idx = Array.FindIndex(options, o => o.Value == current);
                if (idx < 0) idx = 0;
                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    MaxDropDownHeight = 300,
                };
                ToolTipService.SetToolTip(combo, "Smooth Motion 사용 — 꺼짐: 사용 안 함. 켜짐: 드라이버 수준 프레임 생성 사용 (RTX 40 시리즈 이상 전용).");
                var init = true;
                combo.SelectionChanged += (s, ev) =>
                {
                    if (init) return;
                    int i = combo.SelectedIndex;
                    if (i < 0 || i >= options.Length) return;
                    var selectedValue = options[i].Value;
                    bool enabling = selectedValue != 0;
                    const uint LowLatencyOff   = 0x00000000;
                    const uint LowLatencyUltra = 0x00000002;
                    _ = Task.Run(() =>
                    {
                        nvidiaPresetService.SetSmoothMotionEnable(capturedName, installPathSafe, selectedValue);
                        // Cascade: set APIs to All when enabling, None when disabling
                        nvidiaPresetService.SetSmoothMotionApis(capturedName, installPathSafe, enabling ? 0x00000007u : 0x00000000u);
                        // Cascade Low Latency: Ultra when enabling, restore previous value when disabling
                        uint? savedLatency = null;
                        if (enabling)
                        {
                            uint prevLatency = nvidiaPresetService.GetLowLatencyMode(capturedName, installPathSafe);
                            savedLatency = prevLatency != LowLatencyUltra ? prevLatency : (uint?)null;
                            nvidiaPresetService.SetLowLatencyMode(capturedName, installPathSafe, LowLatencyUltra);
                        }
                        else
                        {
                            uint restoreValue = card.PreSmoothMotionLowLatency ?? LowLatencyOff;
                            nvidiaPresetService.SetLowLatencyMode(capturedName, installPathSafe, restoreValue);
                        }
                        // Marshal card state mutation back to UI thread
                        _window.DispatcherQueue?.TryEnqueue(() =>
                        {
                            card.PreSmoothMotionLowLatency = enabling ? savedLatency : null;
                        });
                    });
                    // Rebuild only the driver profile section — Smooth Motion affects the Low Latency
                    // row's enabled state and the APIs combo. Rebuilding the full panel is unnecessary.
                    _window.DispatcherQueue?.TryEnqueue(() => BuildDriverProfileSection(card, card.GameName));
                };
                smoothCol.Children.Add(combo);
                init = false;
            }

            // APIs
            {
                var apisLabel = new TextBlock { Text = "허용 API", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) };
                if (!smoothMotionEnabled)
                    ToolTipService.SetToolTip(apisLabel, "변경하려면 Smooth Motion을 켜세요.");
                smoothCol.Children.Add(apisLabel);
                var options = DlssPresetService.SmoothMotionApisOptions;
                uint current = d.SmoothMotionApis;
                var items = options.Select(o => o.Name).ToArray();
                int idx = Array.FindIndex(options, o => o.Value == current);
                if (idx < 0) idx = 0;
                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    MaxDropDownHeight = 300,
                    IsEnabled = smoothMotionEnabled,
                    Opacity = smoothMotionEnabled ? 1.0 : 0.4,
                };
                ToolTipService.SetToolTip(combo, "Smooth Motion API — Smooth Motion이 후킹할 수 있는 그래픽 API입니다. 없음 = 모든 API에서 사용 안 함.");
                var init = true;
                combo.SelectionChanged += (s, ev) =>
                {
                    if (init) return;
                    int i = combo.SelectedIndex;
                    if (i < 0 || i >= options.Length) return;
                    var valueToWrite = options[i].Value;
                    _ = Task.Run(() => nvidiaPresetService.SetSmoothMotionApis(capturedName, installPathSafe, valueToWrite));
                };
                smoothCol.Children.Add(combo);
                init = false;
            }

            // Flip Pacing (combined — sets both Fullscreen and Windowed together)
            {
                var flipPacingLabel = new TextBlock { Text = "플립 페이싱", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) };
                if (!smoothMotionEnabled)
                    ToolTipService.SetToolTip(flipPacingLabel, "변경하려면 Smooth Motion을 켜세요.");
                smoothCol.Children.Add(flipPacingLabel);
                var options = DlssPresetService.SmoothMotionFlipPacingFsOptions;
                uint current = d.SmoothMotionFlipPacingFs;
                var items = options.Select(o => o.Name).ToArray();
                int idx = Array.FindIndex(options, o => o.Value == current);
                if (idx < 0) idx = 0;
                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    MaxDropDownHeight = 300,
                    IsEnabled = smoothMotionEnabled,
                    Opacity = smoothMotionEnabled ? 1.0 : 0.4,
                };
                ToolTipService.SetToolTip(combo, "플립 페이싱 — 꺼짐: 낮은 지연 우선. 켜짐: 부드러운 프레임 페이싱 우선. 전체 화면과 창 모드를 함께 설정합니다.");
                var init = true;
                combo.SelectionChanged += (s, ev) =>
                {
                    if (init) return;
                    int i = combo.SelectedIndex;
                    if (i < 0 || i >= options.Length) return;
                    var fsValue = options[i].Value;
                    // Also set windowed pacing to the same value (use 0x00000001 for "On" instead of 0xFFFFFFFF)
                    var winValue = fsValue == 0xFFFFFFFF ? 0x00000001u : fsValue;
                    _ = Task.Run(() =>
                    {
                        nvidiaPresetService.SetSmoothMotionFlipPacingFs(capturedName, installPathSafe, fsValue);
                        nvidiaPresetService.SetSmoothMotionFlipPacingWin(capturedName, installPathSafe, winValue);
                    });
                };
                smoothCol.Children.Add(combo);
                init = false;
            }

            Grid.SetColumn(smoothCol, 4);
            nvidiaGrid.Children.Add(smoothCol);
            nvidiaGrid.Children.Add(MakeDlssDivider(5));
            _window.ViewModel.SetLastUiAction($"BuildDriverProfileSectionWithData:SmoothMotion done({capturedName})");

            // ── Column 6: Other (Power, G-Sync, FPS Limit) ──
            var powerCol = new StackPanel { Spacing = 4 };
            var powerLabel = new TextBlock { Text = "기타", FontSize = 11, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) };
            ToolTipService.SetToolTip(powerLabel, "이 게임의 전원 관리, G-Sync 제어, 드라이버 프레임 속도 제한입니다.");
            powerCol.Children.Add(powerLabel);

            // Power Management Mode
            {
                powerCol.Children.Add(new TextBlock { Text = "전원 모드", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var options = DlssPresetService.PowerManagementOptions;
                uint current = d.PowerManagementMode;
                var items = options.Select(o => o.Name).ToArray();
                int idx = Array.FindIndex(options, o => o.Value == current);
                if (idx < 0) idx = 0;
                var combo = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = idx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    MaxDropDownHeight = 300,
                };
                ToolTipService.SetToolTip(combo, "전원 관리 — 적응형: 유휴 시 GPU 클럭을 낮춥니다. 최대: GPU를 최고 클럭에 고정합니다. 최적: 균형 (NVIDIA 권장).");
                var init = true;
                combo.SelectionChanged += (s, ev) =>
                {
                    if (init) return;
                    int i = combo.SelectedIndex;
                    if (i < 0 || i >= options.Length) return;
                    var valueToWrite = options[i].Value;
                    _ = Task.Run(() => nvidiaPresetService.SetPowerManagementMode(capturedName, installPathSafe, valueToWrite));
                };
                powerCol.Children.Add(combo);
                init = false;
            }

            // G-Sync per-game toggle
            {
                powerCol.Children.Add(new TextBlock { Text = "G-Sync", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                bool gsyncEnabled = d.PerGameGSyncEnabled;
                var gsyncCombo = new ComboBox
                {
                    ItemsSource = new[] { "켜짐", "꺼짐" },
                    SelectedIndex = gsyncEnabled ? 0 : 1,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    MaxDropDownHeight = 300,
                };
                ToolTipService.SetToolTip(gsyncCombo, "게임별 G-Sync 제어. 꺼짐으로 설정하면 전역 설정과 관계없이 이 게임에서 G-Sync를 끕니다.");
                var gsyncInit = true;
                gsyncCombo.SelectionChanged += (s, ev) =>
                {
                    if (gsyncInit) return;
                    bool enabled = gsyncCombo.SelectedIndex == 0;
                    _ = Task.Run(() => nvidiaPresetService.SetPerGameGSyncEnabled(capturedName, installPathSafe, enabled));
                    if (_driverSummaryData != null)
                    {
                        _driverSummaryData = _driverSummaryData with { PerGameGSyncEnabled = enabled };
                        UpdateDriverSummary();
                    }
                };
                powerCol.Children.Add(gsyncCombo);
                gsyncInit = false;
            }

            // Per-game FPS limit (FPS Limiter V3). "Global" removes the game's own value so it follows the Settings page limit.
            {
                powerCol.Children.Add(new TextBlock { Text = "FPS 제한", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });
                var presets = DlssPresetService.FpsLimiterPresets;
                var globalFps = d.GlobalFpsLimit;
                var globalItem = globalFps == 0 ? "Global (Off)" : $"Global ({globalFps} FPS)";

                // Builds the item list; a custom per-game value gets its own entry just above "Custom..."
                (string[] Items, int Index) BuildFpsItems(uint? perGame)
                {
                    var list = new List<string> { globalItem };
                    list.AddRange(presets.Select(o => o.Name));
                    if (perGame == null) return (list.ToArray(), 0);
                    int presetIdx = Array.FindIndex(presets, o => o.Value == perGame.Value && o.Value != uint.MaxValue);
                    if (presetIdx >= 0) return (list.ToArray(), presetIdx + 1);
                    list.Insert(list.Count - 1, $"{perGame.Value} FPS (Custom)");
                    return (list.ToArray(), list.Count - 2);
                }

                uint? currentFps = d.PerGameFpsLimit;
                var (fpsItems, fpsIdx) = BuildFpsItems(currentFps);
                var fpsCombo = new ComboBox
                {
                    ItemsSource = fpsItems,
                    SelectedIndex = fpsIdx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    MaxDropDownHeight = 300,
                };
                bool hasFrameLimiter = card.UlStatus is GameStatus.Installed or GameStatus.UpdateAvailable
                                    || card.DcStatus is GameStatus.Installed or GameStatus.UpdateAvailable;
                var fpsTip = "이 게임에만 적용되는 NVIDIA 드라이버 프레임 속도 제한입니다. Global은 설정 페이지에서 지정한 제한을 따릅니다. 꺼짐은 전역 제한이 설정되어 있어도 이 게임의 드라이버 제한을 끕니다.";
                if (hasFrameLimiter)
                    fpsTip += "\n\n이 게임에 ReLimiter 또는 Display Commander가 설치되어 있어 RHI가 드라이버 제한을 꺼짐으로 설정했습니다. 두 제한기를 동시에 사용하면 끊김이 생길 수 있습니다.";
                ToolTipService.SetToolTip(fpsCombo, fpsTip);

                var fpsInit = true;
                fpsCombo.SelectionChanged += async (s, ev) =>
                {
                    if (fpsInit) return;
                    try
                    {
                        var items = (string[])fpsCombo.ItemsSource;
                        int i = fpsCombo.SelectedIndex;
                        if (i < 0 || i >= items.Length) return;
                        var selected = items[i];

                        if (i == 0)
                        {
                            currentFps = null;
                            _ = Task.Run(() => nvidiaPresetService.ClearPerGameFpsLimit(capturedName, installPathSafe));
                            return;
                        }
                        if (selected.EndsWith("(Custom)", StringComparison.Ordinal))
                            return; // re-selected the existing custom value

                        int presetIdx = Array.FindIndex(presets, o => o.Name == selected);
                        if (presetIdx < 0) return;
                        if (presets[presetIdx].Value != uint.MaxValue)
                        {
                            var value = presets[presetIdx].Value;
                            currentFps = value;
                            _ = Task.Run(() => nvidiaPresetService.SetPerGameFpsLimit(capturedName, installPathSafe, value));
                            return;
                        }

                        // "Custom..." — ask for a value
                        var textBox = new TextBox { PlaceholderText = "20-1000", FontSize = 13 };
                        var dialog = new ContentDialog
                        {
                            Title = "사용자 지정 FPS 제한",
                            Content = textBox,
                            PrimaryButtonText = "설정",
                            CloseButtonText = "취소",
                            XamlRoot = fpsCombo.XamlRoot ?? _window.Content.XamlRoot,
                            RequestedTheme = ElementTheme.Dark,
                        };
                        var result = await DialogService.ShowSafeAsync(dialog);
                        uint? newFps = null;
                        if (result == ContentDialogResult.Primary && uint.TryParse(textBox.Text.Trim(), out var fps) && fps >= 20 && fps <= 1000)
                            newFps = fps;

                        if (newFps is uint chosen)
                        {
                            currentFps = chosen;
                            _ = Task.Run(() => nvidiaPresetService.SetPerGameFpsLimit(capturedName, installPathSafe, chosen));
                        }
                        // Rebuild the list (adds the custom entry, or reverts the selection on cancel)
                        var (newItems, newIdx) = BuildFpsItems(currentFps);
                        fpsInit = true;
                        fpsCombo.ItemsSource = newItems;
                        fpsCombo.SelectedIndex = newIdx;
                        fpsInit = false;
                    }
                    catch (Exception ex)
                    {
                        fpsInit = false;
                        CrashReporter.Log($"[DetailPanelBuilder.FpsLimitCombo] Error for '{capturedName}' — {ex.Message}");
                    }
                };
                powerCol.Children.Add(fpsCombo);
                fpsInit = false;
            }

            // Restore Profile Defaults button — sits in the footer row under this column
            var restoreProfileBtn = new Button
            {
                Content = "기본값 복원",
                FontSize = 11,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = UIFactory.Brush(ResourceKeys.AccentBlueBgBrush),
                Foreground = UIFactory.Brush(ResourceKeys.AccentBlueBrush),
                BorderBrush = UIFactory.Brush(ResourceKeys.AccentBlueBorderBrush),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                IsEnabled = nvidiaPresetService.IsSupported,
            };
            ToolTipService.SetToolTip(restoreProfileBtn,
                "이 게임의 NVIDIA 드라이버 프로필을 공장 기본값으로 복원합니다. 모든 사용자 지정 설정(프리셋, 렌더 배율, MFG, 드라이버 재정의)이 제거됩니다. 이 작업은 되돌릴 수 없습니다.");
            restoreProfileBtn.Click += async (s, ev) =>
            {
                var xamlRoot = (s as FrameworkElement)?.XamlRoot ?? _window.Content.XamlRoot;
                var warningDialog = new ContentDialog
                {
                    Title = "드라이버 설정을 복원할까요?",
                    Content = new TextBlock
                    {
                        Text = $"{capturedName}의 드라이버 설정을 공장 기본값으로 복원하고 DLSS/Streamline DLL을 원래 버전으로 되돌립니다. 이 작업은 되돌릴 수 없습니다.",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13,
                    },
                    PrimaryButtonText = "복원",
                    CloseButtonText = "취소",
                    XamlRoot = xamlRoot,
                    RequestedTheme = ElementTheme.Dark,
                };

                var result = await DialogService.ShowSafeAsync(warningDialog);
                if (result != ContentDialogResult.Primary) return;

                var success = nvidiaPresetService.RestoreProfileDefaults(capturedName, installPathSafe);
                if (success)
                {
                    var refreshCard = _window.ViewModel.AllCards.FirstOrDefault(c =>
                        c.GameName.Equals(capturedName, StringComparison.OrdinalIgnoreCase));
                    if (refreshCard != null)
                    {
                        // Also restore DLSS/Streamline DLLs to originals
                        if (refreshCard.DlssDetection != null)
                        {
                            var dlssSvc = _dlssStreamlineService;
                            dlssSvc.RestoreAll(refreshCard.DlssDetection);
                            refreshCard.RefreshDlssVersions(dlssSvc);
                        }
                        _window.DispatcherQueue?.TryEnqueue(() => BuildOverridesPanel(refreshCard));
                    }
                }
            };

            Grid.SetColumn(powerCol, 6);
            nvidiaGrid.Children.Add(powerCol);
            _window.ViewModel.SetLastUiAction($"BuildDriverProfileSectionWithData:Power done({capturedName})");

            // ── Column 8: ReBAR ──
            var rebarCol = new StackPanel { Spacing = 4 };
            var rebarLabel = new TextBlock { Text = "ReBAR", FontSize = 11, Foreground = UIFactory.Brush(ResourceKeys.TextPrimaryBrush) };
            ToolTipService.SetToolTip(rebarLabel, "Resizable BAR — CPU가 GPU VRAM 전체에 한 번에 접근할 수 있게 합니다. 일부 게임에서 성능이 5-10% 향상될 수 있습니다. RTX 30 이상 및 BIOS 지원 필요.");
            rebarCol.Children.Add(rebarLabel);

            bool rebarEnabled = false; // set inside Enable block below
            ulong rebarSizeLimit = d.ReBarSizeLimit;

            // Enable — Auto (Default) / Off / On using new 0x000BFA21 setting
            {
                rebarCol.Children.Add(new TextBlock { Text = "사용", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) });

                uint rebarEnableMode = d.ReBarEnableMode;
                // 0=Off→index 1, 1=Auto→index 0, 2=On→index 2
                int enableIdx = rebarEnableMode == 0 ? 1 : rebarEnableMode == 2 ? 2 : 0;

                var rebarEnableCombo = new ComboBox
                {
                    ItemsSource = new[] { "자동 (기본값)", "꺼짐", "켜짐" },
                    SelectedIndex = enableIdx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    MaxDropDownHeight = 300,
                };
                ToolTipService.SetToolTip(rebarEnableCombo, "자동 = 드라이버가 결정. 켜짐 = ReBAR 강제 사용. 꺼짐 = ReBAR 강제 사용 안 함.");
                var rebarComboInit = true;
                rebarEnableCombo.SelectionChanged += (s, ev) =>
                {
                    if (rebarComboInit) return;
                    // index 0=Auto(1), 1=Off(0), 2=On(2)
                    uint mode = rebarEnableCombo.SelectedIndex switch { 1 => 0u, 2 => 2u, _ => 1u };
                    _ = Task.Run(() => nvidiaPresetService.SetReBarEnableMode(capturedName, installPathSafe, mode));
                    // Rebuild only the driver profile section — ReBAR Enable affects whether the
                    // ReBAR Size combo is enabled. Rebuilding the full panel is unnecessary.
                    _window.DispatcherQueue?.TryEnqueue(() => BuildDriverProfileSection(card, card.GameName));
                };
                rebarCol.Children.Add(rebarEnableCombo);
                rebarComboInit = false;

                // Derive enabled state for Mode/Size combos: only On enables them, Auto and Off grey them
                rebarEnabled = rebarEnableMode == 2;
            }

            // Mode
            {
                var rebarModeLabel = new TextBlock { Text = "모드", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) };
                if (!rebarEnabled)
                    ToolTipService.SetToolTip(rebarModeLabel, "변경하려면 ReBAR 사용을 켜짐으로 설정하세요.");
                rebarCol.Children.Add(rebarModeLabel);
                uint rebarMode = d.ReBarMode;
                var modeItems = DlssPresetService.ReBarModes.Select(m => m.Name).ToList();

                // Select current effective mode: per-game value, or default to Standard (index 0)
                int modeIdx = Array.FindIndex(DlssPresetService.ReBarModes, m => m.Value == rebarMode);
                if (modeIdx < 0) modeIdx = 0;

                var rebarModeCombo = new ComboBox
                {
                    ItemsSource = modeItems,
                    SelectedIndex = modeIdx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    MaxDropDownHeight = 300,
                    IsEnabled = rebarEnabled,
                    Opacity = rebarEnabled ? 1.0 : 0.4,
                };
                ToolTipService.SetToolTip(rebarModeCombo, "표준 = 보수적. 최적화 = 적극적인 드라이버 스케줄링 (NVIDIA 화이트리스트 게임에서 사용).");
                var modeComboInit = true;
                rebarModeCombo.SelectionChanged += (s, ev) =>
                {
                    if (modeComboInit) return;
                    int idx = rebarModeCombo.SelectedIndex;
                    if (idx < 0) return;
                    uint newMode = DlssPresetService.ReBarModes[idx].Value;
                    _ = Task.Run(() => nvidiaPresetService.SetReBarMode(capturedName, installPathSafe, newMode));
                };
                rebarCol.Children.Add(rebarModeCombo);
                modeComboInit = false;
            }

            // Size Limit — always shows actual size values (no Global option)
            {
                var rebarSizeLabel = new TextBlock { Text = "크기 제한", FontSize = 10, Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush), Margin = new Thickness(0, 2, 0, 0) };
                if (!rebarEnabled)
                    ToolTipService.SetToolTip(rebarSizeLabel, "변경하려면 ReBAR 사용을 켜짐으로 설정하세요.");
                rebarCol.Children.Add(rebarSizeLabel);

                var sizeItems = new List<string>();
                var sizeValues = new List<ulong>();
                foreach (var sl in DlssPresetService.ReBarSizeLimits)
                {
                    sizeItems.Add(sl.Name);
                    sizeValues.Add(sl.Value);
                }

                // Select the current effective size: per-game override, or global, or 1GB default
                ulong globalSize = d.GlobalReBarSizeLimit;
                ulong effectiveSize = rebarSizeLimit != 0 ? rebarSizeLimit : (globalSize != 0 ? globalSize : 0x0000000040000000);
                int sizeIdx;
                var matchIdx = sizeValues.IndexOf(effectiveSize);
                sizeIdx = matchIdx >= 0 ? matchIdx : 1; // Default: 1GB (index 1)

                var rebarSizeCombo = new ComboBox
                {
                    ItemsSource = sizeItems,
                    SelectedIndex = sizeIdx,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(6),
                    MaxDropDownHeight = 300,
                    IsEnabled = rebarEnabled,
                    Opacity = rebarEnabled ? 1.0 : 0.4,
                };
                ToolTipService.SetToolTip(rebarSizeCombo, "대부분의 게임에서는 1GB가 최적입니다. ReBAR 관련 끊김이 있으면 512MB로 낮추세요.");
                var sizeComboInit = true;
                rebarSizeCombo.SelectionChanged += (s, ev) =>
                {
                    if (sizeComboInit) return;
                    int idx = rebarSizeCombo.SelectedIndex;
                    if (idx < 0) return;
                    ulong newSize = sizeValues[idx];
                    _ = Task.Run(() => nvidiaPresetService.SetReBarSizeLimit(capturedName, installPathSafe, newSize));
                };
                rebarCol.Children.Add(rebarSizeCombo);
                sizeComboInit = false;
            }

            Grid.SetColumn(rebarCol, 2);
            nvidiaGrid.Children.Add(rebarCol);
            nvidiaGrid.Children.Add(MakeDlssDivider(3));
            _window.ViewModel.SetLastUiAction($"BuildDriverProfileSectionWithData:ReBAR done({capturedName})");

            // Defer adding the fully-built driver grid to the visual tree so WinUI can
            // finish any queued layout work from the DLSS columns above before committing
            // another large grid. Prevents the UI freeze seen on games with all five
            // DLSS components (S.T.A.L.K.E.R. 2 and similar).
            var targetPanel = nvBody ?? _window.NvidiaProfileDriverPanel;
            var isElevatedCapture = d.IsAdmin;
            restoreProfileBtn.IsEnabled = isElevatedCapture && nvidiaPresetService.IsSupported;
            restoreProfileBtn.Opacity = isElevatedCapture ? 1.0 : 0.4;
            // Footer row: admin notice on the left, Restore Defaults under the Other column (fixed widths, see above)
            double gridW = ColCount * colW + overhead;
            var footerGrid = new Grid { ColumnSpacing = ColSpacing, Margin = new Thickness(0, 8, 0, 0) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, gridW - colW - ColSpacing)) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colW) });
            Grid.SetColumn(restoreProfileBtn, 1);
            footerGrid.Children.Add(restoreProfileBtn);
            _window.ViewModel.SetLastUiAction($"BuildDriverProfileSectionWithData:AddToTree({capturedName})");
            var enqueued = _window.DispatcherQueue?.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () =>
                {
                    // Guard: user navigated away between the outer callback and this deferred grid addition.
                    if (_window.ViewModel.SelectedGame != card) return;
                    _window.ViewModel.SetLastUiAction($"BuildDriverProfileSectionWithData:AddingGrid({capturedName})");
                    // Footer (admin notice + Restore Defaults) goes after the grid so it stays at the bottom
                    footerGrid.Children.Add(MakeDriverFooterText(isElevatedCapture, new Thickness(0)));
                    if (Layout.IsTabs)
                    {
                        // Tabs (mockup A): the same controls as one card, arranged before it's shown
                        targetPanel.Children.Add(Layout.ArrangeDriver(nvidiaGrid, footerGrid, _dlssPresetService.DriverVersionString));
                    }
                    else
                    {
                        targetPanel.Children.Add(nvidiaGrid);
                        _window.ViewModel.SetLastUiAction($"BuildDriverProfileSectionWithData:AddingNotice({capturedName})");
                        targetPanel.Children.Add(footerGrid);
                    }
                    _window.ViewModel.SetLastUiAction($"BuildDriverProfileSectionWithData:Done({capturedName})");
                });
            CrashReporter.Log($"[BuildDriverProfileSectionWithData] TryEnqueue(Low) for grid add returned {enqueued?.ToString() ?? "null (no dispatcher)"} for '{capturedName}'");
        }
        // Admin notice is now added inside the deferred TryEnqueue above when nvidiaPresetService.IsSupported.
        // When not supported we still need to add it immediately.
        if (!nvidiaPresetService.IsSupported)
        {
            bool isElevated = d.IsAdmin;
            (nvBody ?? _window.NvidiaProfileDriverPanel).Children.Add(MakeDriverFooterText(isElevated, new Thickness(0, 8, 0, 0)));
        }
    }
}

// v2.7.7 fix: cancel Task.Delay on NVAPI completion to prevent thread pool starvation
