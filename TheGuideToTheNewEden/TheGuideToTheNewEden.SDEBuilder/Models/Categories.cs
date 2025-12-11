using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.Models
{
    [SqlSugar.SugarTable("categories")]
    public class Categories : BaseModel
    {
        [JsonProperty("Name")]
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public Languages Names { get; set; }

        [JsonIgnore]
        public string Name { get; set; }

        public bool Published { get; set; }


        public override Dictionary<string, object> GetDict(LanguageEnum language)
        {
            return new Dictionary<string, object>
            {
                { "Id", Id },
                { "Published", Published },
                { "Name", Names.GetValue(language)},
            };
        }
    }
}
