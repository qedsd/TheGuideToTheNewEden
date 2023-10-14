using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Composition;
using WinRT;
using System.Drawing;
using Vanara.PInvoke;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    public static class TransparentWindowHelper
    {
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const uint LWA_ALPHA = 0x00000002;

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        const int GWL_EXSTYLE = (-20);
        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 4)
            {
                return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
            }
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        }

        [DllImport("User32.dll", CharSet = CharSet.Auto, EntryPoint = "SetWindowLong")]
        public static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("User32.dll", CharSet = CharSet.Auto, EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static long GetWindowLong(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 4)
            {
                return GetWindowLong32(hWnd, nIndex);
            }
            return GetWindowLongPtr64(hWnd, nIndex);
        }

        [DllImport("User32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
        public static extern long GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("User32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
        public static extern long GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        /// <summary>
        /// 使目标window透明
        /// </summary>
        /// <param name="window">目标window</param>
        /// <param name="nOpacity">透明度 0-100</param>
        public static void TransparentWindow(Window window,int nOpacity = 80)
        {
            nOpacity = nOpacity > 100 ? 100 : nOpacity < 0 ? 0 : nOpacity;

            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow apw = AppWindow.GetFromWindowId(myWndId);
            long nExStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, (IntPtr)(nExStyle | WS_EX_LAYERED));
            SetLayeredWindowAttributes(hWnd, (uint)0, (byte)(255 * nOpacity / 100), LWA_ALPHA);
        }


        #region 仅背景透明，内容不透明
        public static void TransparentWindowVisual(Window window)
        {
            using var rgn = Gdi32.CreateRectRgn(-2, -2, -1, -1);
            DwmApi.DwmEnableBlurBehindWindow(WindowHelper.GetWindowHandle(window), new DwmApi.DWM_BLURBEHIND(true)
            {
                dwFlags = DwmApi.DWM_BLURBEHIND_Mask.DWM_BB_ENABLE | DwmApi.DWM_BLURBEHIND_Mask.DWM_BB_BLURREGION,
                hRgnBlur = rgn
            });
            SetTransparent(window, true);
        }

        private static void SetTransparent(Window window, bool isTransparent)
        {
            var brushHolder = window.As<ICompositionSupportsSystemBackdrop>();

            if (isTransparent)
            {
                var colorBrush = WindowsCompositionHelper.Compositor.CreateColorBrush(Windows.UI.Color.FromArgb(0, 255, 255, 255));
                brushHolder.SystemBackdrop = colorBrush;
            }
            else
            {
                brushHolder.SystemBackdrop = null;
            }
        }
        public static unsafe IntPtr WndProc(HWND hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == (uint)User32.WindowMessage.WM_ERASEBKGND)
            {
                if (User32.GetClientRect(hWnd, out var rect))
                {
                    using var brush = Gdi32.CreateSolidBrush(new COLORREF(0, 0, 0));
                    User32.FillRect(wParam, rect, brush);
                    return new IntPtr(1);
                }
            }
            else if (uMsg == (uint)User32.WindowMessage.WM_DWMCOMPOSITIONCHANGED)
            {
                DwmApi.DwmExtendFrameIntoClientArea(hWnd, new DwmApi.MARGINS(0));
                using var rgn = Gdi32.CreateRectRgn(-2, -2, -1, -1);
                DwmApi.DwmEnableBlurBehindWindow(hWnd, new DwmApi.DWM_BLURBEHIND(true)
                {
                    dwFlags = DwmApi.DWM_BLURBEHIND_Mask.DWM_BB_ENABLE | DwmApi.DWM_BLURBEHIND_Mask.DWM_BB_BLURREGION,
                    hRgnBlur = rgn
                });

                return IntPtr.Zero;
            }

            return ComCtl32.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
        #endregion
    }
}
