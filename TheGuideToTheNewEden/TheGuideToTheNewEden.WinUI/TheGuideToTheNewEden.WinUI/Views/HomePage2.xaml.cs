using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.WinUI.Extensions;
using System.Threading;
using System.Diagnostics;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class HomePage2 : Page,IPage
    {
        public HomePage2()
        {
            this.InitializeComponent();
        }

        public void Close()
        {
            
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowWaiting(this.GetType().Name);
            await Task.Delay(3000);
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().HideWaiting(this.GetType().Name);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowMsg(this.GetType().Name, "TestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTestTest", Controls.InfoBarControl.InfoType.Error, false);
        }

        private void ToolWindow_Click(object sender, RoutedEventArgs e)
        {
            ToolWindow toolWindow = new ToolWindow(null, WindowTitleStyle.OnlyClose, true, false);
            toolWindow.Activate();
        }

        private void TransparentWindow_Click(object sender, RoutedEventArgs e)
        {
            Window window = new Window();
            window.SystemBackdrop = new DevWinUI.TransparentBackdrop();
            window.Activate();
        }

        [DllImport("imm32.dll")]
        public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);
        // IME 控制消息常量
        public const int WM_IME_CONTROL = 0x0283;

        // IMC_ 开头的常量是 WM_IME_CONTROL 的 wParam 参数
        public const int IMC_GETCANDIDATEPOS = 0x0007;
        public const int IMC_SETCANDIDATEPOS = 0x0008;
        public const int IMC_GETCOMPOSITIONWINDOW = 0x0009;
        public const int IMC_SETCOMPOSITIONWINDOW = 0x000A;

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        public static IntPtr SendImeControlMessage(IntPtr hWnd, int command, IntPtr data)
        {
            IntPtr hImeWnd = ImmGetDefaultIMEWnd(hWnd);
            if (hImeWnd != IntPtr.Zero)
            {
                return SendMessage(hImeWnd, WM_IME_CONTROL, (IntPtr)command, data);
            }
            return IntPtr.Zero;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct CANDIDATEFORM
        {
            public int dwIndex;
            public int dwStyle;
            public POINT ptCurrentPos;
            public RECT rcArea;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        private void IME_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = Helpers.WindowHelper.GetWindowHandle(this.GetWindow());
            var immHwnd = ImmGetDefaultIMEWnd(hwnd);
            Task.Run(() =>
            {
                while (true)
                {
                    var can = GetCandidatePosition(immHwnd);
                    
                    var pos = can.ptCurrentPos;
                    var rect = can.rcArea;
                    Debug.WriteLine($"{pos.X} {pos.Y} {rect.Left} {rect.Right} {rect.Top} {rect.Bottom}");
                    //var rect = Helpers.WindowHelper.GetWindowRect(immHwnd);
                    //Debug.WriteLine($"{rect.Left} {rect.Right} {rect.Top} {rect.Bottom}");
                    Thread.Sleep(100);
                }
            });
        }
        public static CANDIDATEFORM GetCandidatePosition(IntPtr hWnd)
        {
            CANDIDATEFORM form = new CANDIDATEFORM();
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(form));

            try
            {
                Marshal.StructureToPtr(form, ptr, false);
                SendImeControlMessage(hWnd, IMC_GETCANDIDATEPOS, ptr);
                form = Marshal.PtrToStructure<CANDIDATEFORM>(ptr);
                return form;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        // 定义COMPOSITIONFORM结构
        [StructLayout(LayoutKind.Sequential)]
        public struct COMPOSITIONFORM
        {
            public int dwStyle;
            public POINT ptCurrentPos;
            public RECT rcArea;
        }

        public static COMPOSITIONFORM GetCompositionWindow(IntPtr hWnd)
        {
            COMPOSITIONFORM form = new COMPOSITIONFORM();
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(form));

            try
            {
                Marshal.StructureToPtr(form, ptr, false);
                SendImeControlMessage(hWnd, IMC_GETCOMPOSITIONWINDOW, ptr);
                form = Marshal.PtrToStructure<COMPOSITIONFORM>(ptr);
                return form;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public static void SetCompositionWindow(IntPtr hWnd, int x, int y)
        {
            COMPOSITIONFORM form = new COMPOSITIONFORM();
            form.dwStyle = 0x0001;  // CFS_POINT
            form.ptCurrentPos.X = x;
            form.ptCurrentPos.Y = y;

            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(form));
            try
            {
                Marshal.StructureToPtr(form, ptr, false);
                SendImeControlMessage(hWnd, IMC_SETCOMPOSITIONWINDOW, ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        private void SetIME_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = Helpers.WindowHelper.GetWindowHandle(this.GetWindow());
            var immHwnd = ImmGetDefaultIMEWnd(hwnd);
            SetCompositionWindow(immHwnd, 100,1000);
        }
    }
}
