using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using TheGuideToTheNewEden.PreviewIPC;
using TheGuideToTheNewEden.PreviewIPC.Memory;

namespace TheGuideToTheNewEden.PreviewWindow
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<string> _msgs = new ObservableCollection<string>();
        private IPreviewIPC _previewIPC;
        public MainWindow()
        {
            InitializeComponent();
            ListView_Msg.ItemsSource = _msgs;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (App.Args != null)
            {
                for (int i = 0; i < App.Args?.Length; i++)
                {
                    _msgs.Add(App.Args[i]);
                }
            }
            try
            {
                _previewIPC = new MemoryIPC(App.Args[1]);
                Task.Run(() => GetMsg());
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void GetMsg()
        {
            while(true)
            {
                int[] msgs = _previewIPC.CheckMsg();
                if (msgs != null)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        foreach (var msg in msgs)
                        {
                            _msgs.Add(msg.ToString());
                        }
                    });
                }
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
                var hwnd = (System.Windows.Interop.HwndSource.FromDependencyObject(this) as System.Windows.Interop.HwndSource).Handle;
                var windowRect = new System.Drawing.Rectangle();
                GetWindowRect(hwnd,ref windowRect);
                //0x0001 SWP_NOSIZE | 0x0004 SWP_NOZORDER
                //忽略hWndlnsertAfter和cy、cx，即不改变窗口显示顺序和显示长宽，仅移动位置
                SetWindowPos(hwnd,0, windowRect.X + (int)(currentPos.X - _pressedPos.X), windowRect.Y + (int)(currentPos.Y - _pressedPos.Y), 0,0, 0x0001 | 0x0004);
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
    }
}
