using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("mercenaryTacticalOperations")]
    [SugarIndex("index_mercenaryTacticalOperations_id", nameof(Id), OrderByType.Asc)]
    public class MercenaryTacticalOperations
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        public int DungeonID { get; set; }
        public int AnarchyImpact { get; set; }
        public int DevelopmentImpact { get; set; }
        public int InfomorphBonus { get; set; }
        public MercenaryTacticalOperations() { }
        public MercenaryTacticalOperations(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.MercenaryTacticalOperations;
            Id = data.Id;
            Name = data.Names?.GetValue(language);
            Description = data.Descriptions?.GetValue(language);
            DungeonID = data.DungeonID;
            AnarchyImpact = data.AnarchyImpact;
            DevelopmentImpact = data.DevelopmentImpact;
            InfomorphBonus = data.InfomorphBonus;
        }
    }
}
