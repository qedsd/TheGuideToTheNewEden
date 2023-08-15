using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.GamePreviews
{
    public class PreviewSetting : ObservableObject
    {
        private string processeKeywords = "exefile";
        /// <summary>
        /// 游戏进程名关键词
        /// 按,分割
        /// </summary>
        public string ProcessKeywords
        {
            get => processeKeywords;
            set => SetProperty(ref processeKeywords, value);
        }
        private string switchHotkey_Forward = "F5";
        /// <summary>
        /// 切换运行中的窗口对应的源窗口
        /// </summary>
        public string SwitchHotkey_Forward
        {
            get => switchHotkey_Forward;
            set => SetProperty(ref switchHotkey_Forward, value);
        }
        private string switchHotkey_Backward = "F4";
        /// <summary>
        /// 切换运行中的窗口对应的源窗口
        /// </summary>
        public string SwitchHotkey_Backward
        {
            get => switchHotkey_Backward;
            set => SetProperty(ref switchHotkey_Backward, value);
        }
        private List<PreviewItem> previewItems = new List<PreviewItem>();
        public List<PreviewItem> PreviewItems
        {
            get => previewItems;
            set => SetProperty(ref previewItems, value);
        }

        private int autoLayout;
        /// <summary>
        /// 窗口布局对齐方式
        /// </summary>
        public int AutoLayout
        {
            get => autoLayout;
            set
            {
                SetProperty(ref autoLayout, value);
            }
        }
        private int autoLayoutAnchor;
        /// <summary>
        /// 窗口布局对齐位置
        /// </summary>
        public int AutoLayoutAnchor
        {
            get => autoLayoutAnchor;
            set
            {
                SetProperty(ref autoLayoutAnchor, value);
            }
        }

        private int autoLayoutSpan = 10;
        /// <summary>
        /// 自动对齐间隔
        /// </summary>
        public int AutoLayoutSpan
        {
            get => autoLayoutSpan;
            set
            {
                SetProperty(ref autoLayoutSpan, value);
            }
        }

        private int uniformHeight = 300;
        /// <summary>
        /// 统一窗口长度
        /// </summary>
        public int UniformHeight
        {
            get => uniformHeight;
            set
            {
                SetProperty(ref uniformHeight, value);
            }
        }
        private int uniformWidth = 533;
        /// <summary>
        /// 统一窗口宽度
        /// </summary>
        public int UniformWidth
        {
            get => uniformWidth;
            set
            {
                SetProperty(ref uniformWidth, value);
            }
        }

        private bool startAllWithNoneSetting = true;
        /// <summary>
        /// 开始全部时包含无保存设置的进程
        /// </summary>
        public bool StartAllWithNoneSetting
        {
            get => startAllWithNoneSetting;
            set
            {
                SetProperty(ref startAllWithNoneSetting, value);
            }
        }

        private int startAllDefaultLoadType = 0;
        /// <summary>
        /// 开始全部无保存配置的进程加载配置方式
        /// 0 依序使用已保存列表
        /// 1 新建默认
        /// </summary>
        public int StartAllDefaultLoadType
        {
            get => startAllDefaultLoadType;
            set
            {
                SetProperty(ref startAllDefaultLoadType, value);
            }
        }

        private int setForegroundWindowMode = 0;
        public int SetForegroundWindowMode
        {
            get => setForegroundWindowMode;
            set
            {
                SetProperty(ref setForegroundWindowMode, value);
            }
        }
    }
}
