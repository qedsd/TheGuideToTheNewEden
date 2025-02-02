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
    internal class GamePreviewWindow2 : GamePreviewBaseWindowBase
    {
        private Window _thumbnailWindow;
        private readonly AppWindow _appWindow;
        private IntPtr _thumbHWnd = IntPtr.Zero;
        private WinUICommunity.ThemeService _themeService;
        public GamePreviewWindow2(PreviewItem setting, PreviewSetting previewSetting) : base(setting, previewSetting, false, true)
        {
            _appWindow = Helpers.WindowHelper.GetAppWindow(this);
            _appWindow.IsShownInSwitchers = false;
            HideAppDisplayName();
            Title = _setting.Name;
            SetHeadText(_setting.Name);
            if (_setting.WinX != -1 && _setting.WinY != -1)
            {
                Helpers.WindowHelper.MoveToScreen(this, _setting.WinX, _setting.WinY);
            }
            this.SetIsAlwaysOnTop(true);
            _themeService = new WinUICommunity.ThemeService();
            _themeService.Initialize(this);
            _themeService.ConfigBackdrop(WinUICommunity.BackdropType.Transparent);
            InitUI(_setting.Name);
            MonitorWindow();
        }
        #region UI
        private TextBlock _titleTextBlock;
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
            content.PointerCaptureLost += Content_PointerCaptureLost;
            content.PointerReleased += Content_PointerReleased;
            content.PointerReleased += Content_PointerReleased1;
            content.PointerWheelChanged += Content_PointerWheelChanged;
            content.KeyUp += Content_KeyUp;
            _titleTextBlock = new TextBlock()
            {
                Margin = new Thickness(10),
                FontSize = 16,
                Foreground = TitleNormalBrush,
                Text = title
            };
            content.Children.Add(_titleTextBlock);
            this.Content = content;
        }

        private void Content_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                OnStop?.Invoke(_setting);//交给调用者处理关闭
            }
        }

        private void Content_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _pointerTimer.Stop();
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
            if(Helpers.Win32Helper.IsKeyDown(0x02))
            {
                System.Drawing.Point lpPoint = new System.Drawing.Point();
                Helpers.Win32Helper.GetCursorPos(ref lpPoint);
                _appWindow.Move(new Windows.Graphics.PointInt32(lpPoint.X - _appWindow.Size.Width / 2 - xOffset, lpPoint.Y - _appWindow.Size.Height / 2 - yOffset));
            }
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
        #region 检测窗口大小、位置更新
        private void MonitorWindow()
        {
            _appWindow.Changed += AppWindow_Changed2;
        }

        private void AppWindow_Changed2(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange)
            {
                if (!Helpers.WindowHelper.IsInWindow(_appWindow.Position.X, _appWindow.Position.Y))
                {
                    //可能是最小化后不显示在屏幕范围内
                    _appWindow.IsShownInSwitchers = true;
                    _thumbnailWindow.AppWindow.Move(new Windows.Graphics.PointInt32(_appWindow.Position.X, _appWindow.Position.Y));
                    _thumbnailWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(_appWindow.Size.Width, _appWindow.Size.Height));
                    UpdateThumbnail();
                    return;
                }
                else if (_appWindow.IsShownInSwitchers)
                {
                    //最小化恢复正常显示
                    _appWindow.IsShownInSwitchers = false;
                }
            }
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
        public override void PrivateHideWindow()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                //RestoreTitlebarOp();
                _thumbnailWindow?.Hide();
                this.Hide();
                //StopTitlebarOp();
                UpdateThumbnail();
            });
        }
        public override void PrivateShowWindow(bool hHighlight = false)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                //RestoreTitlebarOp();
                _thumbnailWindow?.Show();
                this.Activate();
                //StopTitlebarOp();
                if (hHighlight)
                {
                    UpdateThumbnail((int)_setting.HighlightMarginLeft, (int)_setting.HighlightMarginRight,
                        (int)_setting.HighlightMarginTop, (int)_setting.HighlightMarginBottom);
                }
                else
                {
                    UpdateThumbnail();
                }
            });
        }
        public override bool IsHideOnForeground()
        {
            return _setting.HideOnForeground;
        }

        public override bool IsHighlight()
        {
            return _setting.Highlight;
        }

        private readonly object _updateThumbnailLocker = new object();
        public override void UpdateThumbnail(int left1 = 0, int right1 = 0, int top1 = 0, int bottom1 = 0)
        {
            lock(_updateThumbnailLocker)
            {
                if (_thumbHWnd != IntPtr.Zero)
                {
                    try
                    {
                        int left = left1;
                        int top = top1;
                        int right = _thumbnailWindow.AppWindow.ClientSize.Width - right1;
                        int bottom = _thumbnailWindow.AppWindow.ClientSize.Height - bottom1;
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
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                }
            }
        }

        public override void PrivateStart(IntPtr sourceHWnd)
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
                Background = BorderHightLightBrush,
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
            var presenter = Helpers.WindowHelper.GetOverlappedPresenter(_thumbnailWindow);
            presenter.SetBorderAndTitleBar(true, false);
            _thumbnailWindow.SetIsShownInSwitchers(false);
            _thumbnailWindow.SetIsAlwaysOnTop(true);
            _thumbnailWindow.AppWindow.Move(new Windows.Graphics.PointInt32(_setting.WinX, _setting.WinY));
            _thumbnailWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(_setting.WinW, _setting.WinH));
            TransparentWindowHelper.TransparentWindow(_thumbnailWindow, _setting.OverlapOpacity);
        }

        public override void Stop()
        {
            base.Stop();
            if (_thumbHWnd != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumbHWnd);
                _thumbnailWindow.Close();
                _thumbHWnd = IntPtr.Zero;
            }
            this.Close();
        }

        public override void PrivateHighlight()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                _titleTextBlock.Foreground = TitleHighlightBrush;
                UpdateThumbnail((int)_setting.HighlightMarginLeft,
                (int)_setting.HighlightMarginRight,
                (int)_setting.HighlightMarginTop + 2,
                (int)_setting.HighlightMarginBottom);
            });
        }

        public override void PrivateCancelHighlight()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                _titleTextBlock.Foreground = TitleNormalBrush;
                UpdateThumbnail();
            });
        }

        public override void SetSize(int w, int h)
        {
            _appWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
        }

        public override void SetPos(int x, int y)
        {
            Helpers.WindowHelper.MoveToScreen(this, x, y);
        }

        public override void GetSizeAndPos(out int x, out int y, out int w, out int h)
        {
            x = _appWindow.Position.X;
            y = _appWindow.Position.Y;
            w = _appWindow.ClientSize.Width;
            h = _appWindow.ClientSize.Height;
        }

        public override int GetWidth()
        {
            return _appWindow.ClientSize.Width;
        }

        public override int GetHeight()
        {
            return _appWindow.ClientSize.Height;
        }

        public override event IGamePreviewWindow.StopDelegate OnStop;
        public override event IGamePreviewWindow.SettingChangedDelegate OnSettingChanged;
    }
}
