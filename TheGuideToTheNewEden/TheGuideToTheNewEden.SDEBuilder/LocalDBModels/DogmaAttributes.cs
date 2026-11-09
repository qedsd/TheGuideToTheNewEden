using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    /// <summary>
    /// 本地化库 <c>dogmaAttributes</c>：只保留属性的显示名与描述（阶段 283）。
    /// 主库 main.db 固定英文，中文显示名走这里。
    /// </summary>
    [SugarTable("dogmaAttributes")]
    [SugarIndex("index_dogmaAttributes_id", nameof(Id), OrderByType.Asc)]
    public class DogmaAttributes
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }

        [SugarColumn(IsNullable = true)]
        public string DisplayName { get; set; }

        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }

        public DogmaAttributes() { }

        public DogmaAttributes(DeserializeModels.DogmaAttributes data, LanguageEnum language)
        {
            Id = data.Id;
            DisplayName = data.DisplayName?.GetValue(language) ?? data.Name;

            // SDE 的 description 只有英文；本地化版本在 tooltipDescription 里（游戏内悬浮说明）。
            Description = data.TooltipDescription?.GetValue(language)
                          ?? data.Descriptions
                          ?? data.Description;
        }
    }
}
