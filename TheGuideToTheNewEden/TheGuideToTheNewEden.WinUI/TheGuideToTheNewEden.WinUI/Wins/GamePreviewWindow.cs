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
using TheGuideToTheNewEden.Core;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class GamePreviewWindow: BaseWindow
    {
        //TODO:监控源窗口大小变化自带修改目标窗口显示，目前只有监控目标窗口变化
        public PreviewItem Setting { get => _setting; }
        private readonly PreviewItem _setting;
        private readonly AppWindow _appWindow;
        private readonly IntPtr _windowHandle = IntPtr.Zero;
        private OverlappedPresenter _presenter;
        public GamePreviewWindow(PreviewItem setting):base()
        {
            _setting = setting;
            SetSmallTitleBar();
            HideAppDisplayName();
            SetHeadText(setting.Name);
            _presenter = Helpers.WindowHelper.GetOverlappedPresenter(this);
            _presenter.IsAlwaysOnTop = true;
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
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(_setting.HighlightColor.A, _setting.HighlightColor.R, _setting.HighlightColor.G, _setting.HighlightColor.B)),
            };
            MainContent = content;
            content.PointerReleased += Content_PointerReleased;
            content.PointerWheelChanged += Content_PointerWheelChanged;
            _appWindow.Closing += AppWindow_Closing;
            InitHotkey();
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if(args.DidPositionChange || args.DidSizeChange)
            {
                _setting.WinW = _appWindow.Size.Width;
                _setting.WinH = _appWindow.Size.Height;
                _setting.WinX = _appWindow.Position.X;
                _setting.WinY = _appWindow.Position.Y;
                OnSettingChanged?.Invoke(_setting);
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
            _appWindow.Changed += AppWindow_Changed;
        }
        public void Show2()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if(!this.Visible)
                {
                    Debug.WriteLine("Activate");
                    _presenter.IsAlwaysOnTop = true;
                    this.Restore();
                }
            });
        }
        public void Hide2()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                _presenter.IsAlwaysOnTop = false;
                this.Minimize();
            });
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
                    Log.Error(ex);
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
            Closed -= GamePreviewWindow_Closed;
            _appWindow.Closing -= AppWindow_Closing;
            _appWindow.Changed -= AppWindow_Changed;
            HotkeyService.OnKeyboardClicked -= HotkeyService_OnKeyboardClicked;
            (MainContent as UIElement).PointerReleased += Content_PointerReleased;
            (MainContent as UIElement).PointerWheelChanged += Content_PointerWheelChanged;
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
            if(keys.Count != 0)
            {
                //TODO:无法获取按键事件
                //if(keys.Count == 1)
                //{
                //    if (keys[0].Name.Equals("Esc", StringComparison.OrdinalIgnoreCase))
                //    {
                //        var foregroundW = Helpers.ShowWindowHelper.GetForegroundWindow();
                //        if(foregroundW == _setting.ProcessInfo.MainWindowHandle)
                //        {
                //            OnStop?.Invoke(_setting);//交给调用者处理关闭
                //            return;
                //        }
                //    }
                //}
                foreach (var key in _keys)
                {
                    if (!keys.Where(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)).Any())
                    {
                        return;
                    }
                }
                ActiveSourceWindow();
            }
        }
        #endregion
        /// <summary>
        /// 高亮
        /// </summary>
        public void Highlight()
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                try
                {
                    int left = 0;
                    int top = _setting.ShowTitleBar ? TitleBarHeight : 0;
                    int right = _appWindow.ClientSize.Width;
                    int bottom = _appWindow.ClientSize.Height;
                    var titleBarHeight = WindowHelper.GetTitleBarHeight(_sourceHWnd);//去掉标题栏高度
                    int widthMargin = WindowHelper.GetBorderWidth(_sourceHWnd);//去掉左边白边及右边显示完整
                    var clientRect = new System.Drawing.Rectangle();
                    Win32.GetClientRect(_sourceHWnd, ref clientRect);//源窗口显示区域分辨率大小
                    //目标窗口显示区域，及GamePreviewWindow
                    WindowCaptureHelper.Rect rcD = new WindowCaptureHelper.Rect(left, top, right, bottom - 6);
                    //源窗口捕获区域，即游戏的窗口
                    WindowCaptureHelper.Rect scS = new WindowCaptureHelper.Rect(widthMargin, titleBarHeight, clientRect.Right + widthMargin, clientRect.Bottom);
                    WindowCaptureHelper.UpdateThumbDestination(_thumbHWnd, rcD, scS);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }
        /// <summary>
        /// 取消高亮
        /// </summary>
        public void CancelHighlight()
        {
            Debug.WriteLine(222);
            UpdateThumbDestination();
        }
        /// <summary>
        /// 设置窗口尺寸
        /// </summary>
        /// <param name="w"></param>
        /// <param name="h"></param>
        public void SetSize(int w, int h)
        {
            _appWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
        }
        public void SetPos(int x, int y)
        {
            Helpers.WindowHelper.MoveToScreen(this, x, y);
        }
        public void GetSizeAndPos(out int x, out int y,out int w, out int h)
        {
            x = _appWindow.Position.X;
            y = _appWindow.Position.Y;
            w = _appWindow.ClientSize.Width;
            h = _appWindow.ClientSize.Height;
        }
        public int GetWidth()
        {
            return _appWindow.ClientSize.Width;
        }
        public int GetHeight()
        {
            return _appWindow.ClientSize.Height;
        }
    }
}
