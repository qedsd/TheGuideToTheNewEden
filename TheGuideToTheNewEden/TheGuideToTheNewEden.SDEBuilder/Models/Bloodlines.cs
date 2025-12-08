using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.Models
{
    [SqlSugar.SugarTable("bloodlines")]
    public class Bloodlines : BaseModel
    {
        [JsonIgnore]
        public string Name { get; set; }
        public int RaceID { get; set; }
        public string Description { get; set; }
        public int CorporationID { get; set; }
        public int Perception { get; set; }
        public int Willpower { get; set; }
        public int Charisma { get; set; }
        public int Memory { get; set; }
        public int Intelligence { get; set; }
        public int IconID { get; set; }

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
                { "Name", Names.GetValue(language)},
                { "RaceID",RaceID},
                { "Description", Descriptions.GetValue(language)},
                { "CorporationID",CorporationID},
                { "Perception",Perception},
                { "Willpower",Willpower},
                { "Charisma",Charisma},
                { "Memory",Memory},
                { "Intelligence",Intelligence},
                { "IconID",IconID},
            };
        }
    }
}
