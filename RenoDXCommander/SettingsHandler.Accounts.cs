using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    // ── Nexus Mods (dev-only) ──────────────────────────────────────────────────

    /// <summary>
    /// Refreshes the Nexus status text and button states based on current settings.
    /// Called on settings page open and after connect/disconnect.
    /// </summary>
    public void RefreshNexusStatus()
    {
        var key      = ViewModel.Settings.NexusApiKey;
        var username = ViewModel.Settings.NexusUsername;
        var isPremium = ViewModel.Settings.NexusIsPremium;

        bool connected = !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(username);

        if (connected)
        {
            var tier = isPremium ? "프리미엄" : "무료";
            _window.NexusStatusText.Text = $"{username}(으)로 연결됨 ({tier})";
            _window.NexusStatusText.Foreground = UIFactory.Brush(
                isPremium ? ResourceKeys.AccentGreenBrush : ResourceKeys.TextSecondaryBrush);
            _window.NexusDisconnectRow.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            _window.NexusConnectBtn.Content = "다시 연결";
        }
        else
        {
            _window.NexusStatusText.Text = "연결 안 됨";
            _window.NexusStatusText.Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush);
            _window.NexusDisconnectRow.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            _window.NexusConnectBtn.Content = "연결";
        }

        // NXM handler status
        bool nxmRegistered = NxmProtocolHandler.IsRegistered();
        _window.NxmStatusText.Text = nxmRegistered ? "BONG DLSS5가 활성 nxm:// 처리기입니다" : "등록 안 됨";
        _window.NxmStatusText.Foreground = UIFactory.Brush(
            nxmRegistered ? ResourceKeys.AccentGreenBrush : ResourceKeys.TextSecondaryBrush);
        _window.NxmRegisterBtn.Content = nxmRegistered ? "등록 해제" : "등록";
    }

    public async void NexusConnectBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var nexusDl  = App.Services.GetRequiredService<NexusDownloadService>();
            var nexusSso = App.Services.GetRequiredService<NexusSsoService>();

            _window.NexusConnectBtn.IsEnabled = false;
            _window.NexusStatusText.Text = "인증을 위해 브라우저를 여는 중...";
            _window.NexusStatusText.Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush);

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(120));
            string? receivedKey = null;

            nexusSso.BrowserUrlReady += url =>
            {
                // Open the Nexus SSO page in the default browser
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
                catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.NexusConnectBtn_Click] Failed to open browser — {ex.Message}"); }

                _window.DispatcherQueue?.TryEnqueue(() =>
                    _window.NexusStatusText.Text = "브라우저에서 인증을 기다리는 중...");
            };

            try
            {
                receivedKey = await nexusSso.AuthoriseAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) { CrashReporter.Log($"[SettingsHandler.NexusConnectBtn_Click] Nexus SSO cancelled or timed out — {ex.Message}"); }

            // Validate the key on a background thread, then marshal ALL UI updates to the dispatcher
            _ = Task.Run(async () =>
            {
                NexusUserInfo? info = null;
                if (!string.IsNullOrEmpty(receivedKey))
                {
                    try { info = await nexusDl.ValidateApiKeyAsync(receivedKey); }
                    catch (Exception ex) { CrashReporter.Log($"[SettingsHandler.NexusConnectBtn_Click] Validate failed — {ex.Message}"); }
                }

                _window.DispatcherQueue?.TryEnqueue(() =>
                {
                    _window.NexusConnectBtn.IsEnabled = true;

                    if (string.IsNullOrEmpty(receivedKey))
                    {
                        _window.NexusStatusText.Text = "인증 시간이 초과되었거나 취소되었습니다.";
                        _window.NexusStatusText.Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush);
                        return;
                    }

                    if (info == null)
                    {
                        _window.NexusStatusText.Text = "받은 키가 올바르지 않습니다. 다시 시도하세요.";
                        _window.NexusStatusText.Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush);
                        return;
                    }

                    ViewModel.Settings.NexusApiKey    = receivedKey;
                    ViewModel.Settings.NexusIsPremium = info.IsPremium;
                    ViewModel.Settings.NexusUsername  = info.Name;
                    ViewModel.SaveSettingsPublic();

                    RefreshNexusStatus();
                    CrashReporter.Log($"[SettingsHandler.NexusConnectBtn_Click] Connected via SSO as {info.Name} (Premium={info.IsPremium})");
                });
            });
        }
        catch (Exception unhandledEx)
        {
            CrashReporter.Log($"[SettingsHandler.NexusConnectBtn_Click] Unhandled error — {unhandledEx.Message}\n{unhandledEx.StackTrace}");
        }
    }

    public void NexusDisconnectBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.Settings.NexusApiKey    = "";
        ViewModel.Settings.NexusIsPremium = false;
        ViewModel.Settings.NexusUsername  = "";
        ViewModel.SaveSettingsPublic();
        RefreshNexusStatus();
        CrashReporter.Log("[SettingsHandler.NexusDisconnectBtn_Click] Disconnected from Nexus Mods");
    }

    public async void NxmRegisterBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            bool currentlyRegistered = NxmProtocolHandler.IsRegistered();

            if (currentlyRegistered)
            {
                NxmProtocolHandler.UnregisterProtocolHandler();
                CrashReporter.Log("[SettingsHandler.NxmRegisterBtn_Click] Unregistered nxm:// handler");
            }
            else
            {
                // Warn if another app already owns the handler
                if (NxmProtocolHandler.AnyHandlerRegistered())
                {
                    var result = await DialogService.ShowSafeAsync(new ContentDialog
                    {
                        Title = "NXM 프로토콜 처리기",
                        Content = "다른 애플리케이션(예: Vortex 또는 MO2)이 이미 nxm:// 처리기로 등록되어 있습니다. BONG DLSS5를 등록하면 대체됩니다. 계속하시겠습니까?",
                        PrimaryButtonText = "BONG DLSS5 등록",
                        CloseButtonText = "취소",
                        XamlRoot = _window.Content.XamlRoot,
                        RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Dark,
                    });
                    if (result != ContentDialogResult.Primary) return;
                }

                try
                {
                    NxmProtocolHandler.RegisterProtocolHandler();
                    CrashReporter.Log("[SettingsHandler.NxmRegisterBtn_Click] Registered nxm:// handler");
                }
                catch (Exception ex)
                {
                    await DialogService.ShowSafeAsync(new ContentDialog
                    {
                        Title = "NXM 등록 실패",
                        Content = $"nxm:// 처리기를 등록할 수 없습니다: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = _window.Content.XamlRoot,
                        RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Dark,
                    });
                    return;
                }
            }

            RefreshNexusStatus();
        }
        catch (Exception unhandledEx)
        {
            CrashReporter.Log($"[SettingsHandler.NxmRegisterBtn_Click] Unhandled error — {unhandledEx.Message}\n{unhandledEx.StackTrace}");
        }
    }

    // ── GitHub OAuth ──────────────────────────────────────────────────────────

    public void RefreshGitHubStatus()
    {
        // If the token was cleared at startup due to 401, make sure the in-memory values are also cleared
        if (App._gitHubTokenExpiredOnStartup)
        {
            ViewModel.Settings.GitHubOAuthToken = "";
            ViewModel.Settings.GitHubUsername   = "";
        }

        var token    = ViewModel.Settings.GitHubOAuthToken;
        var username = ViewModel.Settings.GitHubUsername;
        bool connected = !string.IsNullOrEmpty(token);

        // Check if a PAT file is active
        var patPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RHI", "github_api.txt");
        bool hasPat = System.IO.File.Exists(patPath);

        // Update the PAT status text to show file state
        if (hasPat)
        {
            _window.GitHubPatStatusText.Text = "PAT 파일 사용 중 — 시간당 5,000회 요청 (다시 시작해도 유지)";
            _window.GitHubPatStatusText.Foreground = UIFactory.Brush(ResourceKeys.AccentGreenBrush);
            _window.GitHubPatStatusText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        else
        {
            // Only hide if we haven't just shown a save/clear message
            // (leave it visible if it was just set by a button click)
        }

        if (App._gitHubTokenExpiredOnStartup)
        {
            _window.GitHubStatusText.Text       = "GitHub 로그인이 만료되었습니다. 시간당 5,000회 요청 한도를 복원하려면 아래에 토큰을 추가하세요.";
            _window.GitHubStatusText.Foreground = UIFactory.Brush(ResourceKeys.AccentAmberDimBrush);
            _window.GitHubDisconnectRow.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        }
        else if (connected)
        {
            var display = string.IsNullOrEmpty(username) ? "GitHub" : $"@{username}";
            // Sign-in was removed; existing sessions keep working until signed out
            _window.GitHubStatusText.Text       = $"{display}(으)로 로그인됨 · 시간당 5,000회 요청. 로그인은 더 이상 제공되지 않으니 로그아웃하고 대신 토큰을 추가하세요.";
            _window.GitHubStatusText.Foreground = UIFactory.Brush(ResourceKeys.AccentGreenBrush);
            _window.GitHubDisconnectRow.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        else
        {
            _window.GitHubStatusText.Text       = hasPat ? "PAT 사용 중 · 시간당 5,000회 요청" : "연결 안 됨 · 시간당 60회 요청";
            _window.GitHubStatusText.Foreground = hasPat ? UIFactory.Brush(ResourceKeys.AccentGreenBrush) : UIFactory.Brush(ResourceKeys.TextTertiaryBrush);
            _window.GitHubDisconnectRow.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        }
    }

    public void GitHubDisconnectBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var http = App.Services.GetRequiredService<HttpClient>();

        ViewModel.Settings.GitHubOAuthToken = "";
        ViewModel.Settings.GitHubUsername   = "";
        ViewModel.SaveSettingsPublic();

        // Clear token from DevUnlockService and HttpClient
        // Reset the DevUnlockService cache so it re-reads from disk (may find a file-based token)
        DevUnlockService.ResetTokenCache();
        var fileToken = DevUnlockService.GitHubApiToken; // re-reads github_api.txt / unlock.txt
        GitHubAuthService.ApplyTokenToHttpClient(http, fileToken);

        CrashReporter.Log("[SettingsHandler.GitHubDisconnectBtn_Click] GitHub OAuth token removed");
        RefreshGitHubStatus();
    }

    public async void GitHubPatSaveBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var pat = _window.GitHubPatBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(pat))
            {
                _window.GitHubPatStatusText.Text = "먼저 토큰을 입력하세요.";
                _window.GitHubPatStatusText.Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush);
                _window.GitHubPatStatusText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                return;
            }

            // Basic format check
            if (!pat.StartsWith("ghp_", StringComparison.OrdinalIgnoreCase) &&
                !pat.StartsWith("github_pat_", StringComparison.OrdinalIgnoreCase))
            {
                _window.GitHubPatStatusText.Text = "토큰은 ghp_ 또는 github_pat_로 시작해야 합니다";
                _window.GitHubPatStatusText.Foreground = UIFactory.Brush(ResourceKeys.AccentAmberDimBrush);
                _window.GitHubPatStatusText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }

            _window.GitHubPatSaveBtn.IsEnabled = false;
            _window.GitHubPatSaveBtn.Content = "확인 중...";

            // Validate the token
            bool valid = false;
            string? username = null;
            try
            {
                var http = App.Services.GetRequiredService<HttpClient>();
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://api.github.com/user");
                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {pat}");
                req.Headers.TryAddWithoutValidation("User-Agent", "RHI");
                using var resp = await http.SendAsync(req, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cts.Token);
                valid = resp.IsSuccessStatusCode;
                if (valid)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    doc.RootElement.TryGetProperty("login", out var loginEl);
                    username = loginEl.GetString();
                }
            }
            catch { valid = false; }

            _window.GitHubPatSaveBtn.IsEnabled = true;
            _window.GitHubPatSaveBtn.Content = "저장";

            if (!valid)
            {
                _window.GitHubPatStatusText.Text = "토큰 확인 실패 — 토큰을 확인하고 다시 시도하세요.";
                _window.GitHubPatStatusText.Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush);
                _window.GitHubPatStatusText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                return;
            }

            // Write to github_api.txt
            try
            {
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RHI", "github_api.txt");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                System.IO.File.WriteAllText(path, pat);
            }
            catch (Exception ex)
            {
                _window.GitHubPatStatusText.Text = $"저장 실패: {ex.Message}";
                _window.GitHubPatStatusText.Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush);
                _window.GitHubPatStatusText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                return;
            }

            // Apply immediately
            var http2 = App.Services.GetRequiredService<HttpClient>();
            DevUnlockService.ResetTokenCache();
            DevUnlockService.UpdateToken(pat);
            GitHubAuthService.ApplyTokenToHttpClient(http2, pat);
            ViewModel.RestartPeriodicUpdateCheckTimer(); // checks every hour with a token, 4 h without

            _window.GitHubPatBox.Text = "";
            _window.GitHubPatStatusText.Text = username != null
                ? $"PAT 저장됨 — @{username}(으)로 인증됨"
                : "PAT를 저장하고 적용했습니다.";
            _window.GitHubPatStatusText.Foreground = UIFactory.Brush(ResourceKeys.AccentGreenBrush);
            _window.GitHubPatStatusText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;

            CrashReporter.Log($"[SettingsHandler.GitHubPatSave] PAT saved to github_api.txt (user={username ?? "unknown"})");
            RefreshGitHubStatus();
        }
        catch (Exception unhandledEx)
        {
            CrashReporter.Log($"[SettingsHandler.GitHubPatSaveBtn_Click] Unhandled error — {unhandledEx.Message}\n{unhandledEx.StackTrace}");
        }
    }

    public void GitHubPatClearBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RHI", "github_api.txt");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        catch (Exception ex)
        {
            _window.GitHubPatStatusText.Text = $"지우기 실패: {ex.Message}";
            _window.GitHubPatStatusText.Foreground = UIFactory.Brush(ResourceKeys.AccentRedBrush);
            _window.GitHubPatStatusText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            return;
        }

        // Revert HttpClient to OAuth token if present, else unauthenticated
        DevUnlockService.ResetTokenCache();
        var http = App.Services.GetRequiredService<HttpClient>();
        var oauthToken = ViewModel.Settings.GitHubOAuthToken;
        GitHubAuthService.ApplyTokenToHttpClient(http, string.IsNullOrEmpty(oauthToken) ? null : oauthToken);
        ViewModel.RestartPeriodicUpdateCheckTimer(); // checks every hour with a token, 4 h without

        _window.GitHubPatBox.Text = "";
        _window.GitHubPatStatusText.Text = "PAT를 지웠습니다.";
        _window.GitHubPatStatusText.Foreground = UIFactory.Brush(ResourceKeys.TextTertiaryBrush);
        _window.GitHubPatStatusText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;

        CrashReporter.Log("[SettingsHandler.GitHubPatClear] github_api.txt removed");
        RefreshGitHubStatus();
    }
}
