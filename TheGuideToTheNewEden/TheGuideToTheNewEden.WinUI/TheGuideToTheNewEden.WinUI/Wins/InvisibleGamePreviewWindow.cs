using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WinUI.Interfaces;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class InvisibleGamePreviewWindow : GamePreviewWindowBase
    {
        public InvisibleGamePreviewWindow(PreviewItem setting, PreviewSetting previewSetting) : base(setting, previewSetting)
        {
        }

        public override event IGamePreviewWindow.SettingChangedDelegate OnSettingChanged;
        public override event IGamePreviewWindow.StopDelegate OnStop;

        public override int GetHeight()
        {
            return 0;
        }

        public override void GetSizeAndPos(out int x, out int y, out int w, out int h)
        {
            x = 0;
            y = 0;
            w = 0;
            h = 0;
        }

        public override int GetWidth()
        {
            return 0;
        }

        public override void PrivateCancelHighlight()
        {
            
        }

        public override void PrivateHideWindow()
        {
            
        }

        public override void PrivateHighlight()
        {
            
        }

        public override void PrivateShowWindow(bool hHighlight = false)
        {
            
        }

        public override void PrivateStart(IntPtr sourceHWnd)
        {
           
        }

        public override void SetPos(int x, int y)
        {
            
        }

        public override void SetSize(int w, int h)
        {
            
        }

        public override void UpdateThumbnail(int left = 0, int right = 0, int top = 0, int bottom = 0)
        {
            
        }
    }
}
