using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Syncfusion.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Helpers;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    class LocalIntelWindow : BaseWindow
    {
        /// <summary>
        /// 每个略缩图中间的间隔
        /// </summary>
        private static readonly int SPAN = 1;
        struct LocalIntelWindowItem
        {
            public LocalIntelProcSetting ProcSetting;
            public IntPtr ThumbHWnd;
            /// <summary>
            /// 当前略缩图在预警窗口中的显示位置
            /// </summary>
            public WindowCaptureHelper.Rect ThumbRect;
        }
        private readonly Dictionary<string, LocalIntelWindowItem> _intelDics = new Dictionary<string, LocalIntelWindowItem>();
        private Microsoft.UI.Windowing.AppWindow _appWindow;
        private IntPtr _windowHandle;
        
        public LocalIntelWindow()
        {
            _appWindow = Helpers.WindowHelper.GetAppWindow(this);
            _windowHandle = WindowHelper.GetWindowHandle(this);
            Helpers.WindowHelper.HideTitleBar(this);
            Helpers.WindowHelper.TopMost(this);
            EnableRightMouseMove();
        }
        #region 鼠标右键移动窗口
        private System.Timers.Timer _pointerTimer;
        private int xOffset, yOffset;
        private void EnableRightMouseMove()
        {
            MainUIElement.PointerPressed += MainUIElement_PointerPressed;
            MainUIElement.PointerReleased += MainUIElement_PointerReleased;
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
            _pointerTimer.Stop();
        }

        private void MainUIElement_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
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
                        _intelDics.Add(procSetting.Name, newItem);
                        _appWindow.Resize(new Windows.Graphics.SizeInt32(newItem.ThumbRect.Right, _intelDics.Max(p => p.Value.ThumbRect.Bottom)));
                        UpdateThumb(thumbHWdn, newItem.ThumbRect, new WindowCaptureHelper.Rect(procSetting.X, procSetting.Y, procSetting.X + procSetting.Width, procSetting.Y + procSetting.Height));
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
                        item.ThumbRect.Left = w;
                        item.ThumbRect.Right = w + item.ProcSetting.Width;
                        w = item.ThumbRect.Right + SPAN;
                        UpdateThumb(item.ThumbHWnd, item.ThumbRect, new WindowCaptureHelper.Rect(item.ProcSetting.X, item.ProcSetting.Y, item.ProcSetting.X + item.ProcSetting.Width, procSetting.Y + item.ProcSetting.Height));
                    }
                    _appWindow.Resize(new Windows.Graphics.SizeInt32(w - SPAN, _appWindow.Size.Height));
                }
                else
                {
                    //没有监控则关闭窗口
                    Close();
                }
                return true;
            }
            else
            {
                Core.Log.Error($"尝试移除不存在的进程{procSetting?.Name}");
                return false;
            }
        }
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
    }
}
