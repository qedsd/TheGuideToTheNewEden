using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WinUI.Common;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.Core.Extensions;
using WinUIEx;
using TheGuideToTheNewEden.WinUI.Services;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class GamePreviewWindow: BaseWindow
    {
        //TODO:监控源窗口大小变化自带修改目标窗口显示，目前只有监控目标窗口变化
        private readonly PreviewItem _setting;
        private readonly AppWindow _appWindow;
        private readonly IntPtr _windowHandle = IntPtr.Zero;
        public GamePreviewWindow(PreviewItem setting):base()
        {
            _setting = setting;
            SetSmallTitleBar();
            HideAppDisplayName();
            SetHeadText(setting.Name);
            var presenter = Helpers.WindowHelper.GetOverlappedPresenter(this);
            presenter.IsAlwaysOnTop = true;
            TransparentWindowHelper.TransparentWindow(this, _setting.OverlapOpacity);
            _windowHandle = Helpers.WindowHelper.GetWindowHandle(this);
            _appWindow = Helpers.WindowHelper.GetAppWindow(this);
            _appWindow.IsShownInSwitchers = false;
            
            if (_setting.WinX != -1 && _setting.WinY != -1)
            {
                Helpers.WindowHelper.MoveToScreen(this, _setting.WinX, _setting.WinY);
            }
            var content = new Microsoft.UI.Xaml.Controls.Grid()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Colors.AliceBlue),
            };
            MainContent = content;
            content.PointerReleased += Content_PointerReleased;
            content.PointerWheelChanged += Content_PointerWheelChanged;
            _appWindow.Closing += AppWindow_Closing;
            this.VisibilityChanged += GamePreviewWindow_VisibilityChanged;
            _appWindow.Changed += AppWindow_Changed;

            InitHotkey();
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if(args.DidPositionChange || args.DidSizeChange)
            {
                _setting.WinW = _appWindow.ClientSize.Width;
                _setting.WinH = _appWindow.ClientSize.Height;
                _setting.WinX = _appWindow.Position.X;
                _setting.WinY = _appWindow.Position.Y;
                OnSettingChanged?.Invoke(_setting);
            }
        }

        private void GamePreviewWindow_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
        {
            if(!args.Visible)
            {
                this.Activate();
            }
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;//预览窗口不自动处理关闭
            if (_setting.ShowTitleBar)
            {
                OnStop?.Invoke(_setting);//交给调用者处理关闭
            }
        }

        private void Content_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ActiveSourceWindow();
        }
        /// <summary>
        /// 显示目标窗口
        /// </summary>
        public void ActiveSourceWindow()
        {
            if (Win32.IsIconic(_sourceHWnd))
            {
                Win32.ShowWindow(_sourceHWnd, 4);
            }
            else
            {
                Win32.ShowWindow(_sourceHWnd, 5);
            }
            Win32.SetForegroundWindow(_sourceHWnd);
        }

        private IntPtr _sourceHWnd = IntPtr.Zero;
        private IntPtr _thumbHWnd = IntPtr.Zero;
        public void Start(IntPtr sourceHWnd)
        {
            _sourceHWnd = sourceHWnd;
            _thumbHWnd = WindowCaptureHelper.Show(_windowHandle, sourceHWnd);
            SizeChanged += GamePreviewWindow_SizeChanged;
            Closed += GamePreviewWindow_Closed;
            if (_setting.WinW == 0 || _setting.WinH == 0)
            {
                _setting.WinW = 500;
                var clientSize = WindowHelper.GetClientRect(_sourceHWnd);
                _setting.WinH = (int)(_setting.WinW / (float)clientSize.Width * clientSize.Height);
            }
            _appWindow.Resize(new Windows.Graphics.SizeInt32(_setting.WinW, _setting.WinH));
            UpdateThumbDestination();
            this.Activate();
        }


        private void UpdateThumbDestination()
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                try
                {
                    int left = 0;
                    int top = _setting.ShowTitleBar? TitleBarHeight : 0;
                    int right = _appWindow.ClientSize.Width;
                    int bottom = _appWindow.ClientSize.Height;
                    var titleBarHeight = WindowHelper.GetTitleBarHeight(_sourceHWnd);//去掉标题栏高度
                    int widthMargin = WindowHelper.GetBorderWidth(_sourceHWnd);//去掉左边白边及右边显示完整
                    var clientRect = new System.Drawing.Rectangle();
                    Win32.GetClientRect(_sourceHWnd, ref clientRect);//源窗口显示区域分辨率大小
                    //目标窗口显示区域，及GamePreviewWindow
                    WindowCaptureHelper.Rect rcD = new WindowCaptureHelper.Rect(left, top, right, bottom);
                    //源窗口捕获区域，即游戏的窗口
                    WindowCaptureHelper.Rect scS = new WindowCaptureHelper.Rect(widthMargin, titleBarHeight, clientRect.Right + widthMargin, clientRect.Bottom);
                    WindowCaptureHelper.UpdateThumbDestination(_thumbHWnd, rcD, scS);
                }
                catch(Exception ex)
                {

                }
            }
        }
        private void GamePreviewWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                UpdateThumbDestination();
            }
        }


        private void Content_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(sender as Grid).Properties;
            if (properties != null)
            {
                if (properties.MouseWheelDelta > 0)
                {
                    _appWindow.Resize(new Windows.Graphics.SizeInt32((int)(_appWindow.Size.Width * 1.05), (int)(_appWindow.Size.Height * 1.05)));
                }
                else
                {
                    _appWindow.Resize(new Windows.Graphics.SizeInt32((int)(_appWindow.Size.Width * 0.95), (int)(_appWindow.Size.Height * 0.95)));
                }
            }
        }
        public void Stop()
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumbHWnd);
                _thumbHWnd = IntPtr.Zero;
            }
            SizeChanged -= GamePreviewWindow_SizeChanged;
            _appWindow.Closing -= AppWindow_Closing;
            this.VisibilityChanged -= GamePreviewWindow_VisibilityChanged;
            HotkeyService.OnKeyboardClicked -= HotkeyService_OnKeyboardClicked;
            Close();
        }

        private void GamePreviewWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumbHWnd);
            }
        }

        public delegate void SettingChangedDelegate(PreviewItem previewItem);
        public event SettingChangedDelegate OnSettingChanged;

        public delegate void StopDelegate(PreviewItem previewItem);
        public event StopDelegate OnStop;


        #region 快捷键
        private List<string> _keys;
        private void InitHotkey()
        {
            //快捷键
            if (!string.IsNullOrEmpty(_setting.HotKey))
            {
                var keynames = _setting.HotKey.Split('+');
                if (keynames.NotNullOrEmpty())
                {
                    _keys = keynames.ToList();
                    HotkeyService.OnKeyboardClicked += HotkeyService_OnKeyboardClicked;
                }
            }
        }

        private void HotkeyService_OnKeyboardClicked(List<KeyboardHook.KeyboardInfo> keys)
        {
            foreach(var key in _keys)
            {
                if(!keys.Where(p=>p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)).Any())
                {
                    return;
                }
            }
            ActiveSourceWindow();
        }
        #endregion
    }
}
