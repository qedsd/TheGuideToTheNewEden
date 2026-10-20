using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("mapSecondarySuns")]
    [SugarIndex("index_mapSecondarySuns_id", nameof(Id), OrderByType.Asc)]
    [SugarIndex("index_mapSecondarySuns_solarSystemID", nameof(SolarSystemID), OrderByType.Asc)]
    public class MapSecondarySuns
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public int SolarSystemID { get; set; }
        public int TypeID { get; set; }
        public int EffectBeaconTypeID { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public MapSecondarySuns() { }
        public MapSecondarySuns(DeserializeModels.BaseModel model)
        {
            var data = model as DeserializeModels.MapSecondarySuns;
            Id = data.Id;
            SolarSystemID = data.SolarSystemID;
            TypeID = data.TypeID;
            EffectBeaconTypeID = data.EffectBeaconTypeID;
            if (data.Position != null)
            {
                X = data.Position.X;
                Y = data.Position.Y;
                Z = data.Position.Z;
            }
        }
    }
}
