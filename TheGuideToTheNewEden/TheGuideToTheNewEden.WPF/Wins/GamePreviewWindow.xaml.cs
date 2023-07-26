using ESI.NET.Models.Fleets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WinCore;

namespace TheGuideToTheNewEden.WPF.Wins
{
    public partial class GamePreviewWindow : Window
    {
        private IntPtr _sourceHWnd = IntPtr.Zero;
        private IntPtr _thumbHWnd = IntPtr.Zero;
        private IntPtr _thisHWnd = IntPtr.Zero;
        private string _processId;
        public GamePreviewWindow(string processId)
        {
            _processId = processId;
            InitializeComponent();
            SizeChanged += GamePreviewWindow_SizeChanged;
            Closed += GamePreviewWindow_Closed;
            Closing += GamePreviewWindow_Closing;
        }
        /// <summary>
        /// 主动关闭窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GamePreviewWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            OnStop?.Invoke(_processId);
        }

        private void GamePreviewWindow_Closed(object sender, EventArgs e)
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumbHWnd);
            }
        }

        #region 私有方法
        private void GamePreviewWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                UpdateThumbDestination();
            }
        }
        private void UpdateThumbDestination()
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                try
                {
                    var targetClientRect = new System.Drawing.Rectangle();
                    Win32.GetClientRect(_thisHWnd, ref targetClientRect);
                    int left = 0;
                    int top = 0;
                    int right = targetClientRect.Width;
                    int bottom = targetClientRect.Height;
                    var titleBarHeight = WindowCaptureHelper.GetTitleBarHeight(_sourceHWnd);//去掉标题栏高度;//去掉标题栏高度
                    int widthMargin = 0;//去掉左边白边及右边显示完整
                    var sourceClientRect = new System.Drawing.Rectangle();
                    Win32.GetClientRect(_sourceHWnd, ref sourceClientRect);//源窗口显示区域分辨率大小
                    //目标窗口显示区域，及GamePreviewWindow
                    WindowCaptureHelper.Rect rcD = new WindowCaptureHelper.Rect(left, top, right, bottom);
                    //源窗口捕获区域，即游戏的窗口
                    WindowCaptureHelper.Rect scS = new WindowCaptureHelper.Rect(widthMargin, titleBarHeight, sourceClientRect.Right + widthMargin, sourceClientRect.Bottom);
                    WindowCaptureHelper.UpdateThumbDestination(_thumbHWnd, rcD, scS);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }
        public static IntPtr GetWindowHandle(Window window)
        {
            return new WindowInteropHelper(window).Handle; ;
        }
        #endregion
        #region 公开方法
        public delegate void StopDelegate(string id);
        public event StopDelegate OnStop;
        public void Start(IntPtr sourceHWnd)
        {
            _sourceHWnd = sourceHWnd;
            this.Show();
            _thisHWnd = GetWindowHandle(this);
            _thumbHWnd = WindowCaptureHelper.Show(_thisHWnd, sourceHWnd);
            UpdateThumbDestination();
        }
        public void ActiveSourceWindow()
        {
            WindowHelper.SetForegroundWindow(_sourceHWnd);
        }
        /// <summary>
        /// 被动关闭窗口
        /// </summary>
        public void Stop()
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumbHWnd);
                _thumbHWnd = IntPtr.Zero;
            }
            Closing -= GamePreviewWindow_Closing;
            SizeChanged -= GamePreviewWindow_SizeChanged;
            Closed -= GamePreviewWindow_Closed;
            Close();
        }
        #endregion

    }
}
