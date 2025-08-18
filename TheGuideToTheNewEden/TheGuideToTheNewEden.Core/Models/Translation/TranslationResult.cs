using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Translation
{
    public class TranslationResult
    {
        public bool Success { get; set; }
        public string ErrorMsg { get; set; }

        /// <summary>
        /// 结果
        /// </summary>
        public string Result {  get; set; }

        /// <summary>
        /// 原文
        /// </summary>
        public string Query {  get; set; }

        /// <summary>
        /// 原文语言
        /// </summary>
        public string From {  get; set; }

        /// <summary>
        /// 结果语言
        /// </summary>
        public string To { get; set; }
    }
}
