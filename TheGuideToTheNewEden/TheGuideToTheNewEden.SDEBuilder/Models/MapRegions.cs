using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.Models
{
    [SqlSugar.SugarTable("mapRegions")]
    public class MapRegions : BaseModel
    {
        [JsonIgnore]
        public string Name { get; set; }

        public string Description { get; set; }

        public int NebulaID { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public int WormholeClassID {  get; set; }

        [SqlSugar.SugarColumn(IsIgnore = true)]
        public List<int> ConstellationIDs { get; set; }

        [SqlSugar.SugarColumn(IsIgnore = true)]
        public Position Position { get; set; }

        [JsonProperty("Description")]
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public Languages Descriptions { get; set; }

        
        [JsonProperty("Name")]
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public Languages Names { get; set; }
        public override Dictionary<string, object> GetDict(LanguageEnum language)
        {
            return new Dictionary<string, object>
            {
                { "Id", Id },
                { "Name", Names.GetValue(language) },
                { "X", X },
                { "Y", Y },
                { "Z", Z },
                { "Description", Descriptions.GetValue(language)},
                { "NebulaID", NebulaID },
                { "WormholeClassID", WormholeClassID },
            };
        }
    }
}
