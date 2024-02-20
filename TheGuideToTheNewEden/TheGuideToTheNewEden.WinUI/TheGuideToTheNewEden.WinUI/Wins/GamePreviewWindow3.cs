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
using TheGuideToTheNewEden.WinUI.Interfaces;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class GamePreviewWindow3 : GamePreviewWindowBase
    {
        public GamePreviewWindow3(PreviewItem setting, PreviewSetting previewSetting) : base(setting, previewSetting)
        {

        }
        public override event IGamePreviewWindow.SettingChangedDelegate OnSettingChanged;
        public override event IGamePreviewWindow.StopDelegate OnStop;

        public override void ActiveSourceWindow()
        {
            _previewIPC.SendMsg(IPCOp.ActiveSourceWindow);
        }

        public override void CancelHighlight()
        {
            _previewIPC.SendMsg(IPCOp.CancelHighlight);
        }

        public override int GetHeight()
        {
            var msgs = _previewIPC.GetMsg(IPCOp.GetHeight);
            return msgs[0];
        }

        public override void GetSizeAndPos(out int x, out int y, out int w, out int h)
        {
            var msgs = _previewIPC.GetMsg(IPCOp.GetSizeAndPos);
            x = msgs[0];
            y = msgs[1];
            w = msgs[2];
            h = msgs[3];
        }

        public override int GetWidth()
        {
            var msgs = _previewIPC.GetMsg(IPCOp.GetWidth);
            return msgs[0];
        }

        public override void HideWindow()
        {
            _previewIPC.SendMsg(IPCOp.Hide);
        }

        public override void Highlight()
        {
            _previewIPC.SendMsg(IPCOp.Highlight);
        }

        public override void SetPos(int x, int y)
        {
            _previewIPC.SendMsg(IPCOp.SetPos, new int[] { x, y });
        }

        public override void SetSize(int w, int h)
        {
            _previewIPC.SendMsg(IPCOp.SetSize, new int[] { w, h });
        }

        public override void ShowWindow(bool hHighlight = false)
        {
            _previewIPC.SendMsg(IPCOp.Show);
        }

        private IPreviewIPC _previewIPC;
        public override void Start(IntPtr sourceHWnd)
        {
            _previewIPC = new MemoryIPC(sourceHWnd.ToString());
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PreviewWindow", "TheGuideToTheNewEden.PreviewWindow.exe");
            if(!File.Exists(path))
            {
                throw new Exception("Do not exist file previewWindow.exe");
            }
            try
            {
                string pipeName = $"Preview{Assembly.GetExecutingAssembly().GetName().Version}";
                List<string> args = new List<string>()
                {
                    pipeName,sourceHWnd.ToString()
                 };
                // 启动进程
                Process.Start(path, args);
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                _previewIPC?.Dispose();
            }
        }

        public override void UpdateThumbnail(int left = 0, int right = 0, int top = 0, int bottom = 0)
        {
            _previewIPC.SendMsg(IPCOp.UpdateSizeAndPos, new int[] { left, right, top, bottom });
        }
    }
}
