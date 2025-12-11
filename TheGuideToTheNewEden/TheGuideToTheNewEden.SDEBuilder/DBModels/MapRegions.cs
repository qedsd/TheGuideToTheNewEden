using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SqlSugar.SugarTable("mapRegions")]
    public class MapRegions
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public int NebulaID { get; set; }
        public int WormholeClassID { get; set; }
        public MapRegions() { }
        public MapRegions(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var mapRegions = model as DeserializeModels.MapRegions;
            Id = mapRegions.Id;
            Name = mapRegions.Names.GetValue(language);
            X = mapRegions.Position.X;
            Y = mapRegions.Position.Y;
            Z = mapRegions.Position.Z;
            NebulaID = mapRegions.NebulaID;
            WormholeClassID = mapRegions.WormholeClassID;
        }
    }
}
