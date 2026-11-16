// MainWindow.AntivirusNotice.cs — tells the user when an antivirus deleted a file RHI had just
// downloaded (see Services/AntivirusGuard.cs). Shown once per component per session.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private readonly HashSet<string> _antivirusNoticeShown = new(StringComparer.OrdinalIgnoreCase);

    private void HookAntivirusNotice()
    {
        AntivirusGuard.Blocked += (component, path) =>
            DispatcherQueue?.TryEnqueue(() => _ = ShowAntivirusNoticeAsync(component, path));
    }

    private async Task ShowAntivirusNoticeAsync(string component, string path)
    {
        try
        {
            if (IsShuttingDown || !_antivirusNoticeShown.Add(component)) return;
            ShowStatusBarMessage($"RHI가 다운로드한 {component}을(를) 백신 프로그램이 제거했습니다.", TimeSpan.FromSeconds(15));
            if (Content?.XamlRoot is not { } root) return;

            var folder = Path.GetDirectoryName(path) ?? path;
            var dialog = new ContentDialog
            {
                Title = "백신 프로그램이 다운로드를 제거함",
                Content =
                    $"RHI가 {component}을(를) 다운로드한 직후 백신 프로그램이 삭제했습니다:\n{path}\n\n"
                    + "이런 모드는 게임이 DLL을 불러오는 방식을 바꾸기 때문에, 안전하더라도 백신 프로그램이 자주 탐지합니다 "
                    + "(오탐). RHI는 자동으로 다시 다운로드하지 않습니다.\n\n"
                    + "사용하려면 백신 프로그램에서 %LocalAppData%\\RHI를 예외로 추가하고(Windows 보안: 바이러스 및 위협 "
                    + "방지 > 설정 관리 > 제외), 백신이 파일을 격리했다면 복원한 다음 RHI에서 다시 설치하세요.",
                PrimaryButtonText = "폴더 열기",
                CloseButtonText = "확인",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = root,
                RequestedTheme = ElementTheme.Dark,
            };
            if (await DialogService.ShowSafeAsync(dialog) == ContentDialogResult.Primary)
            {
                var open = Directory.Exists(folder) ? folder
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RHI");
                System.Diagnostics.Process.Start("explorer.exe", $"\"{open}\"");
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.ShowAntivirusNoticeAsync] {ex.Message}");
        }
    }
}
