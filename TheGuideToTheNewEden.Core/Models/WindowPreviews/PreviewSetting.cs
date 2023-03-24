using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.WindowPreviews
{
    public class PreviewSetting : ObservableObject
    {
        private string titleKeywords = "EVE,星战前夜";
        /// <summary>
        /// 游戏窗口标题关键词
        /// 按,分割
        /// </summary>
        public string TitleKeywords
        {
            get => titleKeywords;
            set => SetProperty(ref titleKeywords, value);
        }

        private List<PreviewItem> previewItems;
        public List<PreviewItem> PreviewItems
        {
            get => previewItems;
            set => SetProperty(ref previewItems, value);
        }
    }
}
