using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;

namespace Cccc
{
    sealed partial class Customize
    {
        CheckBox spEnabled, spSymbols, spTrailing, spBlankLines, spEndNewlines;
        ToggleButton[] spModifiers;
        StackPanel spSettings;
        ComboBox spMainKey;
        TextBlock spStatus, spSample, spResult;
        // Supplied by App; the UI does not register a second copy of the shortcut.
        public Func<string> SuperPasteStatus;
        public Func<bool> ConfirmSuperPaste;
        static readonly int[] PasteModifierBits = { 2, 4, 1, 8 };
        internal const string SpSampleText =
            "\uF06C标题  \n" +
            "\n\n\n" +
            "```\n" +
            "code  \n" +
            "```\n" +
            "结尾  \n\n\n";

        StackPanel BuildSuperPaste()
        {
            var warnIcon = Glyph("\uE7BA", 16);
            warnIcon.SetResourceReference(Shape.FillProperty, "DangerBrush");
            var warnTitle = new TextBlock { Text = L.SPAdvancedTitle, Style = Res<Style>("RowTitle") };
            warnTitle.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
            var warnHint = new TextBlock { Text = L.SPAdvancedWarn, Style = Res<Style>("RowHint") };
            warnHint.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
            var warnText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            warnText.Children.Add(warnTitle);
            warnText.Children.Add(warnHint);
            var warn = new StackPanel { Orientation = Orientation.Horizontal, MinHeight = 72 };
            warn.Children.Add(warnIcon);
            warn.Children.Add(warnText);

            spEnabled = Toggle();
            spEnabled.Checked += delegate
            {
                if (loading) return;
                if (ConfirmSuperPaste != null && ConfirmSuperPaste())
                    Edit(delegate { set.SuperPasteEnabled = true; });
                else { loading = true; spEnabled.IsChecked = false; loading = false; }
            };
            spEnabled.Unchecked += delegate { if (!loading) Edit(delegate { set.SuperPasteEnabled = false; }); };

            spStatus = new TextBlock { Text = L.SPInactive, Style = Res<Style>("RowHint") };
            var editor = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            spModifiers = new ToggleButton[4];
            string[] modifierNames = { "Ctrl", "Shift", "Alt", "Windows" };
            for (int i = 0; i < spModifiers.Length; i++)
            {
                spModifiers[i] = new ToggleButton { Content = modifierNames[i], Style = Res<Style>("Chip"),
                    Height = 32, Margin = new Thickness(0, 0, 8, 8), VerticalAlignment = VerticalAlignment.Center };
                spModifiers[i].Checked += delegate { if (!loading) ApplyPasteShortcut(); };
                spModifiers[i].Unchecked += delegate { if (!loading) ApplyPasteShortcut(); };
                editor.Children.Add(spModifiers[i]);
            }
            spMainKey = new ComboBox { Style = Res<Style>("Cmb"), Width = 140, MinWidth = 140, Height = 32, SelectedValuePath = "Tag",
                Margin = new Thickness(0, 0, 8, 8), VerticalAlignment = VerticalAlignment.Center };
            for (int key = 0; key < 256; key++)
                if (PasteHotkey.ValidKey(key)) spMainKey.Items.Add(new ComboBoxItem { Content = ((System.Windows.Forms.Keys)key).ToString(), Tag = key });
            spMainKey.SelectionChanged += delegate { if (!loading) ApplyPasteShortcut(); };
            editor.Children.Add(spMainKey);
            var shortcutCard = new StackPanel();
            shortcutCard.Children.Add(new TextBlock { Text = L.SPShortcutTitle, Style = Res<Style>("RowTitle") });
            shortcutCard.Children.Add(new TextBlock { Text = L.SPShortcutHint, Style = Res<Style>("RowHint") });
            shortcutCard.Children.Add(editor);
            spStatus.Margin = new Thickness(0, 6, 0, 0);
            shortcutCard.Children.Add(spStatus);

            spSymbols = Toggle(); BindToggle(spSymbols, v => set.SuperPasteSymbols = v);
            spTrailing = Toggle(); BindToggle(spTrailing, v => set.SuperPasteTrailing = v);
            spBlankLines = Toggle(); BindToggle(spBlankLines, v => set.SuperPasteBlankLines = v);
            spEndNewlines = Toggle(); BindToggle(spEndNewlines, v => set.SuperPasteEndNewlines = v);

            var sampleCol = new StackPanel();
            sampleCol.Children.Add(new TextBlock { Text = L.SPSample, Style = Res<Style>("RowHint") });
            spSample = new TextBlock { Text = SpSampleText, Style = Res<Style>("RowHint"), TextWrapping = TextWrapping.Wrap };
            sampleCol.Children.Add(spSample);
            var resultCol = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
            resultCol.Children.Add(new TextBlock { Text = L.SPOutput, Style = Res<Style>("RowHint") });
            spResult = new TextBlock { Style = Res<Style>("RowHint"), TextWrapping = TextWrapping.Wrap };
            resultCol.Children.Add(spResult);
            var preview = new Grid();
            preview.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            preview.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(resultCol, 1);
            preview.Children.Add(sampleCol);
            preview.Children.Add(resultCol);

            spSettings = PageOf(
                Box(shortcutCard),
                SettingCard(L.SPRuleSymbols, L.SPRuleSymbolsHint, spSymbols),
                SettingCard(L.SPRuleTrailing, L.SPRuleTrailingHint, spTrailing),
                SettingCard(L.SPRuleBlankLines, L.SPRuleBlankLinesHint, spBlankLines),
                SettingCard(L.SPRuleEndNewlines, L.SPRuleEndNewlinesHint, spEndNewlines),
                Box(preview));
            spSettings.Visibility = set.SuperPasteEnabled ? Visibility.Visible : Visibility.Collapsed;
            return PageOf(Box(warn), SettingCard(L.SuperPasteTitle, L.SPOverwrite, spEnabled), spSettings);
        }

