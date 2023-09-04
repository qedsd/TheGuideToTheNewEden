using ESI.NET.Models.Fleets;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Helpers;
using Vanara.PInvoke;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class GamePreviewOverlapWindow : Window
    {
        private ComCtl32.SUBCLASSPROC wndProcHandler;
        private Grid _grid;
        private TextBlock _textBlock;
        private readonly AppWindow _appWindow;
        public GamePreviewOverlapWindow()
        {
            _textBlock = new TextBlock()
            {
                FontSize = 18,
                Margin = new Thickness(0)
            };
            _textBlock.Loaded += _textBlock_Loaded;
            //_grid = new Grid();
            //_grid.Children.Add(_textBlock);
            Content = _textBlock;
            Helpers.WindowHelper.HideTitleBar2(this);
            _appWindow = Helpers.WindowHelper.GetAppWindow(this);
            this.Activated += GamePreviewOverlapWindow_Activated;
        }

        private void _textBlock_Loaded(object sender, RoutedEventArgs e)
        {
            _textBlock.Loaded -= _textBlock_Loaded;
            var dpi = WindowHelper.GetDpiScale(this);
            Resize((int)Math.Ceiling(_textBlock.ActualWidth * dpi), (int)Math.Ceiling(_textBlock.ActualHeight * dpi));
        }

        private void TransparentWindow()
        {
            Helpers.TransparentWindowHelper.TransparentWindowVisual(this);
            wndProcHandler = new ComCtl32.SUBCLASSPROC(WndProc);
            ComCtl32.SetWindowSubclass(WindowHelper.GetWindowHandle(this), wndProcHandler, 1, IntPtr.Zero);
        }
        private void GamePreviewOverlapWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= GamePreviewOverlapWindow_Activated;
        }

        public void SetTitle(string title)
        {
            _textBlock.Text = title;
        }
        public void Move(int x, int y)
        {
            Helpers.WindowHelper.MoveToScreen(this, x + 10, y + 10);
        }
        public void Resize(int w, int h)
        {
            _appWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
        }
        public void Show()
        {
            this.Activate();
            Helpers.WindowHelper.TopMost(this);
        }

        private unsafe IntPtr WndProc(HWND hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData)
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
    }
}
