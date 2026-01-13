using System;
using System.Drawing;
using System.Windows.Forms;
using LiveShot.API.Events.Capture;
using LiveShot.UI.Properties;
using LiveShot.UI.Views;
using Application = System.Windows.Application;

namespace LiveShot.UI
{
    public class SystemTrayService : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private readonly Action _onCapture;
        private readonly Action _onExit;
        private readonly Action _onSettings;

        public SystemTrayService(Action onCapture, Action onSettings, Action onExit)
        {
            _onCapture = onCapture;
            _onSettings = onSettings;
            _onExit = onExit;
        }

        public void Initialize()
        {
            _notifyIcon = new NotifyIcon();

            // Load icon
            // AssemblyName is LiveShot, so URI authority should be LiveShot
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/LiveShot;component/bg-icon.ico"));
            if (streamInfo != null)
            {
                _notifyIcon.Icon = new Icon(streamInfo.Stream);
            }
            else
            {
                // Fallback to system icon if resource fails
                _notifyIcon.Icon = SystemIcons.Application;
            }

            _notifyIcon.Visible = true;
            _notifyIcon.Text = "LiveShot";
            _notifyIcon.DoubleClick += (s, e) => _onCapture();

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Capturar", null, (s, e) => _onCapture());
            contextMenu.Items.Add("Configuración", null, (s, e) => _onSettings());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Salir", null, (s, e) => _onExit());

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
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
