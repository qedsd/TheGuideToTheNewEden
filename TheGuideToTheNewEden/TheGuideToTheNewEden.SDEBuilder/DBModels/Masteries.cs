using Newtonsoft.Json;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("masteries")]
    [SugarIndex("index_masteries_typeID", nameof(TypeID), OrderByType.Asc)]
    public class Masteries
    {
        public int TypeID { get; set; }
        public int Level { get; set; }
        [SugarColumn(IsNullable = true)]
        public string SkillTypeIDs { get; set; }
        public Masteries() { }
        public Masteries(int typeId, DeserializeModels.MasteryLevel level)
        {
            TypeID = typeId;
            Level = level.Level;
            SkillTypeIDs = level.SkillTypeIDs == null ? null : JsonConvert.SerializeObject(level.SkillTypeIDs);
        }
    }
}
