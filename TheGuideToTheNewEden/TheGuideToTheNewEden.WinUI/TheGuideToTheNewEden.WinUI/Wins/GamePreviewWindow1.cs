using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Interfaces;
using Microsoft.UI.Xaml.Input;
using System.Diagnostics;
using System.Timers;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.WinUI.Common;
using TheGuideToTheNewEden.WinUI.Services;
using Microsoft.UI.Input;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class GamePreviewWindow1 : BaseWindow, IGamePreviewWindow
    {
        private IntPtr _sourceHWnd = IntPtr.Zero;
        private IntPtr _thumbHWnd = IntPtr.Zero;
        private readonly PreviewItem _setting;
        private readonly AppWindow _appWindow;
        private readonly IntPtr _windowHandle = IntPtr.Zero;
        private readonly OverlappedPresenter _presenter;
        private readonly PreviewSetting _previewSetting;
        public GamePreviewWindow1(PreviewItem setting, PreviewSetting previewSetting) : base()
        {
            _previewSetting = previewSetting;
            _setting = setting;
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
            InitHotkey();
            InitUI();
            StopTitlebarOp();
        }

        #region UI
        private System.Timers.Timer _pointerTimer;
        private void InitUI()
        {
            SetSmallTitleBar();
            HideAppDisplayName();
            Title = _setting.Name;
            SetHeadText(_setting.Name);
            _pointerTimer = new System.Timers.Timer()
            {
                AutoReset = true,
                Interval = 10,
            };
            _pointerTimer.Elapsed += PointerTimer_Elapsed;
            _pointerTimer = new System.Timers.Timer()
            {
                AutoReset = true,
                Interval = 10,
            };
            _pointerTimer.Elapsed += PointerTimer_Elapsed;
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
        }
        private void PointerTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            System.Drawing.Point lpPoint = new System.Drawing.Point();
            Helpers.Win32Helper.GetCursorPos(ref lpPoint);
            Debug.WriteLine($"{lpPoint.X} {lpPoint.Y} {_appWindow.Position.X} {_appWindow.Position.Y}");
            _appWindow.Move(new Windows.Graphics.PointInt32(lpPoint.X - _appWindow.Size.Width / 2 - xOffset, lpPoint.Y - _appWindow.Size.Height / 2 - yOffset));

        }
        
        private void Content_PointerReleased1(object sender, PointerRoutedEventArgs e)
        {
            _pointerTimer.Stop();
        }
        private int xOffset, yOffset;
        private void Content_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as UIElement).Properties.IsRightButtonPressed)
            {
                System.Drawing.Point lpPoint = new System.Drawing.Point();
                Helpers.Win32Helper.GetCursorPos(ref lpPoint);
                xOffset = lpPoint.X - _appWindow.Position.X - _appWindow.Size.Width / 2;
                yOffset = lpPoint.Y - _appWindow.Position.Y - _appWindow.Size.Height / 2;
                _pointerTimer.Start();
            }
        }

        private void Content_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as UIElement).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
                WindowHelper.SetForegroundWindow_Click(_sourceHWnd);
        }
        #endregion
        #region 屏蔽标题栏操作
        private void StopTitlebarOp()
        {
            _appWindow.Closing += AppWindow_Closing;
            _appWindow.Changed += AppWindow_Changed;
        }
        private void RestoreTitlebarOp()
        {
            _appWindow.Closing -= AppWindow_Closing;
            _appWindow.Changed -= AppWindow_Changed;
        }
        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange)
            {
                if (!Helpers.WindowHelper.IsInWindow(_appWindow.Position.X, _appWindow.Position.Y))
                {
                    //可能是最小化后不显示在屏幕范围内
                    _appWindow.IsShownInSwitchers = true;
                    return;//不保存位置
                }
                else if (_appWindow.IsShownInSwitchers)
                {
                    //最小化恢复正常显示
                    _appWindow.IsShownInSwitchers = false;
                    return;//不保存位置
                }
            }
            if (args.DidPositionChange || args.DidSizeChange)
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
            OnStop?.Invoke(_setting);//交给调用者处理关闭
        }

        #endregion
        /// <summary>
        /// 显示目标窗口
        /// </summary>
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
                if (clientSize.Width <= 0)
                {
                    clientSize.Width = 500;
                }
                if (clientSize.Height <= 0)
                {
                    clientSize.Height = 300;
                }
                _setting.WinH = (int)(_setting.WinW / (float)clientSize.Width * clientSize.Height);
            }
            _appWindow.Resize(new Windows.Graphics.SizeInt32(_setting.WinW, _setting.WinH));
            UpdateThumbnail();
            this.Activate();
            _appWindow.Changed += AppWindow_Changed;
        }
        public void HideWindow()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                RestoreTitlebarOp();
                this.Hide();
                StopTitlebarOp();
                UpdateThumbnail();
            });
        }
        public void ShowWindow(bool hHighlight = false)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                RestoreTitlebarOp();
                this.Show();
                StopTitlebarOp();
                if(hHighlight)
                {
                    UpdateThumbnail((int)_setting.HighlightMarginLeft,(int)_setting.HighlightMarginRight,
                        (int)_setting.HighlightMarginTop, (int)_setting.HighlightMarginBottom);
                }
                else
                {
                    UpdateThumbnail();
                }
            });
        }

        public void UpdateThumbnail(int left1 = 0, int right1 = 0, int top1 = 0, int bottom1 = 0)
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                try
                {
                    int left = left1;
                    int top = (_setting.ShowTitleBar ? (int)(TitleBarHeight * Helpers.WindowHelper.GetDpiScale(this)) : 0 ) + top1;
                    int right = _appWindow.ClientSize.Width - right1;
                    int bottom = _appWindow.ClientSize.Height - bottom1;
                    var titleBarHeight = WindowHelper.GetTitleBarHeight(_sourceHWnd);//去掉标题栏高度
                    int widthMargin = WindowHelper.GetBorderWidth(_sourceHWnd);//去掉左边白边及右边显示完整
                    var clientRect = new System.Drawing.Rectangle();
                    Win32.GetClientRect(_sourceHWnd, ref clientRect);//源窗口显示区域分辨率大小
                    //目标窗口显示区域，即GamePreviewWindow
                    WindowCaptureHelper.Rect rcD = new WindowCaptureHelper.Rect(left, top, right, bottom);
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
        private void GamePreviewWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                UpdateThumbnail();
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
            Close();
        }

        private void GamePreviewWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumbHWnd);
            }
            HotkeyService.GetHotkeyService(_windowHandle).Unregister(_hotkeyRegisterId);
        }

        #region 快捷键
        private int _hotkeyRegisterId;
        private List<string> _keys;
        private void InitHotkey()
        {
            //快捷键
            if (!string.IsNullOrEmpty(_setting.HotKey))
            {
                Core.Log.Debug($"预览窗口初始化快捷键{_setting.HotKey}");
                if (Services.HotkeyService.TryGetHotkeyVK(_setting.HotKey, out _, out _))
                {
                    if (Services.HotkeyService.GetHotkeyService(_windowHandle).Register(_setting.HotKey, out _hotkeyRegisterId))
                    {
                        Core.Log.Info("注册窗口热键成功");
                        var keynames = _setting.HotKey.Split('+');
                        if (keynames.NotNullOrEmpty())
                        {
                            _keys = keynames.ToList();
                            KeyboardService.OnKeyboardClicked += HotkeyService_OnKeyboardClicked;
                        }
                    }
                    else
                    {
                        Core.Log.Info($"注册窗口热键{_setting.HotKey}失败，请检查是否按键冲突");
                        (WindowHelper.MainWindow as BaseWindow)?.ShowError($"注册窗口热键{_setting.HotKey}失败，请检查是否按键冲突");
                    }
                }
                else
                {
                    Core.Log.Error($"不规范热键{_setting.HotKey}");
                    (WindowHelper.MainWindow as BaseWindow)?.ShowError($"不规范热键{_setting.HotKey}");
                }
            }
        }

        private void HotkeyService_OnKeyboardClicked(List<KeyboardHook.KeyboardInfo> keys)
        {
            if (keys.Count != 0)
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
            this.DispatcherQueue.TryEnqueue(() =>
            {
                UpdateThumbnail(6);
            });
        }
        /// <summary>
        /// 取消高亮
        /// </summary>
        public void CancelHighlight()
        {
            UpdateThumbnail();
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
        public void GetSizeAndPos(out int x, out int y, out int w, out int h)
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

        public bool IsHideOnForeground()
        {
            return _setting.HideOnForeground;
        }
        public bool IsHighlight()
        {
            return _setting.Highlight;
        }

        public event IGamePreviewWindow.SettingChangedDelegate OnSettingChanged;
        public event IGamePreviewWindow.StopDelegate OnStop;
    }
}
