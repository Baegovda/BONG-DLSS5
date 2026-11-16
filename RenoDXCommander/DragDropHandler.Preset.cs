// DragDropHandler.Preset.cs — Preset drop processing: validate, store, game selection, deploy, shader confirmation.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public partial class DragDropHandler
{
    /// <summary>
    /// Processes a dropped .ini file: validate → store → game selection → deploy → shader confirmation.
    /// </summary>
    public async Task ProcessDroppedPreset(string iniPath)
    {
        var fileName = Path.GetFileName(iniPath);
        _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] Received '{fileName}'");

        // ── Step 1: Read and validate ─────────────────────────────────────────
        string content;
        try
        {
            content = File.ReadAllText(iniPath);
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] Failed to read '{iniPath}' — {ex.Message}");
            var errDialog = new ContentDialog
            {
                Title = "❌ 읽기 오류",
                Content = $"파일을 읽지 못했습니다: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(errDialog);
            return;
        }

        if (!PresetValidator.IsReShadePreset(content))
        {
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] '{fileName}' is not a recognised ReShade preset");
            var errDialog = new ContentDialog
            {
                Title = "❌ ReShade 프리셋이 아님",
                Content = "이 파일은 인식되는 ReShade 프리셋이 아닙니다. 올바른 프리셋에는 @.fx 항목이 하나 이상 있는 Techniques= 줄이 있어야 합니다.",
                CloseButtonText = "OK",
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(errDialog);
            return;
        }

        // ── Step 2: Keep a copy in the presets folder ─────────────────────────
        // Only a convenience: the install below reads the dropped file. A preset dragged in from the
        // presets folder itself is already there (copying a file onto itself fails as "in use").
        var destPreset = Path.Combine(PresetPopupHelper.PresetsDir, fileName);
        if (string.Equals(Path.GetFullPath(iniPath), Path.GetFullPath(destPreset), StringComparison.OrdinalIgnoreCase))
        {
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] '{fileName}' is already in the presets folder");
        }
        else
        {
            try
            {
                Directory.CreateDirectory(PresetPopupHelper.PresetsDir);
                File.Copy(iniPath, destPreset, overwrite: true);
                _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] Stored '{fileName}' in presets folder");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not fatal: carry on and install the dropped file
                _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] Couldn't store '{fileName}' in presets folder, installing anyway — {ex.Message}");
            }
        }

        // ── Step 3: Game selection dialog ─────────────────────────────────────
        var cards = ViewModel.AllCards?.ToList() ?? new();
        if (cards.Count == 0)
        {
            var noGamesDialog = new ContentDialog
            {
                Title = "사용 가능한 게임 없음",
                Content = "현재 감지된 게임이 없습니다. 먼저 게임을 추가하세요.",
                CloseButtonText = "OK",
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(noGamesDialog);
            return;
        }

        var combo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "게임 선택...",
        };

        var sortedCards = cards.OrderBy(c => c.GameName, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var card in sortedCards)
            combo.Items.Add(new ComboBoxItem { Content = card.GameName, Tag = card });

        // Auto-select the currently selected game in the sidebar
        if (ViewModel.SelectedGame != null)
        {
            for (int i = 0; i < sortedCards.Count; i++)
            {
                if (string.Equals(sortedCards[i].GameName, ViewModel.SelectedGame.GameName, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    break;
                }
            }
        }

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = $"{fileName}을(를) 게임 폴더에 설치합니다.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = UIFactory.Brush(ResourceKeys.TextSecondaryBrush),
        });
        panel.Children.Add(combo);

        var pickDialog = new ContentDialog
        {
            Title = "🎨 ReShade 프리셋 설치",
            Content = panel,
            PrimaryButtonText = "다음",
            CloseButtonText = "취소",
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        var pickResult = await DialogService.ShowSafeAsync(pickDialog);
        if (pickResult != ContentDialogResult.Primary) return;

        if (combo.SelectedItem is not ComboBoxItem selected || selected.Tag is not GameCardViewModel targetCard)
        {
            var noSelection = new ContentDialog
            {
                Title = "선택된 게임 없음",
                Content = "프리셋을 설치할 게임을 선택하세요.",
                CloseButtonText = "OK",
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(noSelection);
            return;
        }

        var gameName = targetCard.GameName;
        var installPath = targetCard.InstallPath;

        // ── Step 4: Copy preset to game folder ───────────────────────────────
        try
        {
            var destGame = Path.Combine(installPath, fileName);
            File.Copy(iniPath, destGame, overwrite: true);
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] Deployed '{fileName}' to '{installPath}'");
        }
        catch (Exception ex)
        {
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] Failed to deploy preset — {ex.Message}");
            var errDialog = new ContentDialog
            {
                Title = "❌ 배포 실패",
                Content = $"프리셋을 게임 폴더에 복사하지 못했습니다: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = _window.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await DialogService.ShowSafeAsync(errDialog);
            return;
        }

        // ── Step 5: Shader confirmation dialog ───────────────────────────────
        var shaderDialog = new ContentDialog
        {
            Title = "🔧 셰이더를 설치할까요?",
            Content = "필요한 셰이더와 텍스처도 함께 설치할까요?",
            PrimaryButtonText = "예",
            CloseButtonText = "No",
            XamlRoot = _window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        var shaderResult = await DialogService.ShowSafeAsync(shaderDialog);
        if (shaderResult == ContentDialogResult.Primary)
        {
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] User chose to install shaders for '{gameName}'");
            await ViewModel.ApplyPresetShadersAsync(gameName, new[] { iniPath }, targetCard.Source ?? "");

            // Rebuild overrides panel so the shader toggle reflects the new "Select" mode
            if (ViewModel.SelectedGame is { } selectedCard
                && selectedCard.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase))
            {
                _window.BuildOverridesPanel(selectedCard);
            }
        }
        else
        {
            _crashReporter.Log($"[DragDropHandler.ProcessDroppedPreset] User declined shader install for '{gameName}'");
        }
    }
}
