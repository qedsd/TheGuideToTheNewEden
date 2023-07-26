using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinCore
{
    public static class WindowHelper
    {
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


        public static void SetForegroundWindow2(IntPtr targetHandle)
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
    }
}
