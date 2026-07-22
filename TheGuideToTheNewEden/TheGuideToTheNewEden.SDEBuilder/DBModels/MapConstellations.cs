using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("mapConstellations")]
    [SugarIndex("index_mapConstellations_id", nameof(Id), OrderByType.Asc)]
    [SugarIndex("index_mapConstellations_regionID", nameof(RegionID), OrderByType.Asc)]
    public class MapConstellations
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        public int RegionID { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        [SugarColumn(IsNullable = true)]
        public int? FactionID { get; set; }
        [SugarColumn(IsNullable = true)]
        public int? WormholeClassID { get; set; }
        public MapConstellations() { }
        public MapConstellations(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.MapConstellations;
            Id = data.Id;
            Name = data.Names.GetValue(language);
            RegionID = data.RegionID;
            if (data.Position != null)
            {
                X = data.Position.X;
                Y = data.Position.Y;
                Z = data.Position.Z;
            }
            FactionID = data.FactionID;
            WormholeClassID = data.WormholeClassID;
        }
    }
}
