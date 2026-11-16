// MainWindow.GameNotes.cs -- the game menu's "Game notes…": your own notes for a game, the same ones the
// RHI overlay shows (and edits) on its Info and links page. Stored by GameJournalStore.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using RenoDXCommander.Models;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private async Task EditGameNotesAsync(GameCardViewModel card)
    {
        try
        {
            if (Content?.XamlRoot is not { } xamlRoot) return;
            var key = GameKey.FromCard(card.GameName, card.Source).ToKey();
            var box = new TextBox
            {
                Text = GameJournalStore.GetNotes(key),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Width = 520,
                Height = 260,
                PlaceholderText = "기억할 코드, 경로, 설정…",
            };
            ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
            var panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(new TextBlock
            {
                Text = "나만 볼 수 있습니다. 플레이 중에는 BONG DLSS5 오버레이의 정보 및 링크 페이지에도 표시됩니다.",
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(box);
            var dialog = new ContentDialog
            {
                Title = $"{card.GameName} 메모",
                Content = panel,
                PrimaryButtonText = "저장",
                CloseButtonText = "취소",
                DefaultButton = ContentDialogButton.None, // Enter makes a new line
                XamlRoot = xamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            dialog.Resources["ContentDialogMaxWidth"] = 640.0;
            box.Loaded += (s, e) => box.Focus(FocusState.Programmatic);
            if (await DialogService.ShowSafeAsync(dialog) != ContentDialogResult.Primary) return;
            GameJournalStore.SetNotes(key, box.Text ?? "");
            CrashReporter.Log($"[MainWindow.EditGameNotes] Saved notes for '{card.GameName}'");
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"[MainWindow.EditGameNotes] {ex.Message}");
        }
    }
}
