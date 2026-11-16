// SettingsHandler.ShaderCompile.cs -- Settings › Driver Settings › Driver defaults: "Compile shaders now" row under
// Shader pre-compile (built in C#, XAML constraint). Runs NVIDIA's shader compiler straight away, like the NVIDIA
// App's Shader Cache › Compile now, at the Shader pre-compile level. See NvShaderCompilerService.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public partial class SettingsHandler
{
    private bool _shaderCompileRowAdded;
    private Button? _shaderCompileBtn;
    private TextBlock? _shaderCompileStatus;
    private ProgressBar? _shaderCompileBar;
    private bool _shaderCompileOurs; // RHI started the run being shown

    private const string ShaderCompileIdleText = "지금 컴파일";

    private void EnsureShaderCompileRow()
    {
        if (_shaderCompileRowAdded) return;
        _shaderCompileRowAdded = true;
        try
        {
            var path = new List<UIElement>();
            bool found = FindPath(_window.SettingsCardsPanel, _window.ShaderPrecompileCombo, path);
            int ci = found ? path.FindLastIndex(e => e is StackPanel) : -1; // the column; the next element is the row
            if (ci < 0 || ci + 1 >= path.Count || path[ci] is not StackPanel column || path[ci + 1] is not FrameworkElement row)
            {
                CrashReporter.Log("[SettingsHandler.EnsureShaderCompileRow] Shader pre-compile row not found, not added");
                return;
            }

            _shaderCompileBtn = new Button { Content = ShaderCompileIdleText, Style = FindStyle("SettingsButtonStyle") };
            if (Application.Current.Resources.TryGetValue(ResourceKeys.SettingsLabelBrush, out var v) && v is Brush brush)
            {
                _shaderCompileBtn.Foreground = brush;
                _shaderCompileBtn.Resources["ButtonForegroundPointerOver"] = brush;
                _shaderCompileBtn.Resources["ButtonForegroundPressed"] = brush;
            }
            ToolTipService.SetToolTip(_shaderCompileBtn,
                "PC가 유휴 상태가 될 때까지 기다리지 않고 지금 NVIDIA 셰이더 컴파일러를 실행합니다(NVIDIA 앱의 Compile now). " +
                "게임이 셰이더 캐시에 이미 저장한 DirectX 12 셰이더를 설치된 드라이버용으로 다시 빌드하므로, 드라이버 업데이트 후 처음 플레이할 때 " +
                "끊김이 줄어듭니다. 플레이하지 않은 게임(또는 셰이더 캐시 지우기 후)은 컴파일할 수 없습니다. 위의 셰이더 사전 컴파일 수준을 사용합니다(꺼져 있으면 Medium). " +
                "몇 분 정도 걸리며, 다시 클릭하면 중지됩니다.");
            _shaderCompileBtn.Click += ShaderCompileBtn_Click;

            _shaderCompileStatus = new TextBlock
            {
                Style = FindStyle("SettingsHelpStyle"),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };

            _shaderCompileBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = Visibility.Collapsed,
            };

            int at = column.Children.IndexOf(row) + 1;
            column.Children.Insert(at, SettingRow("지금 셰이더 컴파일", _shaderCompileBtn));
            column.Children.Insert(at + 1, _shaderCompileStatus);
            column.Children.Insert(at + 2, _shaderCompileBar);

            if (NvShaderCompilerService.FindCompiler() == null)
            {
                _shaderCompileBtn.IsEnabled = false;
                SetShaderCompileStatus("셰이더 컴파일러(NvOSC.exe)가 포함된 NVIDIA 드라이버가 필요합니다.");
            }
            else if (NvShaderCompilerService.IsRunning())
            {
                ShowShaderCompileRunning(ours: false);
                _ = WatchOtherShaderCompileAsync();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            CrashReporter.Log($"[SettingsHandler.EnsureShaderCompileRow] Failed — {ex.Message}");
        }
    }

    private async void ShaderCompileBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_shaderCompileBtn == null) return;

            // Running (ours, the NVIDIA App's or the idle task's): the button stops it
            if (NvShaderCompilerService.IsRunning())
            {
                await Task.Run(NvShaderCompilerService.Stop);
                ShowShaderCompileIdle();
                SetShaderCompileStatus("중지했습니다. 이미 컴파일된 셰이더는 유지됩니다.");
                return;
            }

            var exe = NvShaderCompilerService.FindCompiler();
            if (exe == null)
            {
                SetShaderCompileStatus("설치된 드라이버에서 NVIDIA 셰이더 컴파일러(NvOSC.exe)를 찾을 수 없습니다.");
                return;
            }

            string resUtil = _window.ShaderPrecompileCombo.SelectedIndex switch
            {
                1 => "MIN",
                3 => "MAX",
                _ => "MED", // Medium, and Off (the NVIDIA App's own Compile now default)
            };

            ShowShaderCompileRunning(ours: true);
            ShaderCompileProgress? last = null;
            var progress = new Progress<ShaderCompileProgress>(p =>
            {
                last = p;
                if (_shaderCompileOurs) ShowShaderCompileProgress(p);
            });
            var started = DateTime.Now;
            int? code = await NvShaderCompilerService.RunAsync(exe, resUtil, _window.LifetimeToken, progress);
            if (_window.IsShuttingDown || !_shaderCompileOurs) return; // stopped with the button: already reported
            ShowShaderCompileIdle();

            var elapsed = DateTime.Now - started;
            string took = elapsed.TotalMinutes < 1 ? "1분 미만" : $"{elapsed.TotalMinutes:F0}분";
            if (code == 0 && last is { Total: > 0 }) took += last.Total == 1 ? ", 앱 1개" : $", 앱 {last.Total}개";
            SetShaderCompileStatus(code switch
            {
                null => "NVIDIA 셰이더 컴파일러를 시작할 수 없습니다. 자세한 내용은 로그를 확인하세요.",
                // It only rebuilds shaders games have already saved in the cache: an empty or fresh cache ends at once
                0 when elapsed.TotalSeconds < 5 => "컴파일할 항목이 없습니다. 게임이 셰이더 캐시에 이미 저장한 셰이더를 다시 빌드하므로, " +
                                                   "게임을 플레이한 후나 드라이버 업데이트 후에 작업할 내용이 생깁니다.",
                0 => $"{DateTime.Now:HH:mm}에 완료 ({took}).",
                _ => $"NVIDIA 셰이더 컴파일러가 코드 {code}(으)로 중지되었습니다. 셰이더 사전 컴파일이 꺼져 있으면 켜고 다시 시도하세요.",
            });
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[SettingsHandler.ShaderCompileBtn_Click] {ex.Message}");
            ShowShaderCompileIdle();
        }
    }

    private void ShowShaderCompileProgress(ShaderCompileProgress p)
    {
        if (p.Total == 0) return; // no app caches found: keep the moving bar and the general text
        if (_shaderCompileBar != null)
        {
            _shaderCompileBar.IsIndeterminate = p.Fraction == null;
            if (p.Fraction is double f) _shaderCompileBar.Value = f * 100;
        }
        if (p.Done == 0) SetShaderCompileStatus($"앱 {p.Total}개의 셰이더 컴파일 중...");
        else SetShaderCompileStatus($"{p.CurrentApp ?? "셰이더"} 컴파일 중 ({p.Done}/{p.Total})");
    }

    /// <summary>A run RHI didn't start was going when Settings opened: reset the button when it ends.</summary>
    private async Task WatchOtherShaderCompileAsync()
    {
        try
        {
            while (!_window.IsShuttingDown && !_shaderCompileOurs && await Task.Run(NvShaderCompilerService.IsRunning))
                await Task.Delay(3000, _window.LifetimeToken);
            if (!_window.IsShuttingDown && !_shaderCompileOurs) { ShowShaderCompileIdle(); SetShaderCompileStatus(null); }
        }
        catch (OperationCanceledException) { /* RHI closing */ }
    }

    private void ShowShaderCompileRunning(bool ours)
    {
        _shaderCompileOurs = ours;
        if (_shaderCompileBtn != null) _shaderCompileBtn.Content = "컴파일 중지";
        if (_shaderCompileBar != null)
        {
            _shaderCompileBar.IsIndeterminate = true; // until the first progress report (or for good without the log)
            _shaderCompileBar.Value = 0;
            _shaderCompileBar.Visibility = ours ? Visibility.Visible : Visibility.Collapsed;
        }
        SetShaderCompileStatus(ours
            ? "셰이더 컴파일 중입니다. 몇 분 정도 걸리고 CPU를 일부 사용하지만 BONG DLSS5는 계속 사용할 수 있습니다."
            : "NVIDIA 셰이더 컴파일러가 이미 실행 중입니다(NVIDIA 앱 또는 유휴 작업이 시작함).");
    }

    private void ShowShaderCompileIdle()
    {
        _shaderCompileOurs = false;
        if (_shaderCompileBtn != null) _shaderCompileBtn.Content = ShaderCompileIdleText;
        if (_shaderCompileBar != null) _shaderCompileBar.Visibility = Visibility.Collapsed;
    }

    private void SetShaderCompileStatus(string? text)
    {
        if (_shaderCompileStatus == null) return;
        _shaderCompileStatus.Text = text ?? "";
        _shaderCompileStatus.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }
}
