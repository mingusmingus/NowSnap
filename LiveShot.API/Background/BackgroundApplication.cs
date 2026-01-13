using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using LiveShot.API.Properties;

namespace LiveShot.API.Background
{
    public class BackgroundApplication : IBackgroundApplication
    {
        private NotifyIcon? _notifyIcon;
        private readonly Action _onCapture;
        private readonly Action _onExit;
        private readonly Action _onSettings;

        // Inyección de dependencias si fuera necesario, o acciones pasadas por constructor
        public BackgroundApplication()
        {
            // Constructor vacío por compatibilidad con DI si no se inyectan acciones
            _onCapture = () => { };
            _onSettings = () => { };
            _onExit = () => { };
        }

        public void Run()
        {
            // Implementación básica del System Tray desde API
            // Nota: Esta clase ahora cumple con la interfaz requerida por Container.cs
            // La lógica real de UI (Ventanas, Hotkeys) suele estar en la capa UI (App.xaml.cs)
            // Pero si se requiere mover aquí, se necesitaría referencia cruzada o inversión de control.

            // Inicialización de Tray Icon (si se desea manejar desde aquí)
            InitializeTray();
        }

        private void InitializeTray()
        {
            _notifyIcon = new NotifyIcon();

            try
            {
                // Intentar cargar icono desde recursos incrustados
                // Como estamos en API, el acceso a recursos WPF (pack://) es complicado sin referencias.
                _notifyIcon.Icon = SystemIcons.Application;
                _notifyIcon.Text = Resources.NotifyIcon_Title; // "LiveShot"
                _notifyIcon.Visible = true;

                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add(Resources.ContextMenu_CaptureScreenShot_Title, null, (s, e) => _onCapture());
                contextMenu.Items.Add(Resources.ContextMenu_Configuration_Title, null, (s, e) => _onSettings());
                contextMenu.Items.Add("-");
                contextMenu.Items.Add(Resources.ContextMenu_Exit_Title, null, (s, e) => _onExit());

                _notifyIcon.ContextMenuStrip = contextMenu;
            }
            catch (Exception)
            {
                // Fallback silencioso
            }
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }
    }
}
