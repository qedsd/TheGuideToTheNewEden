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
    }
}
