using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Cccc
{
    sealed partial class Customize
    {
        UIElement BuildConfigControls()
        {
            var export = new Button { Content = L.ExportConfig, Style = Res<Style>("Subtle"), Margin = new Thickness(0, 0, 8, 0) };
            var import = new Button { Content = L.ImportConfig, Style = Res<Style>("Subtle") };
            export.Click += delegate { ExportSettings(); };
            import.Click += delegate { ImportSettings(); };
            var buttons = new WrapPanel();
            buttons.Children.Add(export); buttons.Children.Add(import);
            return SettingCard(L.ConfigTitle, L.ConfigHint, buttons);
        }

        void ExportSettings()
        {
            var dialog = new SaveFileDialog { Filter = "CCCC settings (*.json)|*.json", DefaultExt = ".json", AddExtension = true,
                FileName = "CCCC-settings-" + DateTime.Now.ToString("yyyy-MM-dd") + ".json" };
            if (dialog.ShowDialog(w) != true) return;
            try
            {
                ConfigFile.Write(dialog.FileName, set);
                MessageBox.Show(w, L.ConfigExported, "CCCC", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception e) { ConfigError(e); }
        }

        void ImportSettings()
        {
            var dialog = new OpenFileDialog { Filter = "CCCC settings (*.json)|*.json", CheckFileExists = true, Multiselect = false };
            if (dialog.ShowDialog(w) != true) return;
            try
            {
                ConfigPreview preview = ConfigFile.Read(dialog.FileName, set);
                if (MessageBox.Show(w, L.ConfigConfirm, "CCCC", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                string backup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CCCC", "settings-before-import.json");
                ConfigFile.Import(preview, set, backup, changed);
                Reload();
                string message = L.ConfigImported + "\n" + backup;
                if (preview.Warnings.Count > 0) message += "\n\n" + L.ConfigFallback + "\n" + string.Join(", ", preview.Warnings);
                MessageBox.Show(w, message, "CCCC", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception e) { ConfigError(e); }
        }

        void ConfigError(Exception e)
        {
            MessageBox.Show(w, L.ConfigFailed + "\n" + e.Message, "CCCC", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
