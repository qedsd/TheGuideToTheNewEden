using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("stationServices")]
    [SugarIndex("index_stationServices_id", nameof(Id), OrderByType.Asc)]
    public class StationServices
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string ServiceName { get; set; }
        public StationServices() { }
        public StationServices(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.StationServices;
            Id = data.Id;
            ServiceName = data.ServiceName?.GetValue(language);
        }
    }
}
