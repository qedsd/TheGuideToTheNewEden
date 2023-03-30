using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.GamePreviews
{
    public class PreviewSetting : ObservableObject
    {
        private string processeKeywords = "wechat";
        /// <summary>
        /// 游戏进程名关键词
        /// 按,分割
        /// </summary>
        public string ProcessKeywords
        {
            get => processeKeywords;
            set => SetProperty(ref processeKeywords, value);
        }

        private List<PreviewItem> previewItems;
        public List<PreviewItem> PreviewItems
        {
            get => previewItems;
            set => SetProperty(ref previewItems, value);
        }
    }
}
