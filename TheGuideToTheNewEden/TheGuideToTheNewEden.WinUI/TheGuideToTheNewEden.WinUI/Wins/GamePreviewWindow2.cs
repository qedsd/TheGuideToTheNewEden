﻿using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WinUI.Common;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Interfaces;
using TheGuideToTheNewEden.WinUI.Services;
using Vanara.PInvoke;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class GamePreviewWindow2 : Window, IGamePreviewWindow
    {
        private Window _thumbnailWindow;
        private readonly AppWindow _appWindow;
        private IntPtr _sourceHWnd = IntPtr.Zero;
        private IntPtr _thumbHWnd = IntPtr.Zero;
        private readonly PreviewItem _setting;
        private readonly PreviewSetting _previewSetting;
        private readonly OverlappedPresenter _presenter;
        /// <summary>
        /// 必须放到全局变量，避免被GC回收
        /// </summary>
        private ComCtl32.SUBCLASSPROC _wndProcHandler;
        public GamePreviewWindow2(PreviewItem setting, PreviewSetting previewSetting)
        {
            _previewSetting = previewSetting;
            _setting = setting;
            _appWindow = Helpers.WindowHelper.GetAppWindow(this);
            _presenter = Helpers.WindowHelper.GetOverlappedPresenter(this);
            ExtendsContentIntoTitleBar = true;
            _appWindow.IsShownInSwitchers = false;
            if (_setting.WinX != -1 && _setting.WinY != -1)
            {
                Helpers.WindowHelper.MoveToScreen(this, _setting.WinX, _setting.WinY);
            }
            this.SetIsAlwaysOnTop(true);
            Helpers.TransparentWindowHelper.TransparentWindowVisual(this);
            _wndProcHandler = new ComCtl32.SUBCLASSPROC(Helpers.TransparentWindowHelper.WndProc);
            ComCtl32.SetWindowSubclass(WindowHelper.GetWindowHandle(this), _wndProcHandler, 1, IntPtr.Zero);
            StopTitlebarOp();
            InitUI(_setting.Name);
            MonitorWindow();
            InitHotkey();
        }
        #region UI
        private TextBlock _TitleTextBlock;
        private void InitUI(string title)
        {
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
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            };
            content.PointerPressed += Content_PointerPressed;
            content.PointerReleased += Content_PointerReleased;
            content.PointerReleased += Content_PointerReleased1;
            content.PointerWheelChanged += Content_PointerWheelChanged;
            _TitleTextBlock = new TextBlock()
            {
                Margin = new Thickness(10),
                FontSize = 16,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                Text = title
            };
            content.Children.Add(_TitleTextBlock);
            this.Content = content;
        }

        private void Content_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
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

        private void PointerTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            System.Drawing.Point lpPoint = new System.Drawing.Point();
            Helpers.Win32Helper.GetCursorPos(ref lpPoint);
            _appWindow.Move(new Windows.Graphics.PointInt32(lpPoint.X - _appWindow.Size.Width / 2 - xOffset, lpPoint.Y - _appWindow.Size.Height / 2 - yOffset));
        }

        private System.Timers.Timer _pointerTimer;
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
            if (e.GetCurrentPoint(sender as UIElement).Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonReleased)
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
        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;//取消关闭
            OnStop?.Invoke(_setting);//交给调用者处理关闭
        }
        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (_presenter.State == OverlappedPresenterState.Minimized || _presenter.State == OverlappedPresenterState.Maximized)
            {
                //不显示标题栏时屏蔽标题栏的最大化最小化功能
                _presenter.Restore();
                return;
            }
        }
        #endregion
        #region 检测窗口大小、位置更新
        private void MonitorWindow()
        {
            _appWindow.Changed += AppWindow_Changed2;
        }

        private void AppWindow_Changed2(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange || args.DidSizeChange)
            {
                _setting.WinW = _appWindow.Size.Width;
                _setting.WinH = _appWindow.Size.Height;
                _setting.WinX = _appWindow.Position.X;
                _setting.WinY = _appWindow.Position.Y;
                //显示窗口是由用户鼠标控制，修改大小位置后需同步修改_thumbnailWindow
                _thumbnailWindow.AppWindow.Move(new Windows.Graphics.PointInt32(_setting.WinX, _setting.WinY));
                _thumbnailWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(_setting.WinW, _setting.WinH));
                OnSettingChanged?.Invoke(_setting);
                UpdateThumbnail();
            }
        }
        #endregion
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
                    if (Services.HotkeyService.GetHotkeyService(this.GetWindowHandle()).Register(_setting.HotKey, out _hotkeyRegisterId))
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
        public void HideWindow()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                RestoreTitlebarOp();
                _thumbnailWindow?.Hide();
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
                _thumbnailWindow?.Show();
                this.Show();
                StopTitlebarOp();
                UpdateThumbnail(hHighlight ? 6 : 0);
            });
        }
        public bool IsHideOnForeground()
        {
            return _setting.HideOnForeground;
        }

        public bool IsHighlight()
        {
            return _setting.Highlight;
        }

        private readonly object _updateThumbnailLocker = new object();
        public void UpdateThumbnail(int bottomMargin = 0)
        {
            lock(_updateThumbnailLocker)
            {
                if (_thumbHWnd != IntPtr.Zero)
                {
                    try
                    {
                        int left = 0;
                        int top = 0;
                        int right = _thumbnailWindow.AppWindow.ClientSize.Width;
                        int bottom = _thumbnailWindow.AppWindow.ClientSize.Height;
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
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                }
            }
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

        public void Start(IntPtr sourceHWnd)
        {
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
            _sourceHWnd = sourceHWnd;

            CreateThumbnailWindow();
            _thumbHWnd = WindowCaptureHelper.Show(_thumbnailWindow.GetWindowHandle(), sourceHWnd);
            _thumbnailWindow.Activate();
            _appWindow.Resize(new Windows.Graphics.SizeInt32(_setting.WinW, _setting.WinH));
            UpdateThumbnail();
            this.Activate();
        }
        private void CreateThumbnailWindow()
        {
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
            _thumbnailWindow = new Window()
            {
                Content = content
            };
            _thumbnailWindow.ExtendsContentIntoTitleBar = true;
            _thumbnailWindow.SetIsShownInSwitchers(false);
            _thumbnailWindow.SetIsAlwaysOnTop(true);
            _thumbnailWindow.AppWindow.Move(new Windows.Graphics.PointInt32(_setting.WinX, _setting.WinY));
            _thumbnailWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(_setting.WinW, _setting.WinH));
            TransparentWindowHelper.TransparentWindow(_thumbnailWindow, _setting.OverlapOpacity);
        }

        public void Stop()
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumbHWnd);
                _thumbnailWindow.Close();
                _thumbHWnd = IntPtr.Zero;
            }
            KeyboardService.OnKeyboardClicked -= HotkeyService_OnKeyboardClicked;
            RestoreTitlebarOp();
            this.Close();
        }

        public void Highlight()
        {
            UpdateThumbnail(6);
        }

        public void CancelHighlight()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                UpdateThumbnail();
            });
        }

        public void SetSize(int w, int h)
        {
            _appWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
        }

        public void SetPos(int x, int y)
        {
            Helpers.WindowHelper.MoveToScreen(this, x, y);
        }

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

        public event IGamePreviewWindow.SettingChangedDelegate OnSettingChanged;
        public event IGamePreviewWindow.StopDelegate OnStop;
    }
}