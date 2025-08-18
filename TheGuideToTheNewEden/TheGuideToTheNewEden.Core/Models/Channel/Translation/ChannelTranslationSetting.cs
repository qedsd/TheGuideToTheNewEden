using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Channel.Translation
{
    public class ChannelTranslationSetting : ChannelSetting
    {
        private string _autoTranslateFrom = "auto";
        /// <summary>
        /// 自动翻译原文语言类型
        /// </summary>
        public string AutoTranslateFrom { get => _autoTranslateFrom; set => SetProperty(ref _autoTranslateFrom, value); }

        private string _autoTranslateTo = "zh-CHS";
        /// <summary>
        /// 自动翻译译文语言类型
        /// </summary>
        public string AutoTranslateTo { get => _autoTranslateTo; set => SetProperty(ref _autoTranslateTo, value); }

        private bool _skipMyself;
        /// <summary>
        /// 自动翻译跳过自己的发言
        /// </summary>
        public bool SkipMyself { get => _skipMyself; set => SetProperty(ref _skipMyself, value); }

        private string _keyword;
        public string Keyword { get => _keyword; set => SetProperty(ref _keyword, value); }
    }
}
