using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.PreviewIPC;
using TheGuideToTheNewEden.PreviewIPC.Memory;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Interfaces;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class GamePreviewWindow3 : GamePreviewWindowBase
    {
        private IntPtr _sourceHWnd;
        public GamePreviewWindow3(PreviewItem setting, PreviewSetting previewSetting) : base(setting, previewSetting)
        {

        }
        public override event IGamePreviewWindow.SettingChangedDelegate OnSettingChanged;
        public override event IGamePreviewWindow.StopDelegate OnStop;

        public override void ActiveSourceWindow()
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

        public override void PrivateCancelHighlight()
        {
            if(_setting.ShowPreviewWindow)
                _previewIPC.SendMsg(IPCOp.CancelHighlight);
        }

        public override int GetHeight()
        {
            if (_setting.ShowPreviewWindow)
                return _setting.WinH;
            else
                return 0;
        }

        public override void GetSizeAndPos(out int x, out int y, out int w, out int h)
        {
            if (_setting.ShowPreviewWindow)
            {
                x = _setting.WinX;
                y = _setting.WinY;
                w = _setting.WinW;
                h = _setting.WinH;
            }
            else
            {
                x = 0; y = 0; w = 0; h = 0;
            }
        }

        public override int GetWidth()
        {
            if (_setting.ShowPreviewWindow)
                return _setting.WinW;
            else
                return 0;
        }

        public override void PrivateHideWindow()
        {
            if (_setting.ShowPreviewWindow)
                _previewIPC.SendMsg(IPCOp.Hide);
        }

        public override void PrivateHighlight()
        {
            if (_setting.ShowPreviewWindow)
            {
                _previewIPC.SendMsg(IPCOp.Highlight, new int[] {(int)_setting.HighlightMarginLeft,
                (int)_setting.HighlightMarginRight,
                (int)_setting.HighlightMarginTop,
                (int)_setting.HighlightMarginBottom});
            }
        }

        public override void SetPos(int x, int y)
        {
            if (_setting.ShowPreviewWindow)
            {
                _setting.WinX = x;
                _setting.WinY = y;
                _previewIPC.SendMsg(IPCOp.SetPos, new int[] { x, y });
            }
        }

        public override void SetSize(int w, int h)
        {
            if (_setting.ShowPreviewWindow)
            {
                _setting.WinW = w;
                _setting.WinH = h;
                _previewIPC.SendMsg(IPCOp.SetSize, new int[] { w, h });
            }
        }

        public override void PrivateShowWindow(bool hHighlight = false)
        {
            if (_setting.ShowPreviewWindow)
            {
                _previewIPC.SendMsg(IPCOp.Show);
                if (hHighlight)
                {
                    Highlight();
                }
            }
        }

        private IPreviewIPC _previewIPC;
        public override void PrivateStart(IntPtr sourceHWnd)
        {
            _sourceHWnd = sourceHWnd;
            if (_setting.ShowPreviewWindow)
            {
                _previewIPC = Services.MemoryIPCService.Create(sourceHWnd);
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PreviewWindow", "TheGuideToTheNewEden.PreviewWindow.exe");
                if (!File.Exists(path))
                {
                    throw new Exception("Do not exist file previewWindow.exe");
                }
                try
                {
                    List<string> args = new List<string>()
                {
                    sourceHWnd.ToString(),
                    _setting.Name == null ? string.Empty : _setting.Name,
                    _setting.WinW.ToString(),
                    _setting.WinH.ToString(),
                    _setting.WinX.ToString(),
                    _setting.WinY.ToString(),
                    _setting.HighlightColor.A.ToString(),
                    _setting.HighlightColor.R.ToString(),
                    _setting.HighlightColor.G.ToString(),
                    _setting.HighlightColor.B.ToString(),
                    _setting.OverlapOpacity.ToString(),
                    Helpers.WindowHelper.MainWindow.GetWindowHandle().ToString(),
                 };
                    // 启动进程
                    Process.Start(path, args);
                    MonitorMsg();
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex);
                    Services.MemoryIPCService.Dispose(_previewIPC);
                }
            }
        }
        private CancellationTokenSource _ancellationTokenSource;
        private void MonitorMsg()
        {
            _ancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = _ancellationTokenSource.Token;
            Task.Run(() =>
            {
                while (true)
                {
                    if(cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    _previewIPC.TryGetMsg(out var op, out var msgs);
                    if(op == IPCOp.UpdateSizeAndPos)
                    {
                        _setting.WinW = msgs[0];
                        _setting.WinH = msgs[1];
                        _setting.WinX = msgs[2];
                        _setting.WinY = msgs[3];
                        _previewIPC.SendMsg(IPCOp.Handled);
                        OnSettingChanged?.Invoke(_setting);
                    }
                    Thread.Sleep(100);
                }
            });
            
        }
        public override void UpdateThumbnail(int left = 0, int right = 0, int top = 0, int bottom = 0)
        {
            if (_setting.ShowPreviewWindow)
                _previewIPC.SendMsg(IPCOp.UpdateSizeAndPos, new int[] { left, right, top, bottom });
        }
        public override void Stop()
        {
            base.Stop();
            if (_setting.ShowPreviewWindow)
            {
                _ancellationTokenSource?.Cancel();
                Task.Run(() =>
                {
                    Services.MemoryIPCService.Dispose(_previewIPC);
                });
            }
        }
    }
}
