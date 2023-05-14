using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Common

{
    internal static class Win32
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(IntPtr moduleName);

        public const int WM_ACTIVATE = 0x0006;
        public const int WA_ACTIVE = 0x01;
        //static int WA_CLICKACTIVE = 0x02;
        public const int WA_INACTIVE = 0x00;

        public const int WM_SETICON = 0x0080;
        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;

        public const UInt32 WS_MINIMIZE = 0x20000000;
        public const int GWL_STYLE = (-16);
        public const int SW_RESTORE = 9;

        [DllImport("User32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        /// <summary>
        /// 显示窗口操作
        /// https://learn.microsoft.com/zh-cn/windows/win32/api/winuser/nf-winuser-showwindow
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="nCmdShow">
        /// 0 关闭窗口
        /// 1 正常大小显示窗口
        /// 2 最小化窗口
        /// 3 最大化窗口
        /// 4 在其最近的大小和位置显示一个窗口
        /// 5 激活窗口并以当前大小和位置显示窗口
        /// </param>
        /// <returns></returns>
        [DllImport("User32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        /// <summary>
        /// 最小化
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        [DllImport("User32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);
        /// <summary>
        /// 最大化
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowRect(IntPtr hWnd, ref System.Drawing.Rectangle rect);
        [DllImport("user32.dll")]
        public static extern IntPtr GetClientRect(IntPtr hWnd, ref System.Drawing.Rectangle rect);
        [DllImport("user32.dll")]
        public static extern IntPtr ClientToScreen(IntPtr hWnd, ref System.Drawing.Point point);

        /// <summary>
        /// 虚拟屏幕的宽度（以像素为单位）
        /// </summary>
        public const int SM_CXVIRTUALSCREEN = 78;
        /// <summary>
        /// 虚拟屏幕的高度（以像素为单位）
        /// </summary>
        public const int SM_CYVIRTUALSCREEN = 79;
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        /// <summary>
        /// 返回当前活动窗口句柄
        /// </summary>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();


        /// <summary>
        /// 返回创建指定窗口线程的标识和创建窗口的进程的标识符
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="ID">可选</param>
        /// <returns></returns>
        [DllImport("User32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hwnd, out int ID);

        /// <summary>
        /// 将一个线程的输入处理机制附加到另一个线程的输入处理机制
        /// </summary>
        /// <param name="idAttach">要附加到另一个线程的线程的标识符</param>
        /// <param name="idAttachTo">将附加到的线程的标识符</param>
        /// <param name="fAttach">如果此参数为 TRUE，则附加两个线程。 如果参数为 FALSE，则分离线程</param>
        /// <returns></returns>
        [DllImport("User32.dll")]
        public static extern bool AttachThreadInput(int idAttach, int idAttachTo, bool fAttach);

        /// <summary>
        /// 将指定的窗口置于 Z 顺序的顶部。 如果窗口是顶级窗口，则会激活该窗口。
        /// 如果窗口是子窗口，则会激活与子窗口关联的顶级父窗口。
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        [DllImport("User32.dll")]
        public static extern bool BringWindowToTop(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// 获取指定窗口dpi缩放
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, int hWndlnsertAfter, int X, int Y, int cx, int cy, uint Flags);

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);
    }
}
