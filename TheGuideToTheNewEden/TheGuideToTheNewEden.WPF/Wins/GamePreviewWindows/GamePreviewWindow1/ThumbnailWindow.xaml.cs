using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TheGuideToTheNewEden.WPF.Common;
using TheGuideToTheNewEden.WPF.Helpers;

namespace TheGuideToTheNewEden.WPF.Wins.GamePreviewWindows
{
    public partial class ThumbnailWindow : Window
    {
        /// <summary>
        /// 当前供显示略缩图的窗口句柄
        /// </summary>
        private IntPtr _thisHwnd = IntPtr.Zero;
        /// <summary>
        /// 创建略缩图返回的句柄
        /// </summary>
        private IntPtr _thumbnailHwnd = IntPtr.Zero;
        /// <summary>
        /// 游戏窗口句柄
        /// </summary>
        private IntPtr _gameHwnd = IntPtr.Zero;
        /// <summary>
        /// 显示角色名称的窗口
        /// </summary>
        private IntPtr _overlapHwnd = IntPtr.Zero;
        public ThumbnailWindow(IntPtr gameHwnd, Color backgroundColor, IntPtr overlapHwnd)
        {
            _overlapHwnd = overlapHwnd;
            _gameHwnd = gameHwnd;
            InitializeComponent();
            Background = new SolidColorBrush(backgroundColor);
            Loaded += ThumbnailWindow_Loaded;
        }

        private void ThumbnailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ThumbnailWindow_Loaded;
            _thisHwnd = (System.Windows.Interop.HwndSource.FromDependencyObject(this) as System.Windows.Interop.HwndSource).Handle;
            _thumbnailHwnd = WindowCaptureHelper.Show(_thisHwnd, _gameHwnd);
            //if(App.GetOpacity() < 100)
            //{
            //    Win32.TransparentWindow(_thisHwnd, App.GetOpacity());
            //}
        }

        public void UpdateThumbnail(int left = 0, int right = 0, int top = 0, int bottom = 0)
        {
            if (_thumbnailHwnd != IntPtr.Zero)
            {
                try
                {
                    //预览窗口显示区域
                    var thisRect = new System.Drawing.Rectangle();
                    Win32.GetWindowRect(_thisHwnd, ref thisRect);
                    int dleft = left;
                    int dtop = top;
                    int dright = thisRect.Width - right - thisRect.X;
                    int dbottom = thisRect.Height - bottom - thisRect.Y;
                    WindowCaptureHelper.Rect targetWindowRect = new WindowCaptureHelper.Rect(dleft, dtop, dright, dbottom);

                    //游戏窗口显示区域
                    var titleBarHeight = WindowCaptureHelper.GetTitleBarHeight(_gameHwnd);//去掉游戏窗口标题栏高度
                    int widthMargin = WindowCaptureHelper.GetBorderWidth(_gameHwnd);//去掉游戏窗口左边白边及右边显示完整
                    var gameRect = new System.Drawing.Rectangle();
                    Win32.GetClientRect(_gameHwnd, ref gameRect);
                    WindowCaptureHelper.Rect targetGameRect = new WindowCaptureHelper.Rect(widthMargin, titleBarHeight, gameRect.Right + widthMargin - gameRect.X, gameRect.Bottom - gameRect.Y);

                    //更新显示
                    WindowCaptureHelper.UpdateThumbDestination(_thumbnailHwnd, targetWindowRect, targetGameRect);
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(ex.Message);
                    });
                }
            }
        }
        public void SetSizeAndPos(int w, int h, int x, int y)
        {
            Win32.SetWindowPos(_thisHwnd, 0, x, y, w, h, 0x0004);//NOZORDER
            //Win32.SetWindowPos(_overlapHwnd, _thisHwnd, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);//NOSIZE | NOMOVE | NOACTIVATE
        }
        public void SetSize(int w, int h)
        {
            Win32.SetWindowPos(_thisHwnd, 0, 0, 0, w, h, 0x0002 | 0x0004);
        }
        public void SetPos(int x, int y)
        {
            Win32.SetWindowPos(_thisHwnd, 0, x, y, 0, 0, 0x0001 | 0x0004);
        }
        public IntPtr GetHwnd()
        {
            return _thisHwnd;
        }
        public IntPtr GetThumbnailHwnd()
        {
            return _thumbnailHwnd;
        }
        public void Dispose()
        {
            WindowCaptureHelper.HideThumb(_thumbnailHwnd);
            this.Dispatcher.Invoke(() =>
            {
                this.Close();
            });
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }
    }
}
