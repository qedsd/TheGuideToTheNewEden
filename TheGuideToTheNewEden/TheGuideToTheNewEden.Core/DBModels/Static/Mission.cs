using SqlSugar;
using System;

namespace TheGuideToTheNewEden.Core.DBModels.Static
{
    /// <summary>
    /// 任务（EVE University Wiki 的 Missions 体系）。
    /// <para>
    /// 老表只有 `Id / Level / Title_Zh / Title_En / Content`，既没有分类也没法按势力/奖励筛，
    /// 2026-11 按「任务攻略」重做：字段对齐 wiki 的 `{{Missiondetails}}` 模板
    /// （Level / Type / Objective / Faction / EWAR / ShipSizeLimit / ShipSuggestion / Rewards / StandingLoss / Extra）。
    /// </para>
    /// </summary>
    [SugarTable("Mission")]
    public class Mission
    {
        /// <summary>主键（构建期分配，稳定不变）。</summary>
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }

        /// <summary>分类，见 <see cref="Enums.MissionCategory"/>。</summary>
        public int Category { get; set; }

        /// <summary>任务等级 1–5；0 表示 wiki 没标（章节枢纽、总览页）。</summary>
        public int Level { get; set; }

        /// <summary>wiki 的 Type 原值，如 `Encounter` / `Courier` / `Mining` / `Storyline(Encounter)`。</summary>
        public string MissionType { get; set; }

        /// <summary>敌对势力索引（与战斗站点同一套：0 无人机 1 天蛇 2 血袭者 3 萨沙 4 天使 5 古斯塔斯，-1 未知/帝国）。</summary>
        public int Faction { get; set; }

        /// <summary>任务英文名（= wiki 页名，多等级页会带 `(Level N)` 后缀）。</summary>
        public string TitleEN { get; set; }

        /// <summary>任务中文名。</summary>
        public string TitleCN { get; set; }

        /// <summary>任务目标，如「击毁所有敌舰」。</summary>
        public string Objective { get; set; }

        /// <summary>电子战/反跳点（wiki 的 WebPoint 与 EWAR 合并后的展示文案）。</summary>
        public string Ewar { get; set; }

        /// <summary>船型限制（如 `Frigate`）。</summary>
        public string ShipLimit { get; set; }

        /// <summary>推荐船型。</summary>
        public string ShipSuggestion { get; set; }

        /// <summary>奖励（ISK/LP/物品）。</summary>
        public string Rewards { get; set; }

        /// <summary>声望损失说明。</summary>
        public string StandingLoss { get; set; }

        /// <summary>备注（wiki 的 Extra）。</summary>
        public string Extra { get; set; }

        /// <summary>地点（从正文抽的代理人/星系，抽不到就空）。</summary>
        public string Location { get; set; }

        /// <summary>所属史诗任务线（只有 Epic 分类才有值）。</summary>
        public string Arc { get; set; }

        /// <summary>同页多等级时的区分（如 `L1` / `L3`），单版本为空。</summary>
        public string Variant { get; set; }

        /// <summary>wiki 页名（用于回溯原始页面）。</summary>
        public string WikiTitle { get; set; }

        /// <summary>正文 Markdown（中文）。</summary>
        public string WikiContent { get; set; }

        /// <summary>内容稀薄（只有 infobox、没有正文）。</summary>
        public int IsStub { get; set; }

        /// <summary>数据同步日期。</summary>
        public string WikiUpdatedAt { get; set; }
    }
}
