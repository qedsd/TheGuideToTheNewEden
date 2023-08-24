using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
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
            window.ExtendsContentIntoTitleBar = false;
            var presenter = Helpers.WindowHelper.GetOverlappedPresenter(window);
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
        public static void TopMost(Window window)
        {
            var presenter = Helpers.WindowHelper.GetOverlappedPresenter(window);
            presenter.IsAlwaysOnTop = true;
        }
        /// <summary>
        /// 使用win32让窗口完全没有边框，包括圆角阴影，不可拖动
        /// </summary>
        /// <param name="window"></param>
        public static void HideTitleBar2(Window window)
        {
            var hWnd = GetWindowHandle(window);
            var styleCurrentWindowStandard = Win32Helper.GetWindowLongPtr(hWnd, -16);
            var styleNewWindowStandard = styleCurrentWindowStandard & ~(0x00040000 | 0xc00000 | 0x80000 | 0x10000 | 0x20000);
            if (Win32Helper.SetWindowLongPtr(hWnd, -16, (IntPtr)styleNewWindowStandard) == IntPtr.Zero)
            {
                //fail
            }
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

        public static void SetForegroundWindow_Click(IntPtr targetHandle)
        {
            if (Win32.IsIconic(targetHandle))
            {
                Win32.ShowWindow(targetHandle, 4);
            }
            else
            {
                Win32.ShowWindow(targetHandle, 5);
            }
            Win32.SetForegroundWindow(targetHandle);
        }

        /// <summary>
        /// EVE-O-P
        /// </summary>
        /// <param name="targetHandle"></param>
        public static void SetForegroundWindow1(IntPtr targetHandle)
        {
            Win32.SetForegroundWindow(targetHandle);
            Win32.SetFocus(targetHandle);
            int style = Win32.GetWindowLong(targetHandle, -16);
            if ((style & 0x20000000) == 0x20000000)
            {
                Win32.ShowWindowAsync(targetHandle, 9);
            }
        }

        /// <summary>
        /// 2.5.0.0
        /// </summary>
        /// <param name="targetHandle"></param>
        public static void SetForegroundWindow2(IntPtr targetHandle)
        {
            IntPtr curForegroundWindow = Win32.GetForegroundWindow();
            Core.Log.Debug($"激活窗口 {targetHandle}({curForegroundWindow})");
            var dwForeID = Win32.GetWindowThreadProcessId(curForegroundWindow, out _);
            var dwCurID = (int)Win32Helper.GetCurrentThreadId();
            if (dwForeID != dwCurID)
            {
                Win32.AttachThreadInput(dwForeID, dwCurID, true);
                Win32.BringWindowToTop(targetHandle);
                if (Win32.IsIconic(targetHandle))
                {
                    Win32.ShowWindow(targetHandle, 4);
                }
                else
                {
                    Win32.ShowWindow(targetHandle, 5);
                }
                Win32.AttachThreadInput(dwForeID, dwCurID, false);
            }
            else
            {
                Win32.BringWindowToTop(targetHandle);
                if (Win32.IsIconic(targetHandle))
                {
                    Win32.ShowWindow(targetHandle, 4);
                }
                else
                {
                    Win32.ShowWindow(targetHandle, 5);
                }
            }
        }

        /// <summary>
        /// 原始版本
        /// </summary>
        /// <param name="targetHandle"></param>
        public static void SetForegroundWindow3(IntPtr targetHandle)
        {
            IntPtr curForegroundWindow = Win32.GetForegroundWindow();
            Core.Log.Debug($"激活窗口 {targetHandle}({curForegroundWindow})");
            var dwCurID = Win32.GetWindowThreadProcessId(curForegroundWindow, out _);
            var dwForeID = Win32.GetWindowThreadProcessId(targetHandle, out _);
            if (!Win32.AttachThreadInput(dwForeID, dwCurID, true))
            {
                Core.Log.Debug($"AttachThreadInput失败：{dwForeID}->{dwCurID}");
                return;
            }
            int tryCount = 0;
            while (tryCount++ < 3)
            {
                if (Win32.SetForegroundWindow(targetHandle))
                {
                    if (Win32.GetForegroundWindow() != targetHandle)
                    {
                        Core.Log.Debug($"SetForegroundWindow成功但未生效（{tryCount}）");
                        Thread.Sleep(100);
                    }
                    else
                    {
                        tryCount = 0;
                        while (tryCount < 3)
                        {
                            if (Win32.BringWindowToTop(targetHandle))
                            {
                                if (Win32.AttachThreadInput(dwForeID, dwCurID, false))
                                {
                                    Core.Log.Debug($"成功激活窗口{targetHandle}");
                                    break;
                                }
                                else
                                {
                                    Core.Log.Debug($"解除AttachThreadInput失败");
                                }
                            }
                            else
                            {
                                Core.Log.Debug($"BringWindowToTop失败（{tryCount}）");
                            }
                        }
                        break;
                    }
                }
                else
                {
                    Core.Log.Debug($"SetForegroundWindow失败（{tryCount}）");
                }
            }
        }

        /// <summary>
        /// 原始版本修正
        /// </summary>
        /// <param name="targetHandle"></param>
        public static void SetForegroundWindow4(IntPtr targetHandle)
        {
            IntPtr curForegroundWindow = Win32.GetForegroundWindow();
            Core.Log.Debug($"激活窗口 {targetHandle}({curForegroundWindow})");
            var dwForeID = Win32.GetWindowThreadProcessId(curForegroundWindow, out _);//当前前台窗口进程id
            var dwCurID = (int)Win32Helper.GetCurrentThreadId();//当前进程id
            if (!Win32.AttachThreadInput(dwCurID, dwForeID,true))
            {
                Core.Log.Debug($"AttachThreadInput失败：{dwCurID}->{dwForeID}");
                return;
            }
            int tryCount = 0;
            while (tryCount++ < 3)
            {
                if (Win32.SetForegroundWindow(targetHandle))
                {
                    if (Win32.GetForegroundWindow() != targetHandle)
                    {
                        Core.Log.Debug($"SetForegroundWindow成功但未生效（{tryCount}）");
                    }
                    else
                    {
                        tryCount = 0;
                        while (tryCount < 3)
                        {
                            if (Win32.BringWindowToTop(targetHandle))
                            {
                                if (Win32.AttachThreadInput(dwCurID, dwForeID, false))
                                {
                                    Core.Log.Debug($"成功激活窗口{targetHandle}");
                                    break;
                                }
                                else
                                {
                                    Core.Log.Debug($"解除AttachThreadInput失败");
                                }
                            }
                            else
                            {
                                Core.Log.Debug($"BringWindowToTop失败（{tryCount}）");
                            }
                        }
                        break;
                    }
                }
                else
                {
                    Core.Log.Debug($"SetForegroundWindow失败（{tryCount}）");
                }
            }
        }

        /// <summary>
        /// 键盘事件
        /// </summary>
        /// <param name="targetHandle"></param>
        public static void SetForegroundWindow5(IntPtr targetHandle)
        {
            Win32.keybd_event(0, 0, 0, 0);//SetForegroundWindow条件之一：The calling process received the last input event.
            SetForegroundWindow_Click(targetHandle);
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
