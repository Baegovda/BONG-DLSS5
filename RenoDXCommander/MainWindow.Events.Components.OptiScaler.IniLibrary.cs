// MainWindow.Events.Components.OptiScaler.IniLibrary.cs — "Save ini..." and "Load saved ini..." in the
// OptiScaler cog's footer. Saved inis live in OsIniLibrary (any number, named by the user); loading one
// makes it the game's OptiScaler.ini and remembers it, so a reinstall or update puts it back.
// Both use flyouts: a second dialog can't open over the cog.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RenoDXCommander.Services;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    /// <summary>Shown in the cog's status line after it reopens (e.g. after loading a saved ini).</summary>
    private string? _osCogNotice;

    private Button BuildOsSaveIniButton(OsCogContext ctx, string? iniPath, Action<string, bool> showStatus)
    {
        var card = ctx.Card;
        var btn = CogDialog.StyledButton("ini 저장...", CogButtonKind.Secondary);
        ToolTipService.SetToolTip(btn,
            "이 게임의 OptiScaler.ini를 원하는 이름으로 저장합니다. 저장된 ini 불러오기로 어떤 게임에든 불러올 수 있습니다.");

        var nameBox = CogDialog.MakeTextBox("", "이름 (예: F5 super ini)");
        nameBox.MaxLength = OsIniLibrary.MaxNameLength;
        var note = MakeOsFooterNote("");
        var saveBtn = CogDialog.StyledButton("저장", CogButtonKind.Primary, compact: true);
        var panel = new StackPanel { Spacing = 8, Width = 300 };
        panel.Children.Add(new TextBlock
        {
            Text = "이 게임의 OptiScaler.ini를 다음 이름으로 저장:",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
        });
        panel.Children.Add(nameBox);
        panel.Children.Add(note);
        panel.Children.Add(saveBtn);
        var flyout = new Flyout { Content = panel };
        btn.Flyout = flyout;

        void ShowNote(string text, bool error)
        {
            note.Text = text;
            note.Foreground = UIFactory.Brush(error ? ResourceKeys.AccentRedBrush : ResourceKeys.TextTertiaryBrush);
        }

        flyout.Opening += (_, _) =>
        {
            nameBox.Text = ViewModel.GetOsIniProfile(card.GameName, ctx.Store) ?? "";
            ShowNote("", false);
        };
        nameBox.TextChanged += (_, _) =>
        {
            var name = OsIniLibrary.CleanName(nameBox.Text);
            ShowNote(name != null && OsIniLibrary.Exists(name) ? $"저장된 ini \"{name}\"을(를) 대체합니다." : "", false);
        };

        async Task SaveAsync()
        {
            try
            {
                var name = OsIniLibrary.CleanName(nameBox.Text);
                if (name == null) { ShowNote("먼저 이름을 입력하세요.", true); return; }
                if (iniPath == null || !File.Exists(iniPath)) { ShowNote("이 게임에는 저장할 OptiScaler.ini가 없습니다.", true); return; }
                bool replaced = OsIniLibrary.Exists(name);
                await Task.Run(() => OsIniLibrary.Save(name, iniPath));
                // The game now matches the saved ini, so a reinstall puts it back
                ViewModel.SetOsIniProfile(card.GameName, name, ctx.Store);
                flyout.Hide();
                showStatus(replaced ? $"저장된 ini \"{name}\"을(를) 대체했습니다." : $"\"{name}\"(으)로 저장했습니다. 저장된 ini 불러오기로 어떤 게임에든 불러올 수 있습니다.", true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CrashReporter.Log($"[MainWindow.BuildOsSaveIniButton] Save failed — {ex.Message}");
                ShowNote($"저장할 수 없습니다: {ex.Message}", true);
            }
        }

        saveBtn.Click += async (_, _) => await SaveAsync();
        nameBox.KeyDown += async (_, e) =>
        {
            if (e.Key != Windows.System.VirtualKey.Enter) return;
            e.Handled = true;
            await SaveAsync();
        };
        return btn;
    }

    private Button BuildOsLoadIniButton(OsCogContext ctx, Action<string, bool> showStatus)
    {
        var card = ctx.Card;
        var store = ctx.Store;
        var btn = CogDialog.StyledButton("저장된 ini 불러오기...", CogButtonKind.Secondary);
        ToolTipService.SetToolTip(btn,
            "저장된 ini 중 하나를 이 게임의 OptiScaler.ini로 사용합니다. BONG DLSS5가 이를 기억해 재설치나 업데이트 후 다시 적용합니다.");

        var panel = new StackPanel { Spacing = 8, Width = 320 };
        var flyout = new Flyout { Content = panel };
        btn.Flyout = flyout;

        void Fill()
        {
            panel.Children.Clear();
            var names = OsIniLibrary.List();
            var note = MakeOsFooterNote("");

            void ShowNote(string text, bool error)
            {
                note.Text = text;
                note.Foreground = UIFactory.Brush(error ? ResourceKeys.AccentRedBrush : ResourceKeys.TextTertiaryBrush);
            }

            var addBtn = CogDialog.StyledButton("파일에서 추가...", CogButtonKind.Secondary, compact: true);
            ToolTipService.SetToolTip(addBtn, "PC의 어느 위치에 있는 OptiScaler.ini(빌드 자체 ini, Discord에서 공유된 ini 등)든 저장된 ini에 추가합니다.");
            addBtn.Click += async (_, _) =>
            {
                try
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    var file = await Task.Run(() =>
                    {
                        var ofn = new NativeInterop.OpenFileName();
                        ofn.structSize = System.Runtime.InteropServices.Marshal.SizeOf(ofn);
                        ofn.hwndOwner = hwnd;
                        ofn.filter = "INI files (*.ini)\0*.ini\0All Files (*.*)\0*.*\0";
                        ofn.file = new string(new char[260]);
                        ofn.maxFile = ofn.file.Length;
                        ofn.title = "OptiScaler.ini 추가";
                        ofn.flags = 0x00080000 | 0x00001000; // OFN_EXPLORER | OFN_FILEMUSTEXIST
                        return NativeInterop.GetOpenFileName(ref ofn) ? ofn.file.TrimEnd('\0') : null;
                    });
                    if (string.IsNullOrEmpty(file)) return;
                    var name = OsIniLibrary.SuggestName(file);
                    await Task.Run(() => OsIniLibrary.Save(name, file));
                    Fill();
                    flyout.ShowAt(btn);
                    showStatus($"\"{name}\"을(를) 저장된 ini에 추가했습니다.", true);
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[MainWindow.BuildOsLoadIniButton] Add from file failed — {ex.Message}");
                    ShowNote($"파일을 추가할 수 없습니다: {ex.Message}", true);
                }
            };

            var folderBtn = CogDialog.StyledButton("폴더 열기", CogButtonKind.Secondary, compact: true);
            ToolTipService.SetToolTip(folderBtn, "저장된 ini가 보관된 폴더를 엽니다. 거기서 이름을 바꾸거나 복사할 수도 있습니다.");
            folderBtn.Click += async (_, _) =>
            {
                try
                {
                    Directory.CreateDirectory(OsIniLibrary.Dir);
                    await Windows.System.Launcher.LaunchFolderPathAsync(OsIniLibrary.Dir);
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"[MainWindow.BuildOsLoadIniButton] Open folder failed — {ex.Message}");
                    ShowNote($"폴더를 열 수 없습니다: {ex.Message}", true);
                }
            };

            if (names.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "아직 저장된 ini가 없습니다. 게임에서 ini 저장으로 저장하거나 파일에서 추가하세요.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                });
                var emptyRow = new Controls.WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
                emptyRow.Children.Add(addBtn);
                emptyRow.Children.Add(folderBtn);
                panel.Children.Add(emptyRow);
                panel.Children.Add(note);
                return;
            }

            var current = ViewModel.GetOsIniProfile(card.GameName, store);
            var combo = CogDialog.MakeCombo(names, Math.Max(0, names.FindIndex(n => n.Equals(current, StringComparison.OrdinalIgnoreCase))));
            combo.HorizontalAlignment = HorizontalAlignment.Stretch;

            var useBtn = CogDialog.StyledButton("이 게임에 사용", CogButtonKind.Primary, compact: true);
            useBtn.Click += async (_, _) =>
            {
                if (combo.SelectedItem is not string name || string.IsNullOrEmpty(card.InstallPath)) return;
                var gameDir = card.InstallPath;
                var err = await Task.Run(() => OsIniLibrary.Deploy(name, gameDir, ViewModel.Settings.OsHotkey));
                if (err != null) { ShowNote($"불러올 수 없습니다: {err}", true); return; }

                ViewModel.SetOsIniProfile(card.GameName, name, store);
                // Edits applied to the old ini don't belong to this one
                ViewModel.SetOsIniEdits(card.GameName, null, store);
                SyncOsFgSettingsFromIni(card.GameName, store, gameDir);
                CrashReporter.Log($"[MainWindow.BuildOsLoadIniButton] '{card.GameName}' now uses saved ini '{name}'");
                flyout.Hide();
                _osCogNotice = $"\"{name}\"을(를) 이 게임의 OptiScaler.ini로 불러왔습니다. 재설치나 업데이트 후 다시 적용됩니다.";
                ReopenOsCog(ctx); // every setting above now reads the new ini
            };

            // Deleting asks for a second click
            var deleteBtn = CogDialog.StyledButton("삭제", CogButtonKind.Danger, compact: true);
            bool armed = false;
            deleteBtn.Click += async (_, _) =>
            {
                if (combo.SelectedItem is not string name) return;
                if (!armed)
                {
                    armed = true;
                    deleteBtn.Content = "한 번 더 클릭하여 삭제";
                    ShowNote($"저장된 ini에서 \"{name}\"을(를) 삭제할까요? 이미 사용 중인 게임은 OptiScaler.ini를 유지합니다.", false);
                    return;
                }
                try
                {
                    await Task.Run(() => OsIniLibrary.Delete(name));
                    if (name.Equals(ViewModel.GetOsIniProfile(card.GameName, store), StringComparison.OrdinalIgnoreCase))
                        ViewModel.SetOsIniProfile(card.GameName, null, store);
                    showStatus($"저장된 ini \"{name}\"을(를) 삭제했습니다.", true);
                    Fill();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    CrashReporter.Log($"[MainWindow.BuildOsLoadIniButton] Delete failed — {ex.Message}");
                    ShowNote($"삭제할 수 없습니다: {ex.Message}", true);
                }
            };
            combo.SelectionChanged += (_, _) =>
            {
                armed = false;
                deleteBtn.Content = "삭제";
                ShowNote("", false);
            };

            panel.Children.Add(new TextBlock
            {
                Text = "저장된 ini를 이 게임의 OptiScaler.ini로 사용합니다. 현재 파일은 먼저 백업됩니다.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
            });
            panel.Children.Add(combo);
            var row = new Controls.WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
            row.Children.Add(useBtn);
            row.Children.Add(deleteBtn);
            row.Children.Add(addBtn);
            row.Children.Add(folderBtn);
            panel.Children.Add(row);
            panel.Children.Add(note);
        }

        flyout.Opening += (_, _) => Fill();
        return btn;
    }

    /// <summary>The Frame Generation dropdowns keep their own saved values; match them to the game's ini.</summary>
    private void SyncOsFgSettingsFromIni(string gameName, string store, string gameDir)
    {
        try
        {
            var settings = OsIniEditor.ParseFile(Path.Combine(gameDir, OptiScalerService.IniFileName));
            string? Val(string key) => settings.FirstOrDefault(s =>
                s.Section.Equals("FrameGen", StringComparison.OrdinalIgnoreCase) && s.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
            ViewModel.SetOsFgInput(gameName, string.IsNullOrEmpty(Val("FGInput")) ? "auto" : Val("FGInput")!, store);
            ViewModel.SetOsFgOutput(gameName, string.IsNullOrEmpty(Val("FGOutput")) ? "auto" : Val("FGOutput")!, store);
            var nvngx = Val("FGNvngxReplacement");
            ViewModel.SetOsFgNvngxReplacement(gameName,
                string.IsNullOrEmpty(nvngx) || nvngx.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "None" : nvngx, store);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashReporter.Log($"[MainWindow.SyncOsFgSettingsFromIni] Failed — {ex.Message}");
        }
    }
}
