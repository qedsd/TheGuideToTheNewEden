using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Syncfusion.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Common;
using TheGuideToTheNewEden.WinUI.Helpers;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    class LocalIntelWindow : BaseWindow
    {
        /// <summary>
        /// 每个略缩图中间的间隔
        /// </summary>
        private static readonly int SPAN = 1;
        class LocalIntelWindowItem
        {
            public LocalIntelProcSetting ProcSetting { get; set; }
            public IntPtr ThumbHWnd { get; set; }
            /// <summary>
            /// 当前略缩图在预警窗口中的显示位置
            /// </summary>
            public WindowCaptureHelper.Rect ThumbRect { get; set; }
        }
        private readonly Dictionary<string, LocalIntelWindowItem> _intelDics = new Dictionary<string, LocalIntelWindowItem>();
        private readonly Microsoft.UI.Windowing.AppWindow _appWindow;
        private readonly IntPtr _windowHandle;
        private int _refreshSpan;

        public LocalIntelWindow(int refreshSpan)
        {
            _refreshSpan = refreshSpan;
            _appWindow = Helpers.WindowHelper.GetAppWindow(this);
            _windowHandle = WindowHelper.GetWindowHandle(this);
            Helpers.WindowHelper.HideTitleBar2(this);
            Helpers.WindowHelper.TopMost(this);
            _appWindow.IsShownInSwitchers = false;
            EnableRightMouseMove();
        }
        #region 鼠标右键移动窗口
        private System.Timers.Timer _pointerTimer;
        private int xOffset, yOffset;
        private void EnableRightMouseMove()
        {
            MainUIElement.PointerPressed += MainUIElement_PointerPressed;
            _pointerTimer = new System.Timers.Timer()
            {
                AutoReset = true,
                Interval = 10,
            };
            _pointerTimer.Elapsed += PointerTimer_Elapsed;
        }
        
        private void PointerTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            System.Drawing.Point lpPoint = new System.Drawing.Point();
            Helpers.Win32Helper.GetCursorPos(ref lpPoint);
            Debug.WriteLine($"{lpPoint.X} {lpPoint.Y} {_appWindow.Position.X} {_appWindow.Position.Y}");
            _appWindow.Move(new Windows.Graphics.PointInt32(lpPoint.X - _appWindow.Size.Width / 2 - xOffset, lpPoint.Y - _appWindow.Size.Height / 2 - yOffset));

        }
        private void MainUIElement_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            MainUIElement.PointerReleased -= MainUIElement_PointerReleased;
            StartScreenshot();
            _pointerTimer.Stop();
        }

        private void MainUIElement_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as UIElement).Properties.IsRightButtonPressed)
            {
                MainUIElement.PointerReleased += MainUIElement_PointerReleased;
                StopScreenshot();
                System.Drawing.Point lpPoint = new System.Drawing.Point();
                Helpers.Win32Helper.GetCursorPos(ref lpPoint);
                xOffset = lpPoint.X - _appWindow.Position.X - _appWindow.Size.Width / 2;
                yOffset = lpPoint.Y - _appWindow.Position.Y - _appWindow.Size.Height / 2;
                _pointerTimer.Start();
            }
        }
        #endregion

        public bool Add(LocalIntelProcSetting procSetting)
        {
            if(!_intelDics.ContainsKey(procSetting.Name))
            {
                try
                {
                    int fw = 0;//当前窗口宽度
                    if (_intelDics.Count != 0)
                    {
                        fw = _intelDics.LastOrDefault().Value.ThumbRect.Right + SPAN;
                    }
                    var thumbHWdn = RegisterThumb(procSetting.HWnd);
                    if (thumbHWdn != IntPtr.Zero)
                    {
                        LocalIntelWindowItem newItem = new LocalIntelWindowItem()
                        {
                            ProcSetting = procSetting,
                            ThumbRect = new WindowCaptureHelper.Rect(fw, 0, fw + procSetting.Width, procSetting.Height),
                            ThumbHWnd = thumbHWdn
                        };
                        int widthMargin = WindowHelper.GetBorderWidth(procSetting.HWnd);//去掉左边白边及右边显示完整
                        _intelDics.Add(procSetting.Name, newItem);
                        _appWindow.Resize(new Windows.Graphics.SizeInt32(newItem.ThumbRect.Right, _intelDics.Max(p => p.Value.ThumbRect.Bottom)));
                        UpdateThumb(thumbHWdn, newItem.ThumbRect, new WindowCaptureHelper.Rect(procSetting.X + widthMargin, procSetting.Y, procSetting.X + procSetting.Width + widthMargin, procSetting.Y + procSetting.Height));
                        StartScreenshot();
                        return true;
                    }
                    else
                    {
                        Core.Log.Error("注册Thumbnail失败");
                        return false;
                    }
                }
                catch(Exception ex)
                {
                    Core.Log.Error(ex);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        public bool Remve(LocalIntelProcSetting procSetting)
        {
            if(_intelDics.TryGetValue(procSetting?.Name, out LocalIntelWindowItem newItem))
            {
                UnRegisterThumb(newItem.ThumbHWnd);
                _intelDics.Remove(procSetting.Name);
                if(_intelDics.Count > 0)
                {
                    int w = 0;
                    //重新调整剩余的略缩图
                    var items = _intelDics.Values.ToArray();
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i];
                        item.ThumbRect = new WindowCaptureHelper.Rect()
                        {
                            Left = w,
                            Right = w + item.ProcSetting.Width,
                            Top = item.ThumbRect.Top,
                            Bottom = item.ThumbRect.Bottom,
                        };
                        w = item.ThumbRect.Right + SPAN;
                        int widthMargin = WindowHelper.GetBorderWidth(procSetting.HWnd);//去掉左边白边及右边显示完整
                        UpdateThumb(item.ThumbHWnd, item.ThumbRect, new WindowCaptureHelper.Rect(item.ProcSetting.X + widthMargin, item.ProcSetting.Y, item.ProcSetting.X + item.ProcSetting.Width + widthMargin, procSetting.Y + item.ProcSetting.Height));
                    }
                    _appWindow.Resize(new Windows.Graphics.SizeInt32(w - SPAN, _intelDics.Max(p => p.Value.ThumbRect.Bottom)));
                }
                else
                {
                    //没有监控则关闭窗口
                    Close();
                    StopScreenshot();
                }
                return true;
            }
            else
            {
                Core.Log.Error($"尝试移除不存在的进程{procSetting?.Name}");
                return false;
            }
        }

        #region 略缩图显示
        private IntPtr RegisterThumb(IntPtr hwdn)
        {
            return WindowCaptureHelper.RegisterThumbnail(_windowHandle, hwdn);
        }
        private void UpdateThumb(IntPtr thumbHWnd, WindowCaptureHelper.Rect destination, WindowCaptureHelper.Rect source)
        {
            WindowCaptureHelper.UpdateThumbDestination(thumbHWnd, destination, source);
        }
        private void UnRegisterThumb(IntPtr hwdn)
        {
            WindowCaptureHelper.HideThumb(hwdn);
        }
        #endregion

        #region 定期截图
        private bool _stopScreenshot = true;
        private void StartScreenshot()
        {
            if(!_stopScreenshot)
            {
                return;
            }
            _stopScreenshot = false;
            Task.Run(() =>
            {
                while(true)
                {
                    if(_stopScreenshot)
                    {
                        return;
                    }
                    try
                    {
                        System.Drawing.Point point = new System.Drawing.Point();
                        Win32.ClientToScreen(_windowHandle, ref point);
                        //截取整个预警窗口图
                        var img = Helpers.WindowCaptureHelper.GetScreenshot(point.X, point.Y, _appWindow.ClientSize.Width, _appWindow.ClientSize.Height);
                        if (img != null)
                        {
                            //按进程分割图像
                            foreach (var item in _intelDics.Values)
                            {
                                var cutBitmap = ImageHelper.ImageToBitmap(img, new Rectangle(item.ThumbRect.Left, item.ThumbRect.Top, item.ThumbRect.Right - item.ThumbRect.Left, item.ThumbRect.Bottom - item.ThumbRect.Top));
                                //ImageHelper.SaveImageToFile(cutBitmap, AppDomain.CurrentDomain.BaseDirectory, item.ProcSetting.Name, System.Drawing.Imaging.ImageFormat.Png);
                                item.ProcSetting.ChangeScreenshot(cutBitmap.Clone() as Bitmap);
                                cutBitmap.Dispose();
                            }
                            img.Dispose();
                        }
                        Thread.Sleep(_refreshSpan);
                    }
                    catch(Exception ex)
                    {
                        Core.Log.Error(ex);
                    }
                }
            });
        }
        private void StopScreenshot()
        {
            _stopScreenshot = true;
        }
        #endregion
    }
}
