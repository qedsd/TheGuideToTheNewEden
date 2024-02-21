using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.PreviewWindow
{
    internal static class Win32
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowRect(IntPtr hWnd, ref System.Drawing.Rectangle rect);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int X, int Y, int cx, int cy, uint Flags);
        [DllImport("user32.dll")]
        public static extern IntPtr GetClientRect(IntPtr hWnd, ref System.Drawing.Rectangle rect);
        [DllImport("user32.dll")]
        public static extern IntPtr ClientToScreen(IntPtr hWnd, ref System.Drawing.Point point);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            internal Rect(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
            internal Rect(double left, double top, float right, float bottom)
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
        internal struct PSIZE
        {
            public int x;
            public int y;
        }

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
        static readonly int DWM_TNP_VISIBLE = 0x8;
        static readonly int DWM_TNP_OPACITY = 0x4;
        static readonly int DWM_TNP_RECTDESTINATION = 0x1;
        static readonly int DWM_TNP_RECTSOURCE = 0x2;
        static readonly int DWM_TNP_SOURCECLIENTAREAONLY = 0x10;
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

        public static int GetTitleBarHeight(IntPtr hwnd)
        {
            var windowRect = new System.Drawing.Rectangle();
            GetWindowRect(hwnd, ref windowRect);
            var clientRect = new System.Drawing.Rectangle();
            GetClientRect(hwnd, ref clientRect);
            System.Drawing.Point point = new System.Drawing.Point();
            ClientToScreen(hwnd, ref point);
            return point.Y - windowRect.Top;
        }
        public static int GetBorderWidth(IntPtr hwnd)
        {
            var windowRect = new System.Drawing.Rectangle();
            GetWindowRect(hwnd, ref windowRect);
            System.Drawing.Point point = new System.Drawing.Point();
            ClientToScreen(hwnd, ref point);
            return point.X - windowRect.Left;
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
        public static IntPtr ShowThumbnail(IntPtr targetHWnd, IntPtr sourceHWnd)
        {
            DwmRegisterThumbnail(targetHWnd, sourceHWnd, out var thumb);
            UpdateThumb(thumb);
            return thumb;
        }
        public static int HideThumb(IntPtr thumb)
        {
            return DwmUnregisterThumbnail(thumb);
        }

        [DllImport("User32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("User32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("User32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
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
    }
}
