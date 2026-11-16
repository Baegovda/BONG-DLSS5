// SettingsHandler.NvAppLimits.cs -- "NVIDIA App" block in Driver Settings › Profiles: one button
// that stops the NVIDIA App resetting DLSS overrides (NvAppOverrideLimitService). Built in C#
// (XAML constraint), added once when Settings first opens.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private bool _nvAppLimitsSetupDone;
    private Button? _nvAppLimitsButton;

    private void EnsureNvAppLimitsPanel()
    {
        if (_nvAppLimitsSetupDone) return;
        _nvAppLimitsSetupDone = true;
        try
        {
            // The Profiles card's left column holds the WrapPanel with "Create missing profiles"
            var column = FindColumnOfButton(_window.SettingsCardsPanel, "Create missing profiles");
            if (column == null)
            {
                CrashReporter.Log("[SettingsHandler.EnsureNvAppLimitsPanel] Profiles column not found — NVIDIA App block not added");
                return;
            }

            column.Children.Add(new TextBlock
            {
                Text = "NVIDIA App",
                Style = FindStyle("SettingsHeadStyle"),
                Margin = new Thickness(0, 6, 0, 0),
            });
            column.Children.Add(new TextBlock
            {
                Text = "NVIDIA App이 지원하지 않는 게임의 DLSS 프리셋과 버전을 초기화하지 못하게 합니다. 드라이버 또는 NVIDIA App 업데이트 후 다시 실행하세요.",
                Style = FindStyle("SettingsHelpStyle"),
                TextWrapping = TextWrapping.Wrap,
            });

            _nvAppLimitsButton = new Button
            {
                Content = "NVIDIA App 재정의 제한 제거...",
                Style = FindStyle("SettingsButtonStyle"),
                Foreground = UIFactory.Brush(ResourceKeys.SettingsLabelBrush),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            ToolTipService.SetToolTip(_nvAppLimitsButton,
                "NVIDIA App은 NVIDIA가 승인한 게임에서만 DLSS 재정의를 허용하고, 다른 게임에서 RHI나 " +
                "NVIDIA Profile Inspector로 설정한 재정의를 되돌립니다. 이 기능은 NVIDIA App 설정 파일에서 해당 제한을 끄고 " +
                "(백업 유지) NVIDIA 서비스를 다시 시작합니다. JPersson77의 nVAppAppApp 스크립트를 기반으로 합니다.");
            _nvAppLimitsButton.Click += NvAppLimitsButton_Click;
            column.Children.Add(_nvAppLimitsButton);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.EnsureNvAppLimitsPanel] Failed — {ex.Message}");
        }
    }

    private async void NvAppLimitsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var xamlRoot = (sender as FrameworkElement)?.XamlRoot ?? _window.Content.XamlRoot;
            if (!NvAppOverrideLimitService.IsNvidiaAppInstalled)
            {
                await ShowNvAppLimitsMessageAsync(xamlRoot, "NVIDIA App을 찾을 수 없음",
                    "이 Windows 사용자에게 NVIDIA App이 설치되어 있지 않습니다. NVIDIA App이 없으면 DLSS 재정의를 초기화하는 것도 없으므로 변경할 내용이 없습니다.");
                return;
            }

            var confirm = new ContentDialog
            {
                Title = "NVIDIA App 재정의 제한을 제거할까요?",
                Content = new TextBlock
                {
                    Text = "RHI가 NVIDIA App 설정 파일을 변경하여 NVIDIA가 승인하지 않은 게임에서 DLSS 재정의를 되돌리지 않도록 합니다. 파일 백업은 유지됩니다.\n\n" +
                           "그런 다음 RHI가 NVIDIA 서비스 두 개를 다시 시작합니다. Windows에서 관리자 권한을 요청하며, 화면이 1~2초 정도 검게 변할 수 있습니다.\n\n" +
                           "먼저 실행 중인 게임을 모두 닫으세요. 드라이버 및 NVIDIA App 업데이트로 제한이 다시 적용될 수 있으므로 업데이트 후 다시 실행하세요.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                },
                PrimaryButtonText = "제한 제거",
                CloseButtonText = "취소",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = xamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            if (await DialogService.ShowSafeAsync(confirm) != ContentDialogResult.Primary) return;

            if (_nvAppLimitsButton != null) { _nvAppLimitsButton.IsEnabled = false; _nvAppLimitsButton.Content = "제거 중..."; }
            NvAppOverrideLimitService.Result result;
            try
            {
                result = await Task.Run(NvAppOverrideLimitService.RemoveLimits);
            }
            finally
            {
                if (_nvAppLimitsButton != null) { _nvAppLimitsButton.IsEnabled = true; _nvAppLimitsButton.Content = "NVIDIA App 재정의 제한 제거..."; }
            }
            CrashReporter.Log($"[SettingsHandler.NvAppLimitsButton_Click] Success={result.Success}: {result.Message}");
            if (_window.IsShuttingDown) return;
            await ShowNvAppLimitsMessageAsync(xamlRoot,
                result.Success ? "NVIDIA App 제한 제거됨" : "NVIDIA App 제한", result.Message);
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.NvAppLimitsButton_Click] {ex.Message}");
        }
    }

    private static async Task ShowNvAppLimitsMessageAsync(XamlRoot? xamlRoot, string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 13, IsTextSelectionEnabled = true },
            CloseButtonText = "확인",
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };
        await DialogService.ShowSafeAsync(dialog);
    }

    private static Style? FindStyle(string key)
        => Application.Current.Resources.TryGetValue(key, out var s) ? s as Style : null;

    /// <summary>Finds the Panel that contains the Panel holding a Button with this text.</summary>
    private static Panel? FindColumnOfButton(DependencyObject? root, string buttonText)
    {
        Panel? found = null;
        bool Walk(DependencyObject? node, Panel? grandparent, Panel? parent)
        {
            switch (node)
            {
                case Button b when b.Content is string s && string.Equals(s, buttonText, StringComparison.Ordinal):
                    found = grandparent;
                    return true;
                case Panel p:
                    foreach (var c in p.Children) if (Walk(c, parent, p)) return true;
                    return false;
                case Border bd: return Walk(bd.Child, grandparent, parent);
                case ContentControl cc when cc.Content is DependencyObject d: return Walk(d, grandparent, parent);
            }
            return false;
        }
        Walk(root, null, null);
        return found;
    }
}
