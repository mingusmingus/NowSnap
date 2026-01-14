using System;
using System.Text;
using System.Windows;
using System.Windows.Input;
using LiveShot.API.Background;
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
            var modifiers = (ModifierKeys)Settings.Default.HotkeyModifiers;

            HotkeyButton.Content = FormatHotkeyString(modifiers, key);
        }

        private string FormatHotkeyString(ModifierKeys modifiers, Key key)
        {
            var sb = new StringBuilder();
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl + ");
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift + ");
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt + ");
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win + ");

            sb.Append(key.ToString());
            return sb.ToString();
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

            // Aggressive memory optimization when minimizing to tray
            BackgroundApplication.FlushMemory();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Cancel -> Just hide/close without saving
            this.Hide();

            // Aggressive memory optimization when minimizing to tray
            BackgroundApplication.FlushMemory();
        }

        private void HotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            HotkeyButton.Content = "Presione teclas...";
            this.KeyDown += OnHotkeyPress;
        }

        private void OnHotkeyPress(object sender, KeyEventArgs e)
        {
            // If only modifier keys are pressed, return and wait for the actual key
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin ||
                e.Key == Key.System) // System key (Alt)
            {
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;

            // Map WPF Key to Virtual Key Code
            int virtualKey = KeyInterop.VirtualKeyFromKey(key);

            Settings.Default.Hotkey = virtualKey;
            Settings.Default.HotkeyModifiers = (int)modifiers;

            HotkeyButton.Content = FormatHotkeyString(modifiers, key);

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
                        // Using explicit path logic as requested
                        key.SetValue(appName, $"\"{exePath}\" --background");
                    }
                }
                else
                {
                    if (key.GetValue(appName) != null)
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

            // Aggressive memory optimization when minimizing to tray
            BackgroundApplication.FlushMemory();
        }
    }
}
