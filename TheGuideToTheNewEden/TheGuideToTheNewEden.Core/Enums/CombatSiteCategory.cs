namespace TheGuideToTheNewEden.Core.Enums
{
    /// <summary>
    /// 战斗站点（Combat Site）的分类。
    /// <para>
    /// 与旧的 <c>DED</c> 表相比，这一列把"死亡远征"从"一张表一锅端"拆成了
    /// EVE University Wiki 用的那套分类（异常 / 无评级复合体 / DED 评级复合体 / 远征 / 化学实验室 / 遗迹 / 三神裔 / 特殊）。
    /// 旧表只能靠 <c>Level</c> 猜，补不进 126 条战斗异常与 34 条化学实验室。
    /// </para>
    /// </summary>
    public enum CombatSiteCategory
    {
        /// <summary>DED 评级复合体（1/10 – 10/10，扫描或由异常升级而来）。</summary>
        DedRated = 0,

        /// <summary>无评级复合体（含 <c>&lt;Faction&gt; Fleet Staging Point</c> 这类"习惯叫 9/10"）。</summary>
        Unrated = 1,

        /// <summary>远征（由无评级复合体触发，只能从星际代理处进入）。</summary>
        Expedition = 2,

        /// <summary>战斗异常（进星系自动出现，不需要扫描）。</summary>
        Anomaly = 3,

        /// <summary>化学实验室（探测窗口算 gas site，实为战斗站点）。</summary>
        ChemicalLab = 4,

        /// <summary>战斗遗迹（极稀有，只在特定 00 区域）。</summary>
        RelicSite = 5,

        /// <summary>三神裔入侵站点（波赫文）。</summary>
        Triglavian = 6,

        /// <summary>其它特殊站点（神庙复合体 / ISHAEKA / 被围困的研究设施等）。</summary>
        Special = 7,

        /// <summary>
        /// 旗舰远征（capital escalation）：<c>&lt;Faction&gt; Capital Staging</c> / <c>Shielded Starbase</c> 这一类。
        /// <para>
        /// 单独一档而不是并进 <see cref="Expedition"/>，因为玩家就是这么叫的（"旗舰远征"），
        /// 而且它们**只在 00 的高等级异常升级时才出**、建议用旗舰打——混在 76 条普通远征里根本找不出来。
        /// 数据来自游戏 SDE 的 <c>dungeons.jsonl</c>（wiki 的 Combat site 页没有把它们列成条目），
        /// 中英文名与官方描述都是现成的，不需要机翻。
        /// </para>
        /// </summary>
        CapitalExpedition = 8,
    }
}
