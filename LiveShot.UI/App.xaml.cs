using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using LiveShot.API;
using LiveShot.API.Events.Application;
using LiveShot.API.Events.Capture;
using LiveShot.API.Utils;
using LiveShot.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace LiveShot.UI
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private IServiceProvider? ServiceProvider { get; set; }
        private IConfiguration? Configuration { get; set; }
        private CaptureScreenView? CaptureScreenView { get; set; }

        private SystemTrayService? _trayService;
        private GlobalHotkeyService? _hotkeyService;
        private Window? _hiddenWindow; // Needed for Hotkeys if main window is closed

        protected override void OnStartup(StartupEventArgs e)
        {
            // Ensure we don't shutdown when the last window closes, because we want to stay in tray
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            LoadConfiguration();
            SetUICulture();

            var serviceCollection = new ServiceCollection()
                .ConfigureAPI(Configuration)
                .ConfigureUI();

            if (Configuration != null)
                serviceCollection.AddSingleton(Configuration);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Initialize Tray and Hotkeys
            InitializeResidentComponents();

            // Check if we should start silently or show capture immediately
            bool isBackgroundStart = e.Args.Contains("--background");

            if (!isBackgroundStart)
            {
                // If not started with --background (manual launch), check user preference
                StartCaptureScreenShot();
            }
        }

        private void InitializeResidentComponents()
        {
            // 1. Create a hidden window to handle Hotkey messages if needed
            _hiddenWindow = new Window
            {
                Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Visibility = Visibility.Hidden
            };
            _hiddenWindow.Show(); // Must show to get Handle, but it's hidden.

            // 2. Setup Hotkeys
            var helper = new WindowInteropHelper(_hiddenWindow);
            _hotkeyService = new GlobalHotkeyService();
            _hotkeyService.HotkeyPressed += () => Dispatcher.Invoke(StartCaptureScreenShot);
            _hotkeyService.Initialize(helper.Handle);

            // Register hotkey and warn if fails
            if (!_hotkeyService.Register())
            {
                 // Don't use MessageBox here potentially, but tray balloon
            }

            // 3. Setup Tray
            _trayService = new SystemTrayService(
                onCapture: () => Dispatcher.Invoke(StartCaptureScreenShot),
                onSettings: () => Dispatcher.Invoke(OpenSettings),
                onExit: () => Dispatcher.Invoke(ExitApplication)
            );
            _trayService.Initialize();

            // Re-check registration to warn user via tray
            if (!_hotkeyService.Register())
            {
                _trayService.ShowNotification("Error", "No se pudo registrar el atajo de teclado. Puede estar en uso por otra aplicación.", ToolTipIcon.Warning);
            }

            // 4. Subscribe to Event Pipeline (legacy support)
            var eventPipeline = ServiceProvider?.GetRequiredService<IEventPipeline>();
            eventPipeline?.Subscribe<CaptureScreenShotEvent>(_ => Dispatcher.Invoke(StartCaptureScreenShot));
            eventPipeline?.Subscribe<ShutdownApplicationEvent>(_ => Dispatcher.Invoke(ExitApplication));
        }

        private void OpenSettings()
        {
            // Check if already open
            var settings = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
            if (settings != null)
            {
                settings.Activate();
                if (settings.WindowState == WindowState.Minimized)
                    settings.WindowState = WindowState.Normal;

                settings.Show(); // Ensure it's shown if hidden
                return;
            }

            settings = new SettingsWindow();
            settings.SettingsSaved += (s, e) =>
            {
                // Update hotkeys immediately when settings are saved
                if (_hotkeyService != null && !_hotkeyService.Register())
                {
                    _trayService?.ShowNotification("Error", "El nuevo atajo no se pudo registrar.", ToolTipIcon.Warning);
                }
            };
            settings.Show();
        }

        private void StartCaptureScreenShot()
        {
            if (WindowUtils.IsOpen(typeof(CaptureScreenView))) return;

            // To be safe, we always get a fresh instance if the old one is gone.
            if (CaptureScreenView == null || !CaptureScreenView.IsLoaded)
            {
                 CaptureScreenView = ServiceProvider?.GetRequiredService<CaptureScreenView>();

                 // Ensure we nullify it when it closes so we know to recreate it
                 if (CaptureScreenView != null)
                 {
                     CaptureScreenView.Closed += (s, e) => CaptureScreenView = null;
                 }
            }

            if (CaptureScreenView != null)
            {
                CaptureScreenView.CaptureScreen();
                CaptureScreenView.Show();
                CaptureScreenView.Activate();
                CaptureScreenView.Focus();
            }
        }

        private void ExitApplication()
        {
            _trayService?.Dispose();
            _hotkeyService?.Dispose();
            Current.Shutdown();
        }

        private void SetUICulture()
        {
            string? culture = Configuration?["CultureUI"];

            if (culture is null)
            {
                culture = "es-ES";
            }

            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
        }

        private void LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
          .SetBasePath(AppDomain.CurrentDomain.BaseDirectory) 
          .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            Configuration = builder.Build();
        }
    }
}
