using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    /// <summary>本地化库 <c>dogmaUnits</c>：属性单位显示名（m / kg / %…），阶段 283。</summary>
    [SugarTable("dogmaUnits")]
    [SugarIndex("index_dogmaUnits_id", nameof(Id), OrderByType.Asc)]
    public class DogmaUnits
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }

        [SugarColumn(IsNullable = true)]
        public string DisplayName { get; set; }

        public DogmaUnits() { }

        public DogmaUnits(DeserializeModels.DogmaUnits data, LanguageEnum language)
        {
            Id = data.Id;
            DisplayName = data.DisplayName?.GetValue(language) ?? data.Name;
        }
    }
}
