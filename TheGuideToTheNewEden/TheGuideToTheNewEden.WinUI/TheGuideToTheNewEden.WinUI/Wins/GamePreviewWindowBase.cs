using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    internal abstract class GamePreviewWindowBase : IGamePreviewWindow
    {
        internal readonly PreviewItem _setting;
        internal readonly PreviewSetting _previewSetting;
        internal GamePreviewWindowBase(PreviewItem setting, PreviewSetting previewSetting) : base()
        {
            _previewSetting = previewSetting;
            _setting = setting;
            InitHotkey();
        }

        #region 快捷键
        private int _hotkeyRegisterId;
        private void InitHotkey()
        {
            //快捷键
            if (!string.IsNullOrEmpty(_setting.HotKey))
            {
                if (HotkeyService.GetHotkeyService(Helpers.WindowHelper.MainWindow.GetWindowHandle()).Register(_setting.HotKey, out _hotkeyRegisterId))
                {
                    HotkeyService.GetHotkeyService(Helpers.WindowHelper.MainWindow.GetWindowHandle()).HotkeyActived -= GamePreviewWindowBase_HotkeyActived;
                    HotkeyService.GetHotkeyService(Helpers.WindowHelper.MainWindow.GetWindowHandle()).HotkeyActived += GamePreviewWindowBase_HotkeyActived;
                    Core.Log.Info($"注册游戏预览窗口热键成功{_setting.HotKey}_{_hotkeyRegisterId}");
                }
                else
                {
                    Core.Log.Error($"注册游戏预览窗口热键失败{_setting.HotKey}");
                }
            }
        }

        private void GamePreviewWindowBase_HotkeyActived(int hotkeyId)
        {
            if (hotkeyId == _hotkeyRegisterId)
            {
                ActiveSourceWindow();
            }
        }
        #endregion



        public abstract event IGamePreviewWindow.SettingChangedDelegate OnSettingChanged;
        public abstract event IGamePreviewWindow.StopDelegate OnStop;

        public abstract void ActiveSourceWindow();
        public abstract void CancelHighlight();
        public abstract int GetHeight();
        public abstract void GetSizeAndPos(out int x, out int y, out int w, out int h);
        public abstract int GetWidth();
        public abstract void HideWindow();
        public abstract void Highlight();
        public virtual bool IsHideOnForeground()
        {
            return _setting.HideOnForeground;
        }
        public virtual bool IsHighlight()
        {
            return _setting.Highlight;
        }
        public abstract void SetPos(int x, int y);
        public abstract void SetSize(int w, int h);
        public abstract void ShowWindow(bool hHighlight = false);
        public abstract void Start(IntPtr sourceHWnd);
        public virtual void Stop()
        {
            HotkeyService.GetHotkeyService(Helpers.WindowHelper.MainWindow.GetWindowHandle()).Unregister(_hotkeyRegisterId);
        }
        public abstract void UpdateThumbnail(int left = 0, int right = 0, int top = 0, int bottom = 0);

        public PreviewItem GetSetting()
        {
            return _setting;
        }
    }
}
