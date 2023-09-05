using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Interfaces;
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
        public GamePreviewWindow2(PreviewItem setting, PreviewSetting previewSetting)
        {
            _previewSetting = previewSetting;
            _setting = setting;
            _appWindow = Helpers.WindowHelper.GetAppWindow(this);
            //_appWindow.IsShownInSwitchers = false;
            if (_setting.WinX != -1 && _setting.WinY != -1)
            {
                Helpers.WindowHelper.MoveToScreen(this, _setting.WinX, _setting.WinY);
            }
            Helpers.WindowHelper.HideTitleBar(this);
            this.SetIsAlwaysOnTop(true);
        }
        public void HideWindow()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                TransparentWindowHelper.TransparentWindow(this, 0);
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

        public void ShowWindow()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                TransparentWindowHelper.TransparentWindow(this, _setting.OverlapOpacity);
            });
        }

        public void UpdateThumbnail(int bottomMargin = 0)
        {
            
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
            _thumbnailWindow = new Window();
            _thumbnailWindow.SetIsShownInSwitchers(false);
            _thumbnailWindow.SetIsAlwaysOnTop(true);
            _thumbnailWindow.AppWindow.Move(new Windows.Graphics.PointInt32(_setting.WinX, _setting.WinY));
            _thumbnailWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(_setting.WinW, _setting.WinH));
            //_thumbnailWindow.MoveAndResize(_setting.WinX, _setting.WinY, _setting.WinW, _setting.WinH);
            _thumbHWnd = WindowCaptureHelper.Show(_thumbnailWindow.GetWindowHandle(), sourceHWnd);
            _thumbnailWindow.Activate();
            _appWindow.Resize(new Windows.Graphics.SizeInt32(_setting.WinW, _setting.WinH));
            UpdateThumbnail();
            this.Activate();
        }

        public void Stop()
        {
            if (_thumbHWnd != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(_thumbHWnd);
                _thumbnailWindow.Close();
                _thumbHWnd = IntPtr.Zero;
            }
            
            this.Close();
        }

        public void Highlight()
        {
            UpdateThumbnail(6);
        }

        public void CancelHighlight()
        {
            UpdateThumbnail();
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
