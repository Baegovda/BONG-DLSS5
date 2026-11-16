namespace RenoDXCommander.Services;

/// <summary>
/// Thrown when a DLSS / Streamline DLL can't be written into a game folder.
/// <see cref="Exception.Message"/> is a plain-English explanation that can be shown
/// to the user as-is; the original exception is kept in <see cref="Exception.InnerException"/>.
/// </summary>
public sealed class DlssSwapException : Exception
{
    public string TargetPath { get; }

    private DlssSwapException(string targetPath, string message, Exception inner)
        : base(message, inner)
    {
        TargetPath = targetPath;
    }

    /// <summary>Builds a user-facing exception from a file-system error.</summary>
    public static DlssSwapException From(string targetPath, Exception inner)
        => new(targetPath, Describe(targetPath, inner), inner);

    internal static bool IsWindowsAppsPath(string path)
        => path.Replace('/', '\\').Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase);

    internal static string Describe(string targetPath, Exception inner)
    {
        bool windowsApps = IsWindowsAppsPath(targetPath);
        return inner switch
        {
            DirectoryNotFoundException when windowsApps =>
                "이 Game Pass 게임의 폴더가 변경되었습니다. 게임이 업데이트되었을 가능성이 높습니다. " +
                "BONG DLSS5에서 새로 고침을 클릭한 뒤 다시 시도하세요.",
            DirectoryNotFoundException =>
                "게임 폴더가 더 이상 이 위치에 없습니다. 게임이 이동, 업데이트 또는 제거되었을 수 있습니다. " +
                "BONG DLSS5에서 새로 고침을 클릭한 뒤 다시 시도하세요.",
            UnauthorizedAccessException when windowsApps =>
                "Windows는 보호된 WindowsApps 폴더에 설치된 Game Pass 게임의 변경을 차단하므로, " +
                "BONG DLSS5가 이 게임의 DLSS 파일을 변경할 수 없습니다.",
            UnauthorizedAccessException =>
                "Windows가 게임 폴더에 대한 액세스를 거부했습니다. BONG DLSS5를 관리자로 실행해 보세요.",
            IOException =>
                "DLSS 파일이 사용 중입니다. 게임을 닫고 다시 시도하세요.",
            _ => inner.Message,
        };
    }
}
