﻿using Microsoft.UI;
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
using System.Threading;
using System.Reflection.Metadata;
using Microsoft.UI.Xaml.Input;
using System.Timers;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class GamePreviewWindow: BaseWindow
    {
        //TODO:监控源窗口大小变化自带修改目标窗口显示，目前只有监控目标窗口变化
        public PreviewItem Setting { get => _setting; }
        private readonly PreviewItem _setting;
        private readonly AppWindow _appWindow;
        private readonly IntPtr _windowHandle = IntPtr.Zero;
        private readonly OverlappedPresenter _presenter;
        private readonly PreviewSetting _previewSetting;
        public GamePreviewWindow(PreviewItem setting, PreviewSetting previewSetting) :base()
        {
            _previewSetting = previewSetting;
            _setting = setting;
            SetSmallTitleBar();
            HideAppDisplayName();
            Title = setting.Name;
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
            content.Children.Add(new TextBlock()
            {
                Text = "不支持最小化游戏窗口",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            MainContent = content;
            MainUIElement.PointerReleased += Content_PointerReleased;
            content.PointerWheelChanged += Content_PointerWheelChanged;
            MainUIElement.PointerPressed += Content_PointerPressed;
            MainUIElement.PointerReleased += Content_PointerReleased1;
            _appWindow.Closing += AppWindow_Closing;
            InitHotkey();
            pointerTimer = new System.Timers.Timer()
            {
                AutoReset = true,
                Interval = 10,
            };
            pointerTimer.Elapsed += PointerTimer_Elapsed;
            if(!Setting.ShowTitleBar)
            {
                SetTitleBar(null);
            }
        }
        //private delegate IntPtr WinProc(IntPtr hWnd, PInvoke.User32.WindowMessage Msg, IntPtr wParam, IntPtr lParam);
        //private WinProc newWndProc = null;

        private void PointerTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            System.Drawing.Point lpPoint = new System.Drawing.Point();
            Helpers.Win32Helper.GetCursorPos(ref lpPoint);
            Debug.WriteLine($"{lpPoint.X} {lpPoint.Y} {_appWindow.Position.X} {_appWindow.Position.Y}");
            _appWindow.Move(new Windows.Graphics.PointInt32(lpPoint.X - _appWindow.Size.Width / 2 - xOffset, lpPoint.Y - _appWindow.Size.Height / 2 - yOffset));
            
        }

        private System.Timers.Timer pointerTimer;
        private void Content_PointerReleased1(object sender, PointerRoutedEventArgs e)
        {
            pointerTimer.Stop();
        }
        private int xOffset, yOffset;
        private void Content_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if(e.GetCurrentPoint(sender as UIElement).Properties.IsRightButtonPressed)
            {
                System.Drawing.Point lpPoint = new System.Drawing.Point();
                Helpers.Win32Helper.GetCursorPos(ref lpPoint);
                xOffset = lpPoint.X - _appWindow.Position.X - _appWindow.Size.Width / 2;
                yOffset = lpPoint.Y - _appWindow.Position.Y - _appWindow.Size.Height / 2;
                pointerTimer.Start();
            }
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if(!Setting.ShowTitleBar &&( _presenter.State == OverlappedPresenterState.Minimized || _presenter.State == OverlappedPresenterState.Maximized))
            {
                //不显示标题栏时屏蔽标题栏的最大化最小化功能
                _presenter.Restore();
                return;
            }
            if (args.DidPositionChange)
            {
                if(!Helpers.WindowHelper.IsInWindow(_appWindow.Position.X, _appWindow.Position.Y))
                {
                    //可能是最小化后不显示在屏幕范围内
                    _appWindow.IsShownInSwitchers = true;
                    return;//不保存位置
                }
                else if(_appWindow.IsShownInSwitchers)
                {
                    //最小化恢复正常显示
                    _appWindow.IsShownInSwitchers = false;
                    return;//不保存位置
                }
            }
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
            if (e.GetCurrentPoint(sender as UIElement).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
                WindowHelper.SetForegroundWindow_Click(_sourceHWnd);
        }
        /// <summary>
        /// 显示目标窗口
        /// </summary>
        public void ActiveSourceWindow()
        {
            Task.Run(() =>
            {
                switch(_previewSetting.SetForegroundWindowMode)
                {
                    case 0: Helpers.WindowHelper.SetForegroundWindow0(_sourceHWnd); break;
                    case 1: Helpers.WindowHelper.SetForegroundWindow1(_sourceHWnd);break;
                    case 2: Helpers.WindowHelper.SetForegroundWindow2(_sourceHWnd); break;
                    case 3: Helpers.WindowHelper.SetForegroundWindow3(_sourceHWnd); break;
                    case 4: Helpers.WindowHelper.SetForegroundWindow4(_sourceHWnd); break;
                    default: Helpers.WindowHelper.SetForegroundWindow0(_sourceHWnd); break;
                }
            });
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
                if(clientSize.Width <= 0)
                {
                    clientSize.Width = 500;
                }
                if(clientSize.Height <= 0)
                {
                    clientSize.Height = 300;
                }
                _setting.WinH = (int)(_setting.WinW / (float)clientSize.Width * clientSize.Height);
            }
            _appWindow.Resize(new Windows.Graphics.SizeInt32(_setting.WinW, _setting.WinH));
            UpdateThumbDestination();
            this.Activate();
            _appWindow.Changed += AppWindow_Changed;
            _UITimer = new DispatcherTimer();
            _UITimer.Interval = TimeSpan.FromMilliseconds(50);
            _UITimer.Tick += UITimer_Tick;
            _UITimer.Start();
        }
        /// <summary>
        /// 与透明度绑定，透明度为0时为不可见
        /// </summary>
        private bool _visible = true;
        private void UITimer_Tick(object sender, object e)
        {
            _UITimer.Stop();
            if(_isVisible != _visible)
            {
                if(_isVisible)
                {
                    TransparentWindowHelper.TransparentWindow(this, _setting.OverlapOpacity);
                    _visible = true;
                }
                else
                {
                    TransparentWindowHelper.TransparentWindow(this, 0);
                    _visible = false;
                }
            }
            _UITimer.Start();
        }
        /// <summary>
        /// 在UI线程执行的timer
        /// </summary>
        private DispatcherTimer _UITimer;
        /// <summary>
        /// 标记当前窗口需不需要显示
        /// </summary>
        private bool _isVisible = true;
        public void Show2()
        {
            _isVisible = true;
        }
        public void Hide2()
        {
            _isVisible = false;
        }

        private void UpdateThumbDestination(int bottomMargin = 0)
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                try
                {
                    int left = 0;
                    int top = Setting.ShowTitleBar ? (int)(TitleBarHeight * Helpers.WindowHelper.GetDpiScale(this)) : 0;
                    int right = _appWindow.ClientSize.Width;
                    int bottom = _appWindow.ClientSize.Height;
                    var titleBarHeight = WindowHelper.GetTitleBarHeight(_sourceHWnd);//去掉标题栏高度
                    int widthMargin = WindowHelper.GetBorderWidth(_sourceHWnd);//去掉左边白边及右边显示完整
                    var clientRect = new System.Drawing.Rectangle();
                    Win32.GetClientRect(_sourceHWnd, ref clientRect);//源窗口显示区域分辨率大小
                    //目标窗口显示区域，及GamePreviewWindow
                    WindowCaptureHelper.Rect rcD = new WindowCaptureHelper.Rect(left, top, right, bottom - bottomMargin);
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
            KeyboardService.OnKeyboardClicked -= HotkeyService_OnKeyboardClicked;
            (MainContent as UIElement).PointerReleased -= Content_PointerReleased;
            (MainContent as UIElement).PointerWheelChanged -= Content_PointerWheelChanged;
            _UITimer.Stop();
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
                Core.Log.Debug($"预览窗口初始化快捷键{_setting.HotKey}");
                var keynames = _setting.HotKey.Split('+');
                if (keynames.NotNullOrEmpty())
                {
                    _keys = keynames.ToList();
                    KeyboardService.OnKeyboardClicked += HotkeyService_OnKeyboardClicked;
                }
            }
        }

        private void HotkeyService_OnKeyboardClicked(List<KeyboardHook.KeyboardInfo> keys)
        {
            if(keys.Count != 0)
            {
                foreach (var key in _keys)
                {
                    if (!keys.Where(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)).Any())
                    {
                        return;
                    }
                }
                //Core.Log.Debug($"捕获到预览窗口激活快捷键{keys.Select(p=>p.Name).ToSeqString(",")}");
                ActiveSourceWindow();
            }
        }
        #endregion
        /// <summary>
        /// 高亮
        /// </summary>
        public void Highlight()
        {
            UpdateThumbDestination(6);
        }
        /// <summary>
        /// 取消高亮
        /// </summary>
        public void CancelHighlight()
        {
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
        /// <summary>
        /// 仅相对于所处的屏幕
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="w"></param>
        /// <param name="h"></param>
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
