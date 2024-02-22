using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using TheGuideToTheNewEden.PreviewIPC;
using TheGuideToTheNewEden.PreviewIPC.Memory;

namespace TheGuideToTheNewEden.PreviewWindow
{
    public partial class MainWindow : Window
    {
        private SolidColorBrush _nomralColor = new SolidColorBrush(Colors.White);
        private SolidColorBrush _hlColor;
        private IntPtr _overlapHwnd = IntPtr.Zero;
        private ObservableCollection<string> _msgs = new ObservableCollection<string>();
        private IPreviewIPC _previewIPC;
        private ThumbnailWindow _thumbnailWindow;
        public MainWindow()
        {
            InitializeComponent();
            ListView_Msg.ItemsSource = _msgs;
            Loaded += MainWindow_Loaded;
            Loaded += MainWindow_Loaded2;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _overlapHwnd = (System.Windows.Interop.HwndSource.FromDependencyObject(this) as System.Windows.Interop.HwndSource).Handle;
            SizeChanged += MainWindow_SizeChanged;
            this.LocationChanged += MainWindow_LocationChanged;
            if (App.Args != null)
            {
                for (int i = 0; i < App.Args?.Length; i++)
                {
                    _msgs.Add(App.Args[i]);
                }
            }
            try
            {
                _previewIPC = new MemoryIPC(App.Args[0]);
                Task.Run(() => GetMsg());
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            _thumbnailWindow = new ThumbnailWindow(App.GetHwnd(), Color.FromArgb(App.GetA(), App.GetR(), App.GetG(), App.GetB()), _overlapHwnd);
            _thumbnailWindow.Loaded += ThumbnailWindow_Loaded;
            _thumbnailWindow.Show();
            SetWindowPos(_overlapHwnd, 0, App.GetX(), App.GetY(), App.GetW(), App.GetH(), 0x0004);
            _thumbnailWindow?.UpdateThumbnail();
            _hlColor = new SolidColorBrush(Color.FromArgb(App.GetA(), App.GetR(), App.GetG(), App.GetB()));
            NameTextBlock.Text = App.GetName();
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _thumbnailWindow?.Dispose();
        }

        private void ThumbnailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _thumbnailWindow.Loaded -= ThumbnailWindow_Loaded;
            Win32.SetWindowPos(_overlapHwnd, _thumbnailWindow.GetHwnd(), 0, 0, 0, 0, 0x0002 | 0x0001);
        }

        #region 通知窗口位置大小变化
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
        private void UpdateSizeAndPos()
        {
            var windowRect = new System.Drawing.Rectangle();
            GetWindowRect(_overlapHwnd, ref windowRect);
            _previewIPC.SendMsg(IPCOp.UpdateSizeAndPos, new int[] { windowRect.Width - windowRect.X, windowRect.Height - windowRect.Y, windowRect.X, windowRect.Y });
            _thumbnailWindow?.SetSizeAndPos(windowRect.Width - windowRect.X, windowRect.Height - windowRect.Y, windowRect.X, windowRect.Y);
            _thumbnailWindow?.UpdateThumbnail();
        }
        #endregion

