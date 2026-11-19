using SqlSugar;

namespace TheGuideToTheNewEden.Core.DBModels.Static
{
    /// <summary>
    /// 萨沙入侵站点（数据在 <c>Resources/Database/static.db</c> 的 <c>IncursionSite</c> 表）。
    /// <para>
    /// 来源是 EVE University Wiki 的 <c>Category:Incursions sites</c> 渲染页——
    /// 站点难度、舰队需求、敌人构成这些表都是模板生成的，只有渲染后的 HTML 才有真实内容。
    /// </para>
    /// </summary>
    [SugarTable("IncursionSite")]
    public class IncursionSite
    {
        public int Id { get; set; }

        /// <summary>0 集结点 1 先锋 2 突击 3 总部 4 母舰。</summary>
        public int Type { get; set; }

        /// <summary>类型的中文名（集结点 / 先锋站点 / 突击站点 / 总部站点 / 母舰站点）。</summary>
        public string TypeCN { get; set; }

        public string TitleEN { get; set; }

        public string TitleCN { get; set; }

        /// <summary>渲染好的中文 Markdown 正文。</summary>
        public string Content { get; set; }

        public bool IsStub { get; set; }

        public string WikiUpdatedAt { get; set; }
    }
}
