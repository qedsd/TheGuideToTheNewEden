using SqlSugar;
using System;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.DBModels
{
    /// <summary>
    /// 战斗站点（死亡远征 / 战斗异常 / 化学实验室 / 远征……）的完整条目。
    /// <para>
    /// 数据来源两条腿：
    /// ① <see cref="CommunityContent"/> —— 本站整理的中文实战攻略（原 <see cref="DED"/> 表的 <c>Content</c> 迁移而来）；
    /// ② <see cref="WikiContent"/> —— 由 EVE University Wiki（CC BY-SA 4.0）抓取、解析并翻译成中文的结构化正文
    ///    （每层 NPC 波次表、掉落、打法要点）。
    /// 两者**分开存放**：wiki 内容可整段重刷，社区攻略一个字都不动。
    /// </para>
    /// <para>
    /// 关于 <see cref="Rating"/> = 9 的 5 条 <c>&lt;Faction&gt; Fleet Staging Point</c>：
    /// 它们在 CONCORD 体系里其实**没有评级**（wiki 明写没有 9/10），但玩家习惯按 9/10 称它们，
    /// 因此这里刻意保留 <c>Rating = 9</c> 并归入 <see cref="CombatSiteCategory.Unrated"/>。
    /// 这是**有意为之**，不是数据错误。
    /// </para>
    /// </summary>
    [SugarTable("CombatSite")]
    public class CombatSite
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }

        /// <summary>分类，见 <see cref="CombatSiteCategory"/>。</summary>
        public int Category { get; set; }

        /// <summary>海盗势力：0 无人机 / 1 天蛇 / 2 血袭者 / 3 萨沙 / 4 天使 / 5 古斯塔斯；-1 表示不适用。</summary>
        public int Faction { get; set; }

        /// <summary>
        /// DED 评级 1–10（表示 <c>n/10</c>）；0 表示没有评级。
        /// 舰队集结点（Fleet Staging Point）按玩家习惯记 **9**。
        /// </summary>
        public int Rating { get; set; }

        /// <summary>战斗异常的难度档 1–10；非异常为 0。</summary>
        public int Tier { get; set; }

        /// <summary>战斗异常的档内等级 1–4（只有 T1/T4/T6/T8 分 4 级、T10 分 2 级）；其它为 0。</summary>
        public int Level { get; set; }

        /// <summary>出现空间位掩码：1 = 高安、2 = 低安、4 = 00。</summary>
        public int Security { get; set; }

        /// <summary>最大可进船型（中文），例如"战列 / T3 巡洋"；未知为空。</summary>
        public string ShipLimit { get; set; }

        public string TitleCN { get; set; }

        public string TitleEN { get; set; }

        /// <summary>可升级/可触发的目标（多选用中文顿号分隔），例如"古斯塔斯侦察哨站 4/10"。</summary>
        public string Escalation { get; set; }

        /// <summary>
        /// <see cref="Escalation"/> 对应的站点 <see cref="Id"/>；0 表示没有明确目标
        /// （wiki 上只标了"8/10""?"这种没链接的档位）。
        /// <para>
        /// 单独存 Id 而不是运行时去解析中文名：中文名会随 SDE 纠名变化
        /// （`血袭者精神药品仓库` → `血袭者精神药物仓库`），按名字反查会悄悄失效。
        /// </para>
        /// </summary>
        public int EscalationId { get; set; }

        /// <summary>远征的触发来源（无评级复合体名）；其它类别为空。</summary>
        public string Source { get; set; }

        /// <summary>出现位置说明（中文），例如"高安 / 低安"或"海玛塔尔 · 摩登赫斯"。</summary>
        public string Location { get; set; }

        /// <summary>对应的 EVE University Wiki 页名（增量同步与"看原文"用）。</summary>
        public string WikiTitle { get; set; }

        /// <summary>本站社区中文攻略（Markdown）。可为空。</summary>
        public string CommunityContent { get; set; }

        /// <summary>由 wiki 抓取并翻译成中文的结构化正文（Markdown）。可为空。</summary>
        public string WikiContent { get; set; }

        /// <summary>正文过简 / wiki 本身是 stub 的标记，界面上会给出"资料不完整"提示。</summary>
        public int IsStub { get; set; }

        /// <summary>社区攻略的最后人工更新时间（ISO 8601）。</summary>
        public string CommunityUpdatedAt { get; set; }

        /// <summary>wiki 数据的最后同步时间（ISO 8601）。</summary>
        public string WikiUpdatedAt { get; set; }
    }
}
