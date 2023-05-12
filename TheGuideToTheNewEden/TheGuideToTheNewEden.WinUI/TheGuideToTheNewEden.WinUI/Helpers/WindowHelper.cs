using ESI.NET.Models.Opportunities;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using TheGuideToTheNewEden.WinUI.Common;
using Windows.UI.WindowManagement;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    public class WindowHelper
    {
        static public Window CreateWindow()
        {
            Window newWindow = new Window();
            TrackWindow(newWindow);
            return newWindow;
        }

        static public void TrackWindow(Window window)
        {
            window.Closed += (sender, args) =>
            {
                _activeWindows.Remove(window);
            };
            _activeWindows.Add(window);
        }

        static public Window GetWindowForElement(UIElement element)
        {
            if (element.XamlRoot != null)
            {
                foreach (Window window in _activeWindows)
                {
                    if (element.XamlRoot == window.Content.XamlRoot)
                    {
                        return window;
                    }
                }
            }
            return null;
        }

        static public List<Window> ActiveWindows { get { return _activeWindows; } }

        static private List<Window> _activeWindows = new List<Window>();

        public static Window MainWindow { get; private set; }
        public static void SetMainWindow(Window window)
        {
            MainWindow = window;
        }

        public static Window CurrentWindow()
        {
            return Window.Current;
        }

        public static void CenterToScreen(Window window)
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow is not null)
            {
                Microsoft.UI.Windowing.DisplayArea displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
                if (displayArea is not null)
                {
                    var CenteredPosition = appWindow.Position;
                    CenteredPosition.X = ((displayArea.WorkArea.Width - appWindow.Size.Width) / 2);
                    CenteredPosition.Y = ((displayArea.WorkArea.Height - appWindow.Size.Height) / 2);
                    appWindow.Move(CenteredPosition);
                }
            }
        }

        public static void MoveToScreen(Window window,int x, int y)
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow is not null)
            {
                var CenteredPosition = appWindow.Position;
                CenteredPosition.X = x;
                CenteredPosition.Y = y;
                appWindow.Move(CenteredPosition);
            }
        }

        public static IntPtr GetWindowHandle(Window window)
        {
            return WinRT.Interop.WindowNative.GetWindowHandle(window);
        }
        public static Microsoft.UI.Windowing.AppWindow GetAppWindow(Window window)
        {
            var hWnd = GetWindowHandle(window);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        }
        public static OverlappedPresenter GetOverlappedPresenter(Window window)
        {
            return GetAppWindow(window).Presenter as OverlappedPresenter;
        }

        public static System.Drawing.Rectangle GetClientRect(IntPtr hwnd)
        {
            var clientRect = new System.Drawing.Rectangle();
            Win32.GetClientRect(hwnd, ref clientRect);
            return clientRect;
        }

        public static System.Drawing.Rectangle GetWindowRect(IntPtr hwnd)
        {
            var windowRect = new System.Drawing.Rectangle();
            Win32.GetWindowRect(hwnd, ref windowRect);
            return windowRect;
        }
        /// <summary>
        /// 标题栏高度
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        public static int GetTitleBarHeight(IntPtr hwnd)
        {
            var windowRect = new System.Drawing.Rectangle();
            Win32.GetWindowRect(hwnd, ref windowRect);
            var clientRect = new System.Drawing.Rectangle();
            Win32.GetClientRect(hwnd, ref clientRect);
            System.Drawing.Point point = new System.Drawing.Point();
            Win32.ClientToScreen(hwnd, ref point);
            return point.Y - windowRect.Top;
        }

        /// <summary>
        /// 边框宽度
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        public static int GetBorderWidth(IntPtr hwnd)
        {
            var windowRect = new System.Drawing.Rectangle();
            Win32.GetWindowRect(hwnd, ref windowRect);
            System.Drawing.Point point = new System.Drawing.Point();
            Win32.ClientToScreen(hwnd, ref point);
            return point.X - windowRect.Left;
        }

        public static void HideTitleBar(Window window)
        {
            var presenter = Helpers.WindowHelper.GetOverlappedPresenter(window);
            presenter.IsAlwaysOnTop = true;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        /// <summary>
        /// 获取所有屏幕分辨率
        /// 如果是多个屏幕，会自动按排布方式扩展
        /// </summary>
        /// <param name="w"></param>
        /// <param name="h"></param>
        public static void GetAllScreenSize(out int w, out int h)
        {
            w = Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN);
            h = Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN);
        }

        public static void SetForegroundWindow(IntPtr targetHandle)
        {
            Core.Log.Debug($"激活窗口{targetHandle}");
            var hForeWnd = Win32.GetForegroundWindow();
            var dwCurID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var dwForeID = Win32.GetWindowThreadProcessId(hForeWnd, out _);
            Win32.AttachThreadInput(dwCurID, dwForeID, true);
            if (Win32.IsIconic(targetHandle))
            {
                Win32.ShowWindowAsync(targetHandle, 1);
            }
            else
            {
                Win32.ShowWindowAsync(targetHandle, 8);
            }
            Win32.SetForegroundWindow(targetHandle);
            Win32.SetWindowPos(targetHandle, 0, 0, 0, 0, 0, 1 | 2);
            Win32.AttachThreadInput(dwCurID, dwForeID, false);


            //EVE-O
            //Win32.SetForegroundWindow(targetHandle);
            //int style = Win32.GetWindowLong(targetHandle, Win32.GWL_STYLE);
            //if ((style & Win32.WS_MINIMIZE) == Win32.WS_MINIMIZE)
            //{
            //    Win32.ShowWindowAsync(_sourceHtargetHandleWnd, Win32.SW_RESTORE);
            //}
        }

        /// <summary>
        /// 指定xy位置是否在显示范围内
        /// 支持多屏幕
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static bool IsInWindow(int x, int y)
        {
            return DisplayArea.GetFromPoint(new Windows.Graphics.PointInt32(x, y), DisplayAreaFallback.None) != null;
        }
        /// <summary>
        /// 获取屏幕缩放比例
        /// eg：1.25
        /// </summary>
        /// <param name="window"></param>
        /// <returns></returns>
        public static float GetDpiScale(Window window)
        {
            return Win32.GetDpiForWindow(GetWindowHandle(window)) / 96f;
        }
    }
}
