using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Models
{
    /// <summary>
    /// 兼容数据库、有道翻译两种结果
    /// </summary>
    public class TranslationItem
    {
        /// <summary>
        /// 若是数据库物品，则存在ID
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// 原文
        /// </summary>
        public string Query { get; set; }

        /// <summary>
        /// 译文
        /// </summary>
        public string Translation { get; set; }

        /// <summary>
        /// 原文语言
        /// </summary>
        public string From {  get; set; }

        /// <summary>
        /// 译文语言
        /// </summary>
        public string To { get; set; }

        /// <summary>
        /// 原文描述
        /// 数据库物品如InvType存在描述
        /// </summary>
        public string QueryDescription { get; set; }

        /// <summary>
        /// 译文描述
        /// 数据库物品如InvType存在描述
        /// </summary>
        public string TranslationDescription { get; set; }

        /// <summary>
        /// 翻译分两种：数据库与翻译API
        /// </summary>
        public bool IsFromDataBase {  get; set; }

        /// <summary>
        /// 查询数据库物品的类型
        /// </summary>
        public DataBaseItemType DataBaseItemType { get; set; }
    }
}
