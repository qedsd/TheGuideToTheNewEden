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

        private bool _autoTranslateToZhOnly = true;
        /// <summary>
        /// 只翻译"非中文为主"的消息。
        /// 中文玩家的诉求是看懂外文频道，把中文消息也送一遍模型既费钱又没意义（WPF 版新增，WinUI 侧不使用）。
        /// </summary>
        public bool AutoTranslateToZhOnly { get => _autoTranslateToZhOnly; set => SetProperty(ref _autoTranslateToZhOnly, value); }

        private int _minLength = 2;
        /// <summary>
        /// 最短长度（去掉空白后的字符数）：过滤"111"、"+++"这类没有翻译价值的噪声（WPF 版新增）。
        /// </summary>
        public int MinLength { get => _minLength; set => SetProperty(ref _minLength, value); }

        private bool _useContext = true;
        /// <summary>
        /// 是否把同一频道里此前已翻译过的几条一起发给模型当上下文（WPF 版新增，默认开）。
        /// 频道里一句话经常依赖前文（"他也来了"这种），带上上下文译文明显更准；代价是每条请求大一点。
        /// </summary>
        public bool UseContext { get => _useContext; set => SetProperty(ref _useContext, value); }

        private int _contextLimit = 4;
        /// <summary>
        /// 上下文条数上限（<see cref="UseContext"/> 打开时生效；WPF 版新增）。
        /// </summary>
        public int ContextLimit { get => _contextLimit; set => SetProperty(ref _contextLimit, value); }
    }
}
