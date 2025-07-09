using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WPF.Common;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Interfaces;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Wins.GamePreviewWindows
{
    public partial class GamePreviewWindow1 : Window, IGamePreviewWindow
    {
        private SolidColorBrush _nomralColor = new SolidColorBrush(Colors.White);
        private SolidColorBrush _hlColor;
        private IntPtr _overlapHwnd = IntPtr.Zero;
        private ThumbnailWindow _thumbnailWindow;
        private PreviewItem _previewItem;
        private PreviewSetting _previewSetting;
        private IntPtr _sourceHWnd;
        private Window _parentWindow;
        private bool _closed = false;
        public GamePreviewWindow1(PreviewItem setting, PreviewSetting previewSetting)
        {
            _previewItem = setting;
            _previewSetting = previewSetting;
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Loaded += MainWindow_Loaded2;
        }

        public event IGamePreviewWindow.SettingChangedDelegate OnSettingChanged;
        public event IGamePreviewWindow.StopDelegate OnStop;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _closed = true;
            _thumbnailWindow?.Dispose();
        }

        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            Task.Run(() =>
            {
                UpdateSizeAndPos();
            });
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Task.Run(() =>
            {
                UpdateSizeAndPos();
            });
        }
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            GetSizeAndPos(out int x, out int y, out int w, out int h);
            if (e.Delta > 0)
            {
                SetSize((int)(w * 1.1), (int)(h * 1.1));
            }
            else
            {
                SetSize((int)(w * 0.9), (int)(h * 0.9));
            }
        }
        private void UpdateSizeAndPos()
        {
            GetSizeAndPos(out int x, out int y, out int w, out int h);
            if (w == 0 || h == 0)
                return;
            _thumbnailWindow?.SetSizeAndPos(w, h, x, y);
            _thumbnailWindow?.UpdateThumbnail();
            _previewItem.WinX = x;
            _previewItem.WinY = y;
            _previewItem.WinW = w;
            _previewItem.WinH = h;
            OnSettingChanged?.Invoke(_previewItem);
        }

        #region 快捷键
        private int _hotkeyRegisterId;
        private void InitHotkey()
        {
            //快捷键
            if (!string.IsNullOrEmpty(_previewItem.HotKey))
            {
                if (HotkeyService.GetHotkeyService(_overlapHwnd).Register(_previewItem.HotKey, out _hotkeyRegisterId))
                {
                    HotkeyService.GetHotkeyService(_overlapHwnd).HotkeyActived -= GamePreviewWindowBase_HotkeyActived;
                    HotkeyService.GetHotkeyService(_overlapHwnd).HotkeyActived += GamePreviewWindowBase_HotkeyActived;
                    Core.Log.Info($"注册游戏预览窗口热键成功{_previewItem.HotKey}_{_hotkeyRegisterId}");
                }
                else
                {
                    Core.Log.Error($"注册游戏预览窗口热键失败:{_previewItem.Name} {_previewItem.HotKey}");
                    MessageBox.Show($"注册游戏预览窗口热键失败:{_previewItem.Name} {_previewItem.HotKey}");
                }
            }
        }

        private void GamePreviewWindowBase_HotkeyActived(int hotkeyId)
        {
            if (hotkeyId == _hotkeyRegisterId)
            {
                ActiveSourceWindow();
            }
        }
        #endregion

        #region 鼠标右键移动
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndlnsertAfter, int X, int Y, int cx, int cy, uint Flags);
        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowRect(IntPtr hWnd, ref System.Drawing.Rectangle rect);
        private Point _pressedPos;
        bool _isDragMoved = false;
        private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var currentPos = e.GetPosition(this);
            if (Mouse.RightButton == MouseButtonState.Pressed && _pressedPos != currentPos)
            {
                _isDragMoved = true;
                var windowRect = new System.Drawing.Rectangle();
                GetWindowRect(_overlapHwnd, ref windowRect);
                //0x0001 SWP_NOSIZE | 0x0004 SWP_NOZORDER
                //忽略hWndlnsertAfter和cy、cx，即不改变窗口显示顺序和显示长宽，仅移动位置
                SetWindowPos(_overlapHwnd, 0, windowRect.X + (int)(currentPos.X - _pressedPos.X), windowRect.Y + (int)(currentPos.Y - _pressedPos.Y), 0,0, 0x0001 | 0x0004);
            }
        }

        private void Window_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragMoved)
            {
                _isDragMoved = false;
                e.Handled = true;
            }
        }

        private void Window_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _pressedPos = e.GetPosition(this);
        }


        #endregion

        /// <summary>
        /// 左键激活游戏窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            WindowHelper.SetForegroundWindow_Click(_sourceHWnd);
        }

        #region 异常处理
        private void MainWindow_Loaded2(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded2;
            System.Windows.Application.Current.DispatcherUnhandledException -= Current_DispatcherUnhandledException;
            System.Windows.Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            //处理非UI线程异常
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            ///Task线程内异常
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            System.Windows.Application.Current.DispatcherUnhandledException -= Current_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
        }
        /// <summary>
        /// 非UI线程异常
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("非UI线程发生未处理的异常：");
            if (e.IsTerminating)//IsTerminating == true 将便不可避免关闭
            {
                stringBuilder.AppendLine("程序发生致命错误，将终止！");
            }
            if (e.ExceptionObject is Exception)
            {
                stringBuilder.Append(((Exception)e.ExceptionObject).Message);
            }
            else
            {
                stringBuilder.Append(e.ExceptionObject);
            }
            MessageBox.Show(stringBuilder.ToString());
        }
        /// <summary>
        /// UI线程异常
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                e.Handled = true;
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("UI线程发生未处理的异常：");
                stringBuilder.Append(e.Exception.Message);
                MessageBox.Show(stringBuilder.ToString());
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("程序发生致命错误，将终止！", "系统错误", MessageBoxButton.OK);
            }
        }
        /// <summary>
        /// Task线程异常
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();//避免崩溃
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("Task线程发生未处理的异常：");
            stringBuilder.Append(e.Exception.Message);
            MessageBox.Show(stringBuilder.ToString());
        }
        #endregion

        public PreviewItem GetSetting()
        {
            return _previewItem;
        }

        public bool IsHideOnForeground()
        {
            return _previewItem.HideOnForeground;
        }

        public bool IsHighlight()
        {
            return _previewItem.Highlight;
        }

        public void ActiveSourceWindow()
        {
            Task.Run(() =>
            {
                switch (_previewSetting.SetForegroundWindowMode)
                {
                    case 0: Helpers.WindowHelper.SetForegroundWindow1(_sourceHWnd); break;
                    case 1: Helpers.WindowHelper.SetForegroundWindow2(_sourceHWnd); break;
                    case 2: Helpers.WindowHelper.SetForegroundWindow3(_sourceHWnd); break;
                    case 3: Helpers.WindowHelper.SetForegroundWindow4(_sourceHWnd); break;
                    case 4: Helpers.WindowHelper.SetForegroundWindow5(_sourceHWnd); break;
                    default: Helpers.WindowHelper.SetForegroundWindow1(_sourceHWnd); break;
                }
            });
        }

        public void Start(nint sourceHWnd)
        {
            _sourceHWnd = sourceHWnd;
            
            SizeChanged += MainWindow_SizeChanged;
            this.LocationChanged += MainWindow_LocationChanged;
            Activated += GamePreviewWindow1_Activated;
            Show();
        }

        private void GamePreviewWindow1_Activated(object sender, EventArgs e)
        {
            Activated -= GamePreviewWindow1_Activated;
            _overlapHwnd = WindowHelper.GetWindowHandle(this);
            InitHotkey();
            SetWindowPos(_overlapHwnd, 0, _previewItem.WinX, _previewItem.WinY, _previewItem.WinW, _previewItem.WinH, 0x0004);
            _hlColor = new SolidColorBrush(Color.FromArgb(_previewItem.HighlightColor.A, _previewItem.HighlightColor.R, _previewItem.HighlightColor.G, _previewItem.HighlightColor.B));
            _nomralColor = new SolidColorBrush(Color.FromArgb(_previewItem.TitleNormalColor.A, _previewItem.TitleNormalColor.R, _previewItem.TitleNormalColor.G, _previewItem.TitleNormalColor.B));
            NameTextBlock.Text = _previewItem.Name;
            NameTextBlock.Foreground = _nomralColor;
            HightLightBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(_previewItem.HighlightColor.A, _previewItem.HighlightColor.R, _previewItem.HighlightColor.G, _previewItem.HighlightColor.B));
            HightLightBorder.BorderThickness = new Thickness(0, 0, 0, 0);


            _thumbnailWindow = new ThumbnailWindow(_sourceHWnd, Color.FromArgb(_previewItem.HighlightColor.A, _previewItem.HighlightColor.R, _previewItem.HighlightColor.G, _previewItem.HighlightColor.B), _overlapHwnd);
            _thumbnailWindow.Activated += ThumbnailWindow_Activated;
            _thumbnailWindow.Show();

            UpdateSizeAndPos();
            _thumbnailWindow?.UpdateThumbnail();
            Win32.SetWindowPos(_overlapHwnd, _thumbnailWindow.GetHwnd(), 0, 0, 0, 0, 0x0002 | 0x0001);
        }

        private async void ThumbnailWindow_Activated(object sender, EventArgs e)
        {
            _thumbnailWindow.Activated -= ThumbnailWindow_Activated;
            _thumbnailWindow.Topmost = true;
            _thumbnailWindow.ShowInTaskbar = false;
            await Task.Delay(500);
            this.Topmost = true;
            this.ShowInTaskbar = false;
            Win32.SetWindowPos(_overlapHwnd, _thumbnailWindow.GetHwnd(), 0, 0, 0, 0, 0x0002 | 0x0001);
            Owner = _parentWindow;
            _thumbnailWindow.Owner = _parentWindow;
        }

        public void Stop()
        {
            _thumbnailWindow.Dispatcher.Invoke(() =>
            {
                _thumbnailWindow.Close();
            });
            this.Dispatcher.Invoke(() =>
            {
                this.Close();
            });
        }

        public void ShowWindow(bool hHighlight = false)
        {
            if (_closed)
                return;
            _thumbnailWindow.Dispatcher.Invoke(() =>
            {
                if (_closed)
                    return;
                _thumbnailWindow.Show();
            });
            this.Dispatcher.Invoke(() =>
            {
                if (_closed)
                    return;
                this.Show();
            });
        }

        public void HideWindow()
        {
            this.Dispatcher.Invoke(() =>
            {
                this.Hide();
            });
            _thumbnailWindow.Dispatcher.Invoke(() =>
            {
                _thumbnailWindow.Hide();
            });
        }

        public void UpdateThumbnail(int left = 0, int right = 0, int top = 0, int bottom = 0)
        {
            _thumbnailWindow.UpdateThumbnail();
        }

        public void Highlight()
        {
            this.Dispatcher.Invoke(() =>
            {
                NameTextBlock.Foreground = _hlColor;
                HightLightBorder.BorderThickness = new Thickness(_previewItem.HighlightMarginLeft, _previewItem.HighlightMarginTop, _previewItem.HighlightMarginRight, _previewItem.HighlightMarginBottom);
            });
            _thumbnailWindow.UpdateThumbnail((int)_previewItem.HighlightMarginLeft, (int)_previewItem.HighlightMarginTop, (int)_previewItem.HighlightMarginRight, (int)_previewItem.HighlightMarginBottom);
        }

        public void CancelHighlight()
        {
            this.Dispatcher.Invoke(() =>
            {
                NameTextBlock.Foreground = _nomralColor;
                HightLightBorder.BorderThickness = new Thickness(0, 0, 0, 0);
            });
            _thumbnailWindow.UpdateThumbnail();
        }

        public void SetSize(int w, int h)
        {
            SetWindowPos(_overlapHwnd, 0, 0, 0, w, h, 0x0002 | 0x0004);
        }

        public void SetPos(int x, int y)
        {
            SetWindowPos(_overlapHwnd, 0, x, y, 0, 0, 0x0001 | 0x0004);
        }

        public void GetSizeAndPos(out int x, out int y, out int w, out int h)
        {
            var windowRect = new System.Drawing.Rectangle();
            GetWindowRect(_overlapHwnd, ref windowRect);
            w = windowRect.Width - windowRect.X;
            h = windowRect.Height - windowRect.Y;
            x = windowRect.X;
            y = windowRect.Y;
        }

        public int GetWidth()
        {
            var windowRect = new System.Drawing.Rectangle();
            GetWindowRect(_overlapHwnd, ref windowRect);
            return windowRect.Width;
        }

        public int GetHeight()
        {
            var windowRect = new System.Drawing.Rectangle();
            GetWindowRect(_overlapHwnd, ref windowRect);
            return windowRect.Height;
        }

        public void SetParentWindow(Window window)
        {
            _parentWindow = window;
        }
    }
}
