using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Common;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    internal class TitleBarHelper
    {
        public static void TriggerTitleBarRepaint(Window window)
        {
            // to trigger repaint tracking task id 38044406
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var activeWindow = Win32.GetActiveWindow();
            if (hwnd == activeWindow)
            {
                Win32.SendMessage(hwnd, Win32.WM_ACTIVATE, Win32.WA_INACTIVE, IntPtr.Zero);
                Win32.SendMessage(hwnd, Win32.WM_ACTIVATE, Win32.WA_ACTIVE, IntPtr.Zero);
            }
            else
            {
                Win32.SendMessage(hwnd, Win32.WM_ACTIVATE, Win32.WA_ACTIVE, IntPtr.Zero);
                Win32.SendMessage(hwnd, Win32.WM_ACTIVATE, Win32.WA_INACTIVE, IntPtr.Zero);
            }

        }
    }
}
