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
        private string switchHotkey = "Ctrl+Tab";
        /// <summary>
        /// 切换运行中的窗口对应的源窗口
        /// </summary>
        public string SwitchHotkey
        {
            get => switchHotkey;
            set => SetProperty(ref switchHotkey, value);
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
    }
}
