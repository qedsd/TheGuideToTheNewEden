using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESI.NET.Models.Fleets;
using System.Windows;
using TheGuideToTheNewEden.WPF.Common;
using System.Windows.Interop;

namespace TheGuideToTheNewEden.WPF.Helpers
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


        static public List<Window> ActiveWindows { get { return _activeWindows; } }

        static private List<Window> _activeWindows = new List<Window>();

        public static Window MainWindow { get; private set; }
        public static void SetMainWindow(Window window)
        {
            MainWindow = window;
        }

        public static void CenterToScreen(Window window)
        {
            
        }

        public static void MoveToScreen(Window window, int x, int y)
        {
            
        }

        public static IntPtr GetWindowHandle(Window window)
        {
            return new WindowInteropHelper(window).Handle;
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
        
        public static void TopMost(Window window)
        {
            window.Topmost = true;
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
            var dwCurID = (int)Win32.GetCurrentThreadId();
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
            var dwCurID = (int)Win32.GetCurrentThreadId();//当前进程id
            if (!Win32.AttachThreadInput(dwCurID, dwForeID, true))
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
                        while (tryCount++ < 3)
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
            return true;
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

        /// <summary>
        /// 依据游戏角色名称获取窗口句柄
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IntPtr GetGameHwndByCharacterName(string name)
        {
            var process = System.Diagnostics.Process.GetProcessesByName("exefile");
            if (process != null && process.Any())
            {
                var targetProc = process.FirstOrDefault(p => p.MainWindowTitle.Contains(name));
                if (targetProc != null)
                {
                    if (targetProc.MainWindowHandle != IntPtr.Zero)
                    {
                        return targetProc.MainWindowHandle;
                    }
                    else
                    {
                        Core.Log.Error($"寻找{name}的窗口句柄返回空");
                    }
                }
                else
                {
                    Core.Log.Error($"无法找到{name}的游戏窗口");
                }
            }
            else
            {
                Core.Log.Error("无法找到exefile进程");
            }
            return IntPtr.Zero;
        }

        public static void TransparentWindow(IntPtr hwnd, int nOpacity = 80)
        {
            nOpacity = nOpacity > 100 ? 100 : nOpacity < 0 ? 0 : nOpacity;
            long nExStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, (IntPtr)(nExStyle | Win32.WS_EX_LAYERED));
            Win32.SetLayeredWindowAttributes(hwnd, (uint)0, (byte)(255 * nOpacity / 100), Win32.LWA_ALPHA);
        }
    }
}
