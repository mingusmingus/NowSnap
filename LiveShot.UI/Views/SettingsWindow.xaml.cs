using System;
using System.Windows;
using System.Windows.Input;
using LiveShot.UI.Properties;
using Microsoft.Win32;

namespace LiveShot.UI.Views
{
    public partial class SettingsWindow : Window
    {
        public event EventHandler? SettingsSaved;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            StartWithWindowsCheckBox.IsChecked = Settings.Default.StartWithWindows;

            var key = KeyInterop.KeyFromVirtualKey(Settings.Default.Hotkey);
            HotkeyButton.Content = key.ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            Settings.Default.Save();

            SetStartup(Settings.Default.StartWithWindows);

            // Notify listeners (App.xaml.cs) to update hotkeys
            SettingsSaved?.Invoke(this, EventArgs.Empty);

            // Hide instead of close to mimic "minimize to tray"
            this.Hide();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Cancel -> Just hide/close without saving
            this.Hide();
        }

        private void HotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            HotkeyButton.Content = "Presione una tecla...";
            this.KeyDown += OnHotkeyPress;
        }

        private void OnHotkeyPress(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Map WPF Key to Virtual Key Code
            int virtualKey = KeyInterop.VirtualKeyFromKey(key);

            Settings.Default.Hotkey = virtualKey;
            HotkeyButton.Content = key.ToString();

            this.KeyDown -= OnHotkeyPress;
            e.Handled = true;
        }

        private void SetStartup(bool enable)
        {
            const string runRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(runRegistryKey, true);
                if (key == null) return;

                string appName = "LiveShot";
                if (enable)
                {
                    string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (exePath != null)
                    {
                        // Ensure we pass --background so it starts silently
                        key.SetValue(appName, $"\"{exePath}\" --background");
                    }
                }
                else
                {
                    key.DeleteValue(appName, false);
                }
            }
            catch (Exception)
            {
                // Access denied or other error
                MessageBox.Show("No se pudo guardar la configuración de inicio automático.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // The prompt: "Al cerrar la ventana principal (X), la aplicación NO debe cerrarse, sino minimizarse a la bandeja del sistema."
            e.Cancel = true;
            this.Hide();
        }
    }
}
