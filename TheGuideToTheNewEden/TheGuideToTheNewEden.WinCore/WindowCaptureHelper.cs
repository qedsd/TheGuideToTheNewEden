using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinCore
{
    public static class WindowCaptureHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowRect(IntPtr hWnd, ref Rectangle rect);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")]
        private static extern int DeleteDC(IntPtr hdc);
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, int nFlags);
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hwnd);

        [DllImport("gdi32.dll")]
        public static extern int BitBlt(
         IntPtr hdcDest, // handle to destination DC目标设备的句柄
         int nXDest,   // x-coord of destination upper-left corner目标对象的左上角的X坐标
         int nYDest,   // y-coord of destination upper-left corner目标对象的左上角的Y坐标
         int nWidth,   // width of destination rectangle目标对象的矩形宽度
         int nHeight, // height of destination rectangle目标对象的矩形长度
         IntPtr hdcSrc,   // handle to source DC源设备的句柄
         int nXSrc,    // x-coordinate of source upper-left corner源对象的左上角的X坐标
         int nYSrc,    // y-coordinate of source upper-left corner源对象的左上角的Y坐标
         UInt32 dwRop   // raster operation code光栅的操作值
         );
        public static Rectangle GetWindowRect(IntPtr hWnd)
        {
            var windowRect = new Rectangle();
            GetWindowRect(hWnd, ref windowRect);
            return windowRect;
        }
        public static Bitmap GetShotCutImage(IntPtr hWnd)
        {
            var hscrdc = GetWindowDC(hWnd);
            if (hscrdc == IntPtr.Zero)
            {
                return null;
            }
            var windowRect = new Rectangle();
            GetWindowRect(hWnd, ref windowRect);
            int width = Math.Abs(windowRect.Width - windowRect.X);
            int height = Math.Abs(windowRect.Height - windowRect.Y);
            var hbitmap = CreateCompatibleBitmap(hscrdc, width, height);
            var hmemdc = CreateCompatibleDC(hscrdc);
            SelectObject(hmemdc, hbitmap);
            PrintWindow(hWnd, hmemdc, 0);
            var bmp = Image.FromHbitmap(hbitmap);
            DeleteDC(hscrdc);
            DeleteDC(hmemdc);
            return bmp;
        }
        public const int SRCCOPY = 0x00CC0020;
        [DllImport("user32.dll")]
        public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();
        /// <summary>
        /// 指定窗口截图
        /// </summary>
        /// <param name="handle">窗口句柄. (在windows应用程序中, 从Handle属性获得)</param>
        /// <returns></returns>
        public static Bitmap CaptureWindow(IntPtr handle)
        {
            IntPtr hdcSrc = GetWindowDC(handle);
            var windowRect = new Rectangle();
            GetWindowRect(handle, ref windowRect);
            int width = Math.Abs(windowRect.Width - windowRect.X);
            int height = Math.Abs(windowRect.Height - windowRect.Y);
            IntPtr hdcDest = CreateCompatibleDC(hdcSrc);
            IntPtr hBitmap = CreateCompatibleBitmap(hdcSrc, width, height);
            IntPtr hOld = SelectObject(hdcDest, hBitmap);
            BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, SRCCOPY);
            SelectObject(hdcDest, hOld);
            DeleteDC(hdcDest);
            ReleaseDC(handle, hdcSrc);
            Bitmap img = Image.FromHbitmap(hBitmap);
            DeleteObject(hBitmap);
            return img;
        }

        #region Interop structs
        [StructLayout(LayoutKind.Sequential)]
        internal struct DWM_THUMBNAIL_PROPERTIES
        {
            public int dwFlags;
            public Rect rcDestination;
            public Rect rcSource;
            public byte opacity;
            public bool fVisible;
            public bool fSourceClientAreaOnly;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public Rect(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
            public Rect(double left, double top, float right, float bottom)
            {
                Left = (int)left;
                Top = (int)top;
                Right = (int)right;
                Bottom = (int)bottom;
            }
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PSIZE
        {
            public int x;
            public int y;
        }
        #endregion
        #region DWM functions

        [DllImport("dwmapi.dll")]
        static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

        [DllImport("dwmapi.dll")]
        static extern int DwmUnregisterThumbnail(IntPtr thumb);

        [DllImport("dwmapi.dll")]
        static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out PSIZE size);

        [DllImport("dwmapi.dll")]
        static extern int DwmUpdateThumbnailProperties(IntPtr hThumb, ref DWM_THUMBNAIL_PROPERTIES props);

        #endregion

        #region Constants

        static readonly int DWM_TNP_VISIBLE = 0x8;
        static readonly int DWM_TNP_OPACITY = 0x4;
        static readonly int DWM_TNP_RECTDESTINATION = 0x1;
        static readonly int DWM_TNP_RECTSOURCE = 0x2;

        #endregion

        public static IntPtr Show(IntPtr targetHWnd, IntPtr sourceHWnd)
        {
            DwmRegisterThumbnail(targetHWnd, sourceHWnd, out var thumb);
            UpdateThumb(thumb);
            return thumb;
        }
        public static int HideThumb(IntPtr thumb)
        {
            return DwmUnregisterThumbnail(thumb);
        }
        private static void UpdateThumb(IntPtr thumb)
        {
            if (thumb != IntPtr.Zero)
            {
                PSIZE size;
                DwmQueryThumbnailSourceSize(thumb, out size);
                DWM_THUMBNAIL_PROPERTIES props = new DWM_THUMBNAIL_PROPERTIES();

                props.fVisible = true;
                props.dwFlags = DWM_TNP_VISIBLE | DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY;
                props.opacity = 255;
                props.rcDestination = new Rect(0, 0, size.x, size.y);//显示的位置大小
                DwmUpdateThumbnailProperties(thumb, ref props);
            }
        }
        public static void UpdateThumbDestination(IntPtr thumb, Rect rect)
        {
            if (thumb != IntPtr.Zero)
            {
                DWM_THUMBNAIL_PROPERTIES props = new DWM_THUMBNAIL_PROPERTIES();
                props.dwFlags = DWM_TNP_RECTDESTINATION;
                props.rcDestination = rect;//显示的位置大小
                DwmUpdateThumbnailProperties(thumb, ref props);
            }
        }
        public static void UpdateThumbDestination(IntPtr thumb, Rect rcDestination, Rect rcSource)
        {
            if (thumb != IntPtr.Zero)
            {
                DWM_THUMBNAIL_PROPERTIES props = new DWM_THUMBNAIL_PROPERTIES();
                props.dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_RECTSOURCE;
                props.rcDestination = rcDestination;//显示的位置大小
                props.rcSource = rcSource;
                DwmUpdateThumbnailProperties(thumb, ref props);
            }
        }
        public static PSIZE GetThumbSourceSize(IntPtr thumb)
        {
            PSIZE size;
            DwmQueryThumbnailSourceSize(thumb, out size);
            return size;
        }
    }
}
