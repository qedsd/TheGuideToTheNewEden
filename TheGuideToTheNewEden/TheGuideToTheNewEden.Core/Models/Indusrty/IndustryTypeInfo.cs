using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Indusrty
{
    /// <summary>
    /// 工业计算用的物品摘要（types + groups 联查结果）。
    /// 名称来自主库（英文原名）；需要本地化时由调用方再走 LocalDbService。
    /// </summary>
    public class IndustryTypeInfo
    {
        public int TypeID { get; set; }
        public string TypeName { get; set; }
        public int GroupID { get; set; }
        public string GroupName { get; set; }
        public int CategoryID { get; set; }
        public double Volume { get; set; }
        public double PackagedVolume { get; set; }
    }
}