        void ApplyPasteShortcut()
        {
            int mask = 0;
            for (int i = 0; i < spModifiers.Length; i++) if (spModifiers[i].IsChecked == true) mask |= PasteModifierBits[i];
            int vk = 0;
            var item = spMainKey.SelectedItem as ComboBoxItem;
            if (item != null && item.Tag is int) vk = (int)item.Tag;
            int mods, key;
            if (!PasteHotkey.Parse(mask + ":" + vk, out mods, out key)) { spStatus.Text = L.SPInvalidShortcut; return; }
            Edit(delegate { set.SuperPasteShortcut = mods + ":" + key; });
        }

        void ReloadShortcutEditor()
        {
            int mods = 12, key = 45;
            PasteHotkey.Parse(set.SuperPasteShortcut, out mods, out key);
            for (int i = 0; i < spModifiers.Length; i++) spModifiers[i].IsChecked = (mods & PasteModifierBits[i]) != 0;
            spMainKey.SelectedValue = key;
        }

        void ReloadSuperPaste()
        {
            if (spEnabled == null) return;
            if (spSettings != null) spSettings.Visibility = set.SuperPasteEnabled ? Visibility.Visible : Visibility.Collapsed;
            spEnabled.IsChecked = set.SuperPasteEnabled;
            ReloadShortcutEditor();
            spSymbols.IsChecked = set.SuperPasteSymbols;
            spTrailing.IsChecked = set.SuperPasteTrailing;
            spBlankLines.IsChecked = set.SuperPasteBlankLines;
            spEndNewlines.IsChecked = set.SuperPasteEndNewlines;
            spStatus.Text = SuperPasteStatus == null ? L.SPInactive : SuperPasteStatus();
            PreviewSuperPaste();
        }

        void PreviewSuperPaste()
        {
            if (spResult == null) return;
            spResult.Text = SuperPasteText.Clean(SpSampleText, set.PasteRules());
        }
    }
}
