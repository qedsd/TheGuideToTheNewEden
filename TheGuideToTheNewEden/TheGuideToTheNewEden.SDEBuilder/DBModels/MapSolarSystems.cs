using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SqlSugar.SugarTable("mapSolarSystems")]
    public class MapSolarSystems
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int RegionID { get; set; }
        public int ConstellationID { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public double Luminosity { get; set; }
        public bool Border { get; set; }
        public bool Corridor { get; set; }
        public double SecurityStatus { get; set; }

        [SqlSugar.SugarColumn(IsNullable = true)]
        public string SecurityClass { get; set; }
        public double Radius { get; set; }
        public int StarID { get; set; }
        public int WormholeClassID { get; set; }
        public MapSolarSystems() { }
        public MapSolarSystems(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var mapSolarSystems = model as DeserializeModels.MapSolarSystems;
            Id = mapSolarSystems.Id;
            Name = mapSolarSystems.Names.GetValue(language);
            RegionID = mapSolarSystems.RegionID;
            ConstellationID = mapSolarSystems.ConstellationID;
            X = mapSolarSystems.Position.X;
            Y = mapSolarSystems.Position.Y;
            Z = mapSolarSystems.Position.Z;
            if(mapSolarSystems.Position2D != null)
            {
                X2 = mapSolarSystems.Position2D.X;
                Y2 = mapSolarSystems.Position2D.Y;
            }
            Luminosity = mapSolarSystems.Luminosity;
            Border = mapSolarSystems.Border;
            Corridor = mapSolarSystems.Corridor;
            SecurityStatus = mapSolarSystems.SecurityStatus;
            SecurityClass = mapSolarSystems.SecurityClass;
            Radius = mapSolarSystems.Radius;
            StarID = mapSolarSystems.StarID;
            WormholeClassID = mapSolarSystems.WormholeClassID;
        }
    }
}
