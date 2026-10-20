using Newtonsoft.Json;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("cloneGrades")]
    [SugarIndex("index_cloneGrades_id", nameof(Id), OrderByType.Asc)]
    public class CloneGrades
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Skills { get; set; }
        public CloneGrades() { }
        public CloneGrades(DeserializeModels.BaseModel model)
        {
            var data = model as DeserializeModels.CloneGrades;
            Id = data.Id;
            Name = data.Name ?? data.Names;
            Skills = data.Skills == null ? null : JsonConvert.SerializeObject(data.Skills);
        }
    }
}
