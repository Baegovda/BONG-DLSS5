// NvAppOverrideLimitService.cs -- removes the NVIDIA App's per-game "no DLSS override" flags so
// the App stops resetting DLSS presets/versions set by RHI (or Profile Inspector) on games NVIDIA
// hasn't approved. Same edits as JPersson77's nVAppAppApp.ps1: flip the Disable_*_Override flags in
// ApplicationStorage.json and every fingerprint.db, lock fingerprint.db read-only, then restart the
// two NVIDIA container services so they reload the files.

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace RenoDXCommander.Services;

public static class NvAppOverrideLimitService
{
    public sealed record Result(bool Success, string Message);

    private static readonly string NvBackendDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NVIDIA Corporation", "NVIDIA app", "NvBackend");

    private static readonly string BackupRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RHI", "NvAppBackup");

    private static readonly string[] ServiceNames = { "NVDisplay.ContainerLocalSystem", "NvContainerLocalSystem" };

    // "Disable_SR_Override":true  →  false   (whitespace tolerant)
    private static readonly Regex JsonFlag = new(
        @"(""Disable_(?:FG|RR|SR|RR_Model|SR_Model)_Override""\s*:\s*)true\b", RegexOptions.Compiled);

    // <Disable_SR_Override>1</Disable_SR_Override>  →  0
    private static readonly Regex XmlFlag = new(
        @"(<Disable_(FG|RR|SR|RR_Model|SR_Model)_Override>)\s*1\s*(</Disable_\2_Override>)", RegexOptions.Compiled);

    public static bool IsNvidiaAppInstalled => Directory.Exists(NvBackendDir);

    /// <summary>Patches the files and restarts the services. Runs file work only; call off the UI thread.</summary>
    public static Result RemoveLimits()
    {
        try
        {
            if (!Directory.Exists(NvBackendDir))
                return new Result(false, "이 Windows 사용자에게 NVIDIA App이 설치되어 있지 않아 변경할 내용이 없습니다.");

            var files = new List<(string Path, bool IsJson)>();
            var json = Path.Combine(NvBackendDir, "ApplicationStorage.json");
            if (File.Exists(json)) files.Add((json, true));
            foreach (var db in Directory.EnumerateFiles(NvBackendDir, "fingerprint.db", SearchOption.AllDirectories))
                files.Add((db, false));
            if (files.Count == 0)
                return new Result(false, "NVIDIA App의 설정 파일을 찾을 수 없습니다. NVIDIA App을 한 번 연 후 다시 시도하세요.");

            string? backupDir = null;
            int changedFlags = 0, changedFiles = 0, skipped = 0;
            foreach (var (path, isJson) in files)
            {
                var bytes = File.ReadAllBytes(path);
                if (Array.IndexOf(bytes, (byte)0) >= 0)
                {
                    // Not a text file (format changed): leave it alone rather than risk corrupting it
                    CrashReporter.Log($"[NvAppOverrideLimitService.RemoveLimits] Skipped non-text file {path}");
                    skipped++;
                    continue;
                }
                bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                var text = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));

                int count = 0;
                var updated = isJson
                    ? JsonFlag.Replace(text, m => { count++; return m.Groups[1].Value + "false"; })
                    : XmlFlag.Replace(text, m => { count++; return m.Groups[1].Value + "0" + m.Groups[3].Value; });

                if (count > 0)
                {
                    backupDir ??= CreateBackupDir();
                    BackupFile(path, backupDir);
                    SetReadOnly(path, false);
                    var outBytes = Encoding.UTF8.GetBytes(updated);
                    if (bom) outBytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(outBytes).ToArray();
                    File.WriteAllBytes(path, outBytes);
                    changedFlags += count;
                    changedFiles++;
                    CrashReporter.Log($"[NvAppOverrideLimitService.RemoveLimits] {path}: {count} flag(s) cleared");
                }
                // Lock fingerprint.db so the NVIDIA App can't put the flags back
                if (!isJson) SetReadOnly(path, true);
            }

            if (changedFlags == 0)
            {
                return skipped > 0
                    ? new Result(false, "NVIDIA App의 파일 형식을 BONG DLSS5가 인식하지 못해 아무것도 변경하지 않았습니다. 최신 NVIDIA App에서 설정 저장 방식이 바뀌었을 수 있습니다.")
                    : new Result(true, "제한이 이미 해제되어 있습니다. 변경할 내용이 없습니다.");
            }

