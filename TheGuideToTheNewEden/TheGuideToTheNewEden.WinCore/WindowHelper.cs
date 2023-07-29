using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
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
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();
        public static void SetForegroundWindow(IntPtr targetHandle)
        {
            IntPtr curForegroundWindow = Win32.GetForegroundWindow();
            Core.Log.Debug($"激活窗口 {targetHandle}({curForegroundWindow})");
            var dwForeID = Win32.GetWindowThreadProcessId(curForegroundWindow, out _);
            var dwCurID = (int)GetCurrentThreadId();
            if (!Win32.AttachThreadInput(dwCurID, dwForeID, true))
            {
                Core.Log.Debug($"AttachThreadInput失败：{dwCurID}->{dwForeID}");
                return;
            }
            int tryCount = 0;
            keybd_event((byte)0xA4, 0x45, 0x1 | 0x2, 0);
            while (tryCount++ < 3)
            {
                if (Win32.GetForegroundWindow() != targetHandle && Win32.SetForegroundWindow(targetHandle))
                {
                    if (Win32.GetForegroundWindow() != targetHandle)
                    {
                        Core.Log.Debug($"SetForegroundWindow成功但未生效（{tryCount}）");
                    }
                    else
                    {
                        Core.Log.Debug($"SetForegroundWindow成功且生效（{tryCount}）");
                        tryCount = 0;
                        while (tryCount < 3)
                        {
                            if (Win32.BringWindowToTop(targetHandle))
                            {
                                Win32.SetFocus(targetHandle);
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

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
        [DllImport("User32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        public static void SetForegroundWindow3(IntPtr targetHandle)
        {
            keybd_event(0, 0, 0, 0);
            //if (Win32.GetFocus() == targetHandle)
            //{
            //    Core.Log.Debug("已获取到焦点");
            //}
            //else
            //{
            //    int tryCount = 0;
            //    while (tryCount++ < 3)
            //    {
            //        if (Win32.SetFocus(targetHandle) != IntPtr.Zero)
            //        {
            //            Core.Log.Debug("已设置焦点");
            //            break;
            //        }
            //        Core.Log.Debug($"设置焦点失败{tryCount}");
            //    }
            //}
            if (Win32.SetForegroundWindow(targetHandle))
            {
                Core.Log.Debug("SetForegroundWindow成功");
            }
            else
            {
                Core.Log.Debug("SetForegroundWindow失败");
            }
            int style = Win32.GetWindowLong(targetHandle, -16);

            if ((style & 0x20000000) == 0x20000000)
            {
                if (Win32.ShowWindowAsync(targetHandle, 9))
                {
                    Core.Log.Debug("ShowWindowAsync成功");
                }
                else
                {
                    Core.Log.Debug("ShowWindowAsync失败");
                }
            }
            Win32.SetFocus(targetHandle);
        }

        public static void SetForegroundWindow4(IntPtr targetHandle)
        {
            IntPtr curForegroundWindow = Win32.GetForegroundWindow();
            Core.Log.Debug($"激活窗口 {targetHandle}({curForegroundWindow})");
            var dwForeID = Win32.GetWindowThreadProcessId(curForegroundWindow, out _);
            var dwCurID = (int)GetCurrentThreadId();
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
    }
}
