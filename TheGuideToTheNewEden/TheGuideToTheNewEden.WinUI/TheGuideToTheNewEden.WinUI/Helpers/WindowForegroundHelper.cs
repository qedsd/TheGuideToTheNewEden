using System;
using System.Runtime.InteropServices;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    /// <summary>
    /// 基于按键事件的窗口前台激活辅助类，解决 SetForegroundWindow 在某些情况下失效的问题。
    /// </summary>
    public static class ForegroundHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// 发送 Alt 键（按下+弹起）
        /// </summary>
        public static void PressAlt()
        {
            var inputs = new INPUT[2];

            // 按下 Alt
            inputs[0].type = 1; // INPUT_KEYBOARD
            inputs[0].wVk = 0x12; // VK_MENU
            inputs[0].dwFlags = 0x0001; // KEYEVENTF_EXTENDEDKEY

            // 松开 Alt
            inputs[1].type = 1;
            inputs[1].wVk = 0x12;
            inputs[1].dwFlags = 0x0001 | 0x0002; // EXTENDEDKEY | KEYUP

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// 激活指定窗口
        /// </summary>
        public static void ActivateWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            PressAlt();                    // 模拟 Alt 键
            SetForegroundWindow(hWnd);     // 激活窗口

            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }
        }
    }
}
