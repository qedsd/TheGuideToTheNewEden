﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
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
    internal abstract class GamePreviewBaseWindowBase : BaseWindow, IGamePreviewWindow
    {
        internal IntPtr _sourceHWnd;
        internal readonly PreviewItem _setting;
        internal readonly PreviewSetting _previewSetting;
        internal GamePreviewBaseWindowBase(PreviewItem setting, PreviewSetting previewSetting, bool useThemeService, bool hideCaptionButton) : base(useThemeService,false, hideCaptionButton)
        {
            _previewSetting = previewSetting;
            _setting = setting;
            InitHotkey();
            BorderHightLightBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(_setting.HighlightColor.A, _setting.HighlightColor.R, _setting.HighlightColor.G, _setting.HighlightColor.B));
            TitleHighlightBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(_setting.TitleHighlightColor.A, _setting.TitleHighlightColor.R, _setting.TitleHighlightColor.G, _setting.TitleHighlightColor.B));
            TitleNormalBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(_setting.TitleNormalColor.A, _setting.TitleNormalColor.R, _setting.TitleNormalColor.G, _setting.TitleNormalColor.B));
        }

        #region 快捷键
        private int _hotkeyRegisterId;
        private void InitHotkey()
        {
            //快捷键
            if (!string.IsNullOrEmpty(_setting.HotKey))
            {
                if (HotkeyService.GetHotkeyService(this.GetWindowHandle()).Register(_setting.HotKey, out _hotkeyRegisterId))
                {
                    HotkeyService.GetHotkeyService(this.GetWindowHandle()).HotkeyActived -= GamePreviewWindowBase_HotkeyActived;
                    HotkeyService.GetHotkeyService(this.GetWindowHandle()).HotkeyActived += GamePreviewWindowBase_HotkeyActived;
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

        public virtual void ActiveSourceWindow()
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
        public void CancelHighlight()
        {
            if (!Isighlight) return;
            Isighlight = false;
            PrivateCancelHighlight();
        }
        public abstract void PrivateCancelHighlight();
        public abstract int GetHeight();
        public abstract void GetSizeAndPos(out int x, out int y, out int w, out int h);
        public abstract int GetWidth();
        public void HideWindow()
        {
            if (!IsShowing) return;
            IsShowing = false;
            PrivateHideWindow();
        }
        public abstract void PrivateHideWindow();
        public void Highlight()
        {
            if (Isighlight) return;
            Isighlight = true;
            PrivateHighlight();
        }
        public abstract void PrivateHighlight();
        public virtual bool IsHideOnForeground()
        {
            return _setting.HideOnForeground;
        }
        public virtual bool IsHighlight()
        {
            return _setting.Highlight;
        }
        public bool IsClosed { get; private set; } = false;
        public bool IsShowing { get; set; } = true;
        public bool Isighlight { get; set; } = false;
        public SolidColorBrush BorderHightLightBrush { get;private set; }
        public SolidColorBrush TitleHighlightBrush { get; private set; }
        public SolidColorBrush TitleNormalBrush { get; private set; }
        public abstract void SetPos(int x, int y);
        public abstract void SetSize(int w, int h);
        public void ShowWindow(bool hHighlight = false)
        {
            if (IsClosed || IsShowing) return;
            IsShowing = true;
            PrivateShowWindow(hHighlight);
        }
        public abstract void PrivateShowWindow(bool hHighlight = false);
        public void Start(IntPtr sourceHWnd)
        {
            IsShowing = true;
            _sourceHWnd = sourceHWnd;
            PrivateStart(sourceHWnd);
        }
        public abstract void PrivateStart(IntPtr sourceHWnd);
        public virtual void Stop()
        {
            IsClosed = true;
            HotkeyService.GetHotkeyService(this.GetWindowHandle()).Unregister(_hotkeyRegisterId);
        }
        public abstract void UpdateThumbnail(int left = 0, int right = 0, int top = 0, int bottom = 0);
        public PreviewItem GetSetting()
        {
            return _setting;
        }
    }
}
