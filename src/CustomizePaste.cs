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

        string ShortcutText()
        {
            return ((set.SuperPasteModifiers & 2) != 0 ? "Ctrl+" : "") +
                ((set.SuperPasteModifiers & 4) != 0 ? "Shift+" : "") +
                ((set.SuperPasteModifiers & 1) != 0 ? "Alt+" : "") +
                ((System.Windows.Forms.Keys)set.SuperPasteKey).ToString();
        }
        StackPanel BuildSuperPaste()
        {
            spEnabled = Toggle(); BindToggle(spEnabled, value => set.SuperPasteEnabled = value);
            spKey = PasteTextBox(false, 34); spKey.IsReadOnly = true;
            spKey.GotKeyboardFocus += delegate { if (SuperPasteCapture != null) SuperPasteCapture(true); };
            spKey.LostKeyboardFocus += delegate { if (SuperPasteCapture != null) SuperPasteCapture(false); };
            spKey.PreviewKeyDown += (sender, e) =>
            {
                e.Handled = true;
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                int vk = KeyInterop.VirtualKeyFromKey(key);
                ModifierKeys mods = Keyboard.Modifiers;
                if (!PasteHotkey.ValidKey(vk) || mods == ModifierKeys.None || (mods & ModifierKeys.Windows) != 0) return;
                int mask = ((mods & ModifierKeys.Alt) != 0 ? 1 : 0) | ((mods & ModifierKeys.Control) != 0 ? 2 : 0) | ((mods & ModifierKeys.Shift) != 0 ? 4 : 0);
                Edit(delegate { set.SuperPasteKey = vk; set.SuperPasteModifiers = mask; });
            };
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
                Box(PageOf(new TextBlock { Text = L.SPHotkeyHint, Style = Res<Style>("RowHint") }, spKey, spStatus)),
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
            spKey.Text = ShortcutText(); spMode.SelectedIndex = set.SuperPasteMarkdown ? 0 : 1;
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
