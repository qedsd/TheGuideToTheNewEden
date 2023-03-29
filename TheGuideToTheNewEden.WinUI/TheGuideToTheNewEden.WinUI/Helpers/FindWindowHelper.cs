using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    public class WindowInfo
    {
        public IntPtr hwnd;
        public string Title;
    }
    internal static class FindWindowHelper
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
        [DllImport("User32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int nMaxCount);
        [DllImport("user32.dll")]
        public static extern IntPtr GetLastActivePopup(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32")]
        public static extern int EnumWindows(CallBack x, int y);
        public delegate bool CallBack(IntPtr hwnd, int lParam);
        
        private static bool IsAltTabWindow(IntPtr hwnd)
        {
            // Start at the root owner
            IntPtr hwndWalk = GetAncestor(hwnd, 3);
            // See if we are the last active visible popup
            IntPtr hwndTry;
            while ((hwndTry = GetLastActivePopup(hwndWalk)) != hwndTry)
            {
                if (IsWindowVisible(hwndTry)) break;
                hwndWalk = hwndTry;
            }
            return hwndWalk == hwnd;
        }
        private static string GetWindowTitle(IntPtr hWnd)
        {
            int length = GetWindowTextLength(hWnd);
            StringBuilder windowName = new StringBuilder(length + 1);
            GetWindowText(hWnd, windowName, windowName.Capacity);
            return windowName.ToString();
        }

        public static async Task<List<WindowInfo>> EnumWindowsAsync(int waitTime = 1000)
        {
            if(windowInfos == null)
            {
                windowInfos = new Queue<WindowInfo>();
            }
            else
            {
                windowInfos.Clear();
            }
            EnumWindows(EnumWindowsCallback, 0);
            await Task.Delay(waitTime);
            return windowInfos.ToList();
        }
        private static Queue<WindowInfo> windowInfos;
        public static bool EnumWindowsCallback(IntPtr hwnd, int lParam)
        {
            if (IsAltTabWindow(hwnd))
            {
                string title = GetWindowTitle(hwnd);
                if (!string.IsNullOrEmpty(title))
                {
                    windowInfos.Enqueue(new WindowInfo() { hwnd = hwnd, Title = title });
                }
            }
            return true;
        }
    }
}