        private string _lastMsg = null;
        private void GetMsg()
        {
            while(true)
            {
                _previewIPC.TryGetMsg(out var op, out var msgs);
                if (op != IPCOp.None)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.Append(op);
                    if(msgs != null && msgs.Length > 0)
                    {
                        foreach(var p in msgs)
                        {
                            stringBuilder.Append(' ');
                            stringBuilder.Append(p);
                        }
                    }
                    string msgStr = stringBuilder.ToString();
                    if(_lastMsg != msgStr)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            _msgs.Add(msgStr);
                        });
                        _lastMsg = msgStr;
                    }
                }
                HandleMsg(op, msgs);
                Thread.Sleep(100);
            }
        }
        
        #region 鼠标右键移动
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, int hWndlnsertAfter, int X, int Y, int cx, int cy, uint Flags);
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

        #region 响应
        private void HandleMsg(IPCOp op, int[] msgs)
        {
            switch (op)
            {
                case IPCOp.None:
                case IPCOp.Handled:
                case IPCOp.UpdateSizeAndPos:
                case IPCOp.ResultMsg:return;
                case IPCOp.Close: Close(op, msgs); break;
                case IPCOp.Hide: Hide(op, msgs); break;
                case IPCOp.Show: Show(op, msgs); break;
                case IPCOp.UpdateThumbnail: UpdateThumbnail(op, msgs); break;
                case IPCOp.Highlight: Highlight(op, msgs); break;
                case IPCOp.CancelHighlight: CancelHighlight(op, msgs); break;
                case IPCOp.SetSize: SetSize(op, msgs); break;
                case IPCOp.SetPos: SetPos(op, msgs); break;
                case IPCOp.GetSizeAndPos: GetSizeAndPos(op, msgs); break;
                case IPCOp.GetWidth: GetWidth(op, msgs); break;
                case IPCOp.GetHeight: GetHeight(op, msgs); break;
            }
        }
        private void Close(IPCOp op, int[] msgs)
        {
            _previewIPC?.Dispose();
            this.Dispatcher.Invoke(() =>
            {
                this.Close();
            });
        }
        private void Hide(IPCOp op, int[] msgs)
        {
            this.Dispatcher.Invoke(() =>
            {
                this.Hide();
            });
            _thumbnailWindow.Dispatcher.Invoke(() =>
            {
                _thumbnailWindow.Hide();
            });
            _previewIPC.SendMsg(IPCOp.Handled);
        }
        private void Show(IPCOp op, int[] msgs)
        {
            _thumbnailWindow.Dispatcher.Invoke(() =>
            {
                _thumbnailWindow.Show();
            });
            this.Dispatcher.Invoke(() =>
            {
                this.Show();
            });
            _previewIPC.SendMsg(IPCOp.Handled);
        }
        private void UpdateThumbnail(IPCOp op, int[] msgs)
        {
            this.Dispatcher.Invoke(() =>
            {
                _msgs.Add("更新略缩图");
            });
            _thumbnailWindow.UpdateThumbnail();
            _previewIPC.SendMsg(IPCOp.Handled);
        }
        private void Highlight(IPCOp op, int[] msgs)
        {
            this.Dispatcher.Invoke(() =>
            {
                _msgs.Add("高亮");
                NameTextBlock.Foreground = _hlColor;
            });
            _thumbnailWindow.UpdateThumbnail(msgs[0], msgs[1], msgs[2], msgs[3]);
            _previewIPC.SendMsg(IPCOp.Handled);
        }
        private void CancelHighlight(IPCOp op, int[] msgs)
        {
            this.Dispatcher.Invoke(() =>
            {
                _msgs.Add("取消高亮");
                NameTextBlock.Foreground = _nomralColor;
            });
            _thumbnailWindow.UpdateThumbnail();
            _previewIPC.SendMsg(IPCOp.Handled);
        }
        private void SetSize(IPCOp op, int[] msgs)
        {
            this.Dispatcher.Invoke(() =>
            {
                _msgs.Add("设置窗口大小");
            });
            SetWindowPos(_overlapHwnd, 0, 0, 0, msgs[0], msgs[1], 0x0002 | 0x0004);
            _previewIPC.SendMsg(IPCOp.Handled);
        }
        private void SetPos(IPCOp op, int[] msgs)
        {
            this.Dispatcher.Invoke(() =>
            {
                _msgs.Add("设置窗口位置");
            });
            SetWindowPos(_overlapHwnd, 0, msgs[0], msgs[1], 0, 0, 0x0001 | 0x0004);
            _previewIPC.SendMsg(IPCOp.Handled);
        }
        private void GetSizeAndPos(IPCOp op, int[] msgs)
        {
            var windowRect = new System.Drawing.Rectangle();
            GetWindowRect(_overlapHwnd, ref windowRect);
            this.Dispatcher.Invoke(() =>
            {
                _msgs.Add($"获取窗口大小和位置：{windowRect.Width - windowRect.X}x{windowRect.Height - windowRect.Y} {windowRect.X} {windowRect.Y}");
            });
            _previewIPC.SendMsg(IPCOp.ResultMsg, new int[] { windowRect.Width - windowRect.X, windowRect.Height - windowRect.Y, windowRect.X, windowRect.Y });
        }
        private void GetWidth(IPCOp op, int[] msgs)
        {
            var windowRect = new System.Drawing.Rectangle();
            GetWindowRect(_overlapHwnd, ref windowRect);
            this.Dispatcher.Invoke(() =>
            {
                _msgs.Add($"获取窗口宽：{windowRect.Width - windowRect.X}");
            });
            _previewIPC.SendMsg(IPCOp.ResultMsg, new int[] { windowRect.Width });
        }
        private void GetHeight(IPCOp op, int[] msgs)
        {
            var windowRect = new System.Drawing.Rectangle();
            GetWindowRect(_overlapHwnd, ref windowRect);
            this.Dispatcher.Invoke(() =>
            {
                _msgs.Add($"获取窗口高：{windowRect.Height - windowRect.Y}");
            });
            _previewIPC.SendMsg(IPCOp.ResultMsg, new int[] { windowRect.Height });
        }
        private void UpdateSizeAndPos(IPCOp op, int[] msgs)
        {
            this.Dispatcher.Invoke(() =>
            {
                _msgs.Add("设置窗口大小和位置");
            });
            SetWindowPos(_overlapHwnd, 0, msgs[2], msgs[3], msgs[0], msgs[1], 0x0004);
            _previewIPC.SendMsg(IPCOp.Handled);
        }
        #endregion

        private async void Button_Test_Click(object sender, RoutedEventArgs e)
        {
            var previewIPC = new MemoryIPC(App.Args[1]);
            previewIPC.SendMsg(IPCOp.SetSize, new int[] { 500, 500 });
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Win32.SetForegroundWindow_Click(App.GetHwnd());
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

    }
}
