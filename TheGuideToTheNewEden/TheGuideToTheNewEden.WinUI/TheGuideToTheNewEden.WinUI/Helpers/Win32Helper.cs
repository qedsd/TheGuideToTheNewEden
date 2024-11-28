using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    public static class Win32Helper
    {
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(ref Point lpPoint);
        [DllImport("User32.dll", CharSet = CharSet.Auto, EntryPoint = "SetWindowLong")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("User32.dll", CharSet = CharSet.Auto, EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("User32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
        public static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("User32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
        public static extern long GetWindowLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("user32")]  
        public static extern IntPtr GetParent(IntPtr hWnd);
        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();
        /// <summary>
        /// 获取按键按下、抬起、切换状态
        /// </summary>
        /// <param name="nVirtKey">https://learn.microsoft.com/zh-cn/windows/win32/inputdev/virtual-key-codes</param>
        /// <returns></returns>

        [DllImport("user32.dll", EntryPoint = "GetKeyState")]
        public static extern int GetKeyState(int nVirtKey);

        /// <summary>
        /// 按键是否按下
        /// </summary>
        /// <param name="nVirtKey"></param>
        /// <returns></returns>
        public static bool IsKeyDown(int nVirtKey)
        {
            return ((GetKeyState(nVirtKey) & 0x8000) != 0) ? true : false;
        }
    }
}
