using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Models.Translation
{
    /// <summary>
    /// 术语表条目：主数据库（英文）与本地化数据库（中文）里同名表按 <c>Id</c> 配对的结果。
    /// 供 AI 翻译做"术语约束"（注入提示词）与结果后校验使用。
    /// </summary>
    public class GlossaryEntry
    {
        /// <summary>对应的 SDE ID（物品 TypeID / 星系 SolarSystemID / 星域 RegionID / 空间站 StationID）。</summary>
        public int Id { get; set; }

        /// <summary>名词类别。</summary>
        public DataBaseItemType Kind { get; set; }

        /// <summary>英文名（主库）。</summary>
        public string English { get; set; }

        /// <summary>中文名（本地化库）。</summary>
        public string Chinese { get; set; }

        /// <summary>
        /// 是否为"市场物品"（<c>types.MarketGroupID</c> 非空）。
        /// 代理人舰船之类的非市场物品名字长且冷门，命中排序时排在市场物品之后。
        /// </summary>
        public bool IsMarketItem { get; set; }

        public override string ToString() => $"{English} = {Chinese}";
    }
}
