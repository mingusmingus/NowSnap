using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using LiveShot.UI.Properties;

namespace LiveShot.UI
{
    public class GlobalHotkeyService : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private IntPtr _windowHandle;
        private HwndSource? _source;
        private const int HOTKEY_ID = 9000;

        // No modifiers for simplicity, or 0
        private const uint MOD_NONE = 0x0000;

        public event Action? HotkeyPressed;

        public void Initialize(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source?.AddHook(HwndHook);

            // Initial registration
            Register();
        }

        public bool Register()
        {
            Unregister(); // Clear existing
            int vk = Settings.Default.Hotkey;
            if (vk > 0)
            {
                bool result = RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_NONE, (uint)vk);
                return result;
            }
            return false;
        }

        private void Unregister()
        {
             UnregisterHotKey(_windowHandle, HOTKEY_ID);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                if (wParam.ToInt32() == HOTKEY_ID)
                {
                    HotkeyPressed?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            _source?.RemoveHook(HwndHook);
        }
    }
}
