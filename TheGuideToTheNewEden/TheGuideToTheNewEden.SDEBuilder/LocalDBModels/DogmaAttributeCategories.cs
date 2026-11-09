using SqlSugar;
using System.Collections.Generic;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    /// <summary>
    /// 本地化库 <c>dogmaAttributeCategories</c>：属性分组名（阶段 283）。
    /// SDE 的分组名只有英文，这里带一份人工中文表；其它语言回退英文。
    /// </summary>
    [SugarTable("dogmaAttributeCategories")]
    [SugarIndex("index_dogmaAttributeCategories_id", nameof(Id), OrderByType.Asc)]
    public class DogmaAttributeCategories
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }

        [SugarColumn(IsNullable = true)]
        public string Name { get; set; }

        public DogmaAttributeCategories() { }

        public DogmaAttributeCategories(DeserializeModels.DogmaAttributeCategories data, LanguageEnum language)
        {
            Id = data.Id;
            Name = language == LanguageEnum.Zh && ChineseNames.TryGetValue(data.Id, out var zh)
                ? zh
                : data.Name;
        }

        /// <summary>SDE 里没有本地化的分组名，这里补齐中文。</summary>
        public static readonly Dictionary<int, string> ChineseNames = new Dictionary<int, string>
        {
            { 1, "装配" },
            { 2, "护盾" },
            { 3, "装甲" },
            { 4, "结构" },
            { 5, "电容" },
            { 6, "锁定" },
            { 7, "杂项" },
            { 8, "技能要求" },
            { 9, "未分类" },
            { 10, "无人机" },
            { 12, "AI" },
            { 17, "速度与移动" },
            { 19, "掉落" },
            { 20, "远程协助" },
            { 21, "电子战 - 目标标记" },
            { 22, "电子战 - 能量中和" },
            { 23, "电子战 - 远程ECM" },
            { 24, "电子战 - 感应抑阻" },
            { 25, "电子战 - 目标干扰" },
            { 26, "电子战 - 跟踪扰乱" },
            { 27, "电子战 - 跃迁扰断" },
            { 28, "电子战 - 停滞缠绕" },
            { 29, "炮台" },
            { 30, "导弹" },
            { 31, "图形" },
            { 32, "实体奖励" },
            { 33, "实体额外属性" },
            { 34, "战斗机能力" },
            { 36, "电子战 - 抗性" },
            { 37, "加成" },
            { 38, "战斗机属性" },
            { 39, "超级武器" },
            { 40, "机库与货舱" },
            { 41, "死亡相关" },
            { 42, "行为属性" },
            { 51, "采矿" },
            { 52, "过热" },
        };
    }
}
