using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.Models
{
    [SqlSugar.SugarTable("mapSolarSystems")]
    internal class MapSolarSystems : BaseModel
    {
        [JsonIgnore]
        public string Name { get; set; }
        public int RegionID { get; set; }
        public int ConstellationID { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Luminosity { get; set; }
        public bool Border { get; set; }
        public bool Corridor { get; set; }
        public double SecurityStatus { get; set; }
        public string SecurityClass { get; set; }
        public double Radius { get; set; }
        public int StarID { get; set; }
        public int WormholeClassID { get; set; }

        [JsonProperty("Name")]
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public Languages Names { get; set; }

        [SqlSugar.SugarColumn(IsIgnore = true)]
        public List<int> PlanetIDs { get; set; }

        [SqlSugar.SugarColumn(IsIgnore = true)]
        public Position Position { get; set; }

        [SqlSugar.SugarColumn(IsIgnore = true)]
        public Position Position2D { get; set; }

        [SqlSugar.SugarColumn(IsIgnore = true)]
        public List<int> StargateIDs { get; set; }

        //public override List<DBTable> GetDBTables(LanguageEnum language)
        //{
        //    List<DBTable> tables = new List<DBTable>();
        //    tables.Add(new DBTable()
        //    {
        //        TableModel = typeof(MapSolarSystems),
        //        TableValues = new Dictionary<string, object>
        //        {
        //            { "Id", Id },
        //            { "Name", Names.GetValue(language)},
        //            { "RegionID", RegionID },
        //            { "ConstellationID", ConstellationID },
        //            { "X", X },
        //            { "Y", Y },
        //            { "Z", Z },
        //            { "Luminosity", Luminosity },
        //            { "Border", Border },
        //            { "Corridor", Corridor },
        //            { "SecurityStatus", SecurityStatus },
        //            { "SecurityClass", SecurityClass },
        //            { "Radius", Radius },
        //            { "StarID", StarID },
        //            { "WormholeClassID", WormholeClassID },
        //        }
        //    });
        //}

        public override Dictionary<string, object> GetDict(LanguageEnum language)
        {
            throw new NotImplementedException();
        }
    }
}
