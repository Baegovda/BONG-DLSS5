// OsCustomBuildDialogs.cs -- dialogs shared by the Settings card (SettingsHandler.OsCustomBuilds.cs) and
// drag and drop (DragDropHandler.Addon.cs) for custom OptiScaler builds.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

internal static class OsCustomBuildDialogs
{
    /// <summary>Asks for a build name (prefilled). Null when cancelled.</summary>
    internal static async Task<string?> AskNameAsync(XamlRoot xamlRoot, string title, string suggested, string? intro = null)
    {
        var box = new TextBox { Text = suggested, MaxLength = 60, PlaceholderText = "이름" };
        box.SelectAll();
        var panel = new StackPanel { Spacing = 8 };
        if (intro != null)
            panel.Children.Add(new TextBlock { Text = intro, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock
        {
            Text = "OptiScaler 버전 목록에 \"Custom: <name>\"으로 표시됩니다.",
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(box);
        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "저장",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };
        box.Loaded += (s, e) => box.Focus(FocusState.Programmatic);
        var result = await DialogService.ShowSafeAsync(dialog);
        if (result != ContentDialogResult.Primary) return null;
        var name = box.Text.Trim();
        return name.Length == 0 ? suggested : name;
    }
}