            PruneBackups(5);
            var restart = RestartServices();
            CrashReporter.Log($"[NvAppOverrideLimitService.RemoveLimits] {changedFlags} flag(s) cleared in {changedFiles} file(s), backup in {backupDir}");
            var msg = "이제 NVIDIA App이 지원하지 않는 게임의 DLSS 재정의를 초기화하지 않습니다. " +
                      $"원본 파일의 백업은 {backupDir}에 있습니다.";
            return restart == null
                ? new Result(true, msg + "\n\nNVIDIA 서비스를 다시 시작했습니다. 드라이버 또는 NVIDIA App 업데이트 후 다시 실행하세요.")
                : new Result(false, msg + "\n\n" + restart);
        }
        catch (UnauthorizedAccessException ex)
        {
            CrashReporter.Log($"[NvAppOverrideLimitService.RemoveLimits] Access denied: {ex.Message}");
            return new Result(false, "Windows에서 BONG DLSS5의 NVIDIA App 파일 변경을 허용하지 않았습니다. NVIDIA App을 닫고 다시 시도하세요.");
        }
        catch (IOException ex)
        {
            CrashReporter.Log($"[NvAppOverrideLimitService.RemoveLimits] IO error: {ex.Message}");
            return new Result(false, "파일이 사용 중이어서 변경할 수 없습니다. NVIDIA App을 닫고 다시 시도하세요.\n\n" + ex.Message);
        }
    }

    /// <summary>Restarts the NVIDIA container services. Returns null on success, else a user-facing message.</summary>
    private static string? RestartServices()
    {
        var script = "$ok = $true; foreach ($n in @('" + string.Join("','", ServiceNames) + "')) { " +
                     "if (Get-Service -Name $n -ErrorAction SilentlyContinue) { " +
                     "try { Restart-Service -Force -Name $n -ErrorAction Stop } catch { $ok = $false } } }; " +
                     "if (-not $ok) { exit 1 }";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        // Always "runas": no prompt when RHI is already elevated, one UAC prompt otherwise
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {encoded}",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            using var p = Process.Start(psi);
            if (p == null) return "NVIDIA 서비스를 다시 시작할 수 없습니다. 변경 사항을 적용하려면 PC를 다시 시작하세요.";
            if (!p.WaitForExit(60_000))
                return "NVIDIA 서비스 재시작이 너무 오래 걸렸습니다. 변경 사항을 적용하려면 PC를 다시 시작하세요.";
            if (p.ExitCode != 0)
            {
                CrashReporter.Log($"[NvAppOverrideLimitService.RestartServices] Exit code {p.ExitCode}");
                return "NVIDIA 서비스를 다시 시작할 수 없습니다. 변경 사항을 적용하려면 PC를 다시 시작하세요.";
            }
            CrashReporter.Log("[NvAppOverrideLimitService.RestartServices] Services restarted");
            return null;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            CrashReporter.Log("[NvAppOverrideLimitService.RestartServices] UAC prompt cancelled");
            return "Windows 관리자 확인이 취소되어 NVIDIA 서비스를 다시 시작하지 않았습니다. 변경 사항을 적용하려면 PC를 다시 시작하세요.";
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            CrashReporter.Log($"[NvAppOverrideLimitService.RestartServices] {ex.Message}");
            return "NVIDIA 서비스를 다시 시작할 수 없습니다. 변경 사항을 적용하려면 PC를 다시 시작하세요.";
        }
    }

    private static string CreateBackupDir()
    {
        var dir = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void BackupFile(string path, string backupDir)
    {
        var rel = Path.GetRelativePath(NvBackendDir, path);
        var dest = FileHelper.SafeCombine(backupDir, rel) ?? Path.Combine(backupDir, Path.GetFileName(path));
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(path, dest, overwrite: true);
        // Backups are restored by copying back, so don't keep the read-only flag on them
        SetReadOnly(dest, false);
    }

    private static void SetReadOnly(string path, bool readOnly)
    {
        var attr = File.GetAttributes(path);
        var updated = readOnly ? attr | FileAttributes.ReadOnly : attr & ~FileAttributes.ReadOnly;
        if (updated != attr) File.SetAttributes(path, updated);
    }

    private static void PruneBackups(int keep)
    {
        try
        {
            var old = new DirectoryInfo(BackupRoot).GetDirectories()
                .OrderByDescending(d => d.Name, StringComparer.Ordinal).Skip(keep);
            foreach (var d in old) d.Delete(recursive: true);
        }
        catch (IOException ex) { CrashReporter.Log($"[NvAppOverrideLimitService.PruneBackups] {ex.Message}"); }
        catch (UnauthorizedAccessException ex) { CrashReporter.Log($"[NvAppOverrideLimitService.PruneBackups] {ex.Message}"); }
    }
}
