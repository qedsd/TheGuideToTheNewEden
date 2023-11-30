using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.GamePreviews;

namespace TheGuideToTheNewEden.WinUI.Interfaces
{
    internal interface IGamePreviewWindow
    {
        bool IsHideOnForeground();
        bool IsHighlight();
        void ActiveSourceWindow();
        void Start(IntPtr sourceHWnd);
        void Stop();
        void ShowWindow(bool hHighlight = false);
        void HideWindow();
        void UpdateThumbnail(int left = 0, int right = 0, int top = 0, int bottom = 0);
        void Highlight();
        void CancelHighlight();
        void SetSize(int w, int h);
        void SetPos(int x, int y);
        void GetSizeAndPos(out int x, out int y, out int w, out int h);
        int GetWidth();
        int GetHeight();
        public delegate void SettingChangedDelegate(PreviewItem previewItem);
        public event SettingChangedDelegate OnSettingChanged;

        public delegate void StopDelegate(PreviewItem previewItem);
        public event StopDelegate OnStop;
    }
}
