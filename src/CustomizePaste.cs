using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Cccc
{
    sealed partial class Customize
    {
        CheckBox spEnabled;
        CheckBox[] spRules;
        ComboBox spMode;
        TextBox spKey, spInput, spOutput;
        TextBlock spSummary, spStatus;
        // Supplied by App; the UI does not register a second copy of the shortcut.
        public Func<string> SuperPasteStatus;
        public Action<bool> SuperPasteCapture;

        ListBox spShortcuts;
        Button spRemove, spAdd, spSaveKey;
        CheckBox[] spModifiers;
        ComboBox spMainKey;
        static readonly int[] PasteModifierBits = { 2, 4, 1, 8 };
        void ReloadShortcutEditor()
        {
            int mods = 12, key = 45;
            int index = spShortcuts.SelectedIndex;
            if (index >= 0) PasteHotkey.Parse(set.SuperPasteShortcuts[index], out mods, out key);
            for (int i = 0; i < spModifiers.Length; i++) spModifiers[i].IsChecked = (mods & PasteModifierBits[i]) != 0;
            spMainKey.SelectedValue = key;
        }
        internal static int PasteModifierMask(ModifierKeys mods, bool windowsDown)
        {
            return ((mods & ModifierKeys.Alt) != 0 ? 1 : 0) | ((mods & ModifierKeys.Control) != 0 ? 2 : 0) |
                ((mods & ModifierKeys.Shift) != 0 ? 4 : 0) | (((mods & ModifierKeys.Windows) != 0 || windowsDown) ? 8 : 0);
        }
        void SavePasteShortcut(int mask, int vk)
        {
            string shortcut = mask + ":" + vk;
            int unusedMods, unusedKey;
            if (!PasteHotkey.Parse(shortcut, out unusedMods, out unusedKey)) { spStatus.Text = L.SPInvalidShortcut; return; }
            int index = spShortcuts.SelectedIndex;
            int existing = set.SuperPasteShortcuts.IndexOf(shortcut);
            if (existing >= 0)
            {
                spShortcuts.SelectedIndex = existing;
                spShortcuts.Focus();
                spStatus.Text = L.SPShortcutExists + " " + PasteHotkey.Display(shortcut);
                return;
            }
            Edit(delegate
            {
                if (index >= 0) set.SuperPasteShortcuts[index] = shortcut;
                else set.SuperPasteShortcuts.Add(shortcut);
            });
            spShortcuts.SelectedIndex = index >= 0 ? index : set.SuperPasteShortcuts.Count - 1;
            ReloadShortcutEditor();
            spShortcuts.Focus();
        }
        void ReloadShortcutList()
        {
            int selected = spShortcuts.SelectedIndex;
            spShortcuts.Items.Clear();
            foreach (string shortcut in set.SuperPasteShortcuts) spShortcuts.Items.Add(PasteHotkey.Display(shortcut));
            spShortcuts.SelectedIndex = selected < spShortcuts.Items.Count ? selected : spShortcuts.Items.Count - 1;
            spRemove.IsEnabled = set.SuperPasteShortcuts.Count > 1 && spShortcuts.SelectedIndex >= 0;
        }
        StackPanel BuildSuperPaste()
        {
            spEnabled = Toggle(); BindToggle(spEnabled, value => set.SuperPasteEnabled = value);
            spShortcuts = new ListBox { MaxHeight = 150, Margin = new Thickness(0, 6, 0, 6) };
            spShortcuts.SetResourceReference(Control.BackgroundProperty, "Surface");
            spShortcuts.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            spKey = PasteTextBox(false, 34); spKey.IsReadOnly = true;
            spRemove = new Button { Content = L.Remove, Style = Res<Style>("Subtle"), Margin = new Thickness(8, 0, 0, 0) };
            spAdd = new Button { Content = L.SPAddShortcut, Style = Res<Style>("Subtle") };
            spAdd.Click += delegate { spShortcuts.SelectedIndex = -1; ReloadShortcutEditor(); spKey.Text = L.SPPressShortcut; spKey.Focus(); };
            spRemove.Click += delegate
            {
                int index = spShortcuts.SelectedIndex;
                if (index >= 0 && set.SuperPasteShortcuts.Count > 1) Edit(delegate { set.SuperPasteShortcuts.RemoveAt(index); });
            };
            spShortcuts.SelectionChanged += delegate
            {
                if (loading) return;
                int index = spShortcuts.SelectedIndex;
                spKey.Text = index >= 0 ? PasteHotkey.Display(set.SuperPasteShortcuts[index]) : L.SPPressShortcut;
                spRemove.IsEnabled = index >= 0 && set.SuperPasteShortcuts.Count > 1;
                ReloadShortcutEditor();
            };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(spAdd); buttons.Children.Add(spRemove);
            spKey.GotKeyboardFocus += delegate { if (SuperPasteCapture != null) SuperPasteCapture(true); };
            spKey.LostKeyboardFocus += delegate { if (SuperPasteCapture != null) SuperPasteCapture(false); if (spStatus != null && SuperPasteStatus != null) spStatus.Text = SuperPasteStatus(); };
            spKey.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Tab) return;
                if (e.Key == Key.Escape) { spShortcuts.Focus(); e.Handled = true; return; }
                e.Handled = true;
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                int vk = KeyInterop.VirtualKeyFromKey(key);
                ModifierKeys mods = Keyboard.Modifiers;
                if (!PasteHotkey.ValidKey(vk)) return;
                // WPF's modifier snapshot can omit Win for remapped/system-handled input.
                int mask = PasteModifierMask(mods, Native.KeyDown(0x5B) || Native.KeyDown(0x5C));
                SavePasteShortcut(mask, vk);
            };
            var manual = new WrapPanel { Margin = new Thickness(0, 6, 0, 2), VerticalAlignment = VerticalAlignment.Center };
            spModifiers = new CheckBox[4];
            string[] modifierNames = { "Ctrl", "Shift", "Alt", "Windows" };
            for (int i = 0; i < spModifiers.Length; i++)
            {
                spModifiers[i] = new CheckBox { Content = modifierNames[i], Margin = new Thickness(0, 8, 12, 8), VerticalAlignment = VerticalAlignment.Center };
                spModifiers[i].SetResourceReference(Control.ForegroundProperty, "TextBrush");
                manual.Children.Add(spModifiers[i]);
            }
            spMainKey = new ComboBox { Style = Res<Style>("Cmb"), Width = 110, SelectedValuePath = "Tag", Margin = new Thickness(0, 0, 8, 0) };
            for (int key = 0; key < 256; key++)
                if (PasteHotkey.ValidKey(key)) spMainKey.Items.Add(new ComboBoxItem { Content = ((System.Windows.Forms.Keys)key).ToString(), Tag = key });
            spSaveKey = new Button { Content = L.SPSaveShortcut, Style = Res<Style>("Subtle") };
            spSaveKey.Click += delegate
            {
                int mask = 0;
                for (int i = 0; i < spModifiers.Length; i++) if (spModifiers[i].IsChecked == true) mask |= PasteModifierBits[i];
                SavePasteShortcut(mask, spMainKey.SelectedValue is int ? (int)spMainKey.SelectedValue : 0);
            };
            manual.Children.Add(spMainKey); manual.Children.Add(spSaveKey);
            spMode = new ComboBox { Style = Res<Style>("Cmb"), Width = 250 };
            spMode.Items.Add(L.SPMarkdown); spMode.Items.Add(L.SPPlain);
            spMode.SelectionChanged += delegate { if (!loading) Edit(delegate { set.SuperPasteMarkdown = spMode.SelectedIndex == 0; }); };
            var rulePanel = new StackPanel(); spRules = new CheckBox[9];
            var labels = L.SPRuleLabels;
            for (int i = 0; i < spRules.Length; i++)
            {
                int bit = 1 << i;
                var check = new CheckBox { Content = labels[i], Margin = new Thickness(4, 6, 4, 6) };
                check.SetResourceReference(Control.ForegroundProperty, "TextBrush");
                check.Checked += delegate { if (!loading) Edit(delegate { set.SuperPasteOptions |= bit; }); };
                check.Unchecked += delegate { if (!loading) Edit(delegate { set.SuperPasteOptions &= ~bit; }); };
                spRules[i] = check; rulePanel.Children.Add(check);
            }
            spInput = PasteTextBox(true, 130);
            spInput.Text = "\uF06C中文  English  \r\n\r\n`保留  code`\r\n\r\n";
            spOutput = PasteTextBox(true, 130); spOutput.IsReadOnly = true;
            spSummary = new TextBlock { Style = Res<Style>("RowHint") };
            spStatus = new TextBlock { Style = Res<Style>("RowHint") };
            spInput.TextChanged += delegate { PreviewSuperPaste(); };
            return PageOf(SettingCard(L.SuperPasteTitle, L.SPOverwrite, spEnabled),
                Box(PageOf(new TextBlock { Text = L.SPHotkeyHint, Style = Res<Style>("RowHint") }, spShortcuts, buttons, spKey, new TextBlock { Text = L.SPManualShortcut, Style = Res<Style>("RowHint") }, manual, spStatus)),
                SettingCard(L.SuperPasteTitle, L.SPModeHint, spMode), Box(rulePanel),
                Header(L.SPSample), spInput, Header(L.SPOutput), spOutput, spSummary);
        }
        TextBox PasteTextBox(bool multiline, int height)
        {
            var box = new TextBox { Height = height, AcceptsReturn = multiline, AcceptsTab = multiline,
                TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(8), Margin = new Thickness(0, 4, 0, 4), MaxLength = 32000 };
            box.SetResourceReference(Control.BackgroundProperty, "Surface");
            box.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            return box;
        }
        void ReloadSuperPaste()
        {
            if (spEnabled == null) return;
            spEnabled.IsChecked = set.SuperPasteEnabled;
            ReloadShortcutList();
            ReloadShortcutEditor();
            spKey.Text = spShortcuts.SelectedIndex >= 0 ? PasteHotkey.Display(set.SuperPasteShortcuts[spShortcuts.SelectedIndex]) : L.SPPressShortcut; spMode.SelectedIndex = set.SuperPasteMarkdown ? 0 : 1;
            for (int i = 0; i < spRules.Length; i++) spRules[i].IsChecked = (set.SuperPasteOptions & (1 << i)) != 0;
            spRules[7].IsEnabled = !set.SuperPasteMarkdown;
            spStatus.Text = SuperPasteStatus == null ? L.SPInactive : SuperPasteStatus();
            PreviewSuperPaste();
        }
        void PreviewSuperPaste()
        {
            if (spOutput == null || spInput == null) return;
            PasteResult result = SuperPasteText.Clean(spInput.Text, set.PasteRules());
            spOutput.Text = result.Text;
            spSummary.Text = L.Zh ? "变化区域：" + result.Changes + "；保护行：" + result.ProtectedRegions :
                "Changed regions: " + result.Changes + "; protected lines: " + result.ProtectedRegions;
        }
    }
}
