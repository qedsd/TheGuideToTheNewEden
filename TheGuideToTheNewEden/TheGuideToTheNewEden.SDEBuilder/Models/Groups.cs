using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using static Azure.Core.HttpHeader;

namespace TheGuideToTheNewEden.SDEBuilder.Models
{
    [SqlSugar.SugarTable("groups")]
    public class Groups : BaseModel
    {
        public int CategoryID { get; set; }
        [JsonIgnore]
        public string Name { get; set; }
        public int IconID { get; set; }
        public bool UseBasePrice { get; set; }
        public bool Anchored { get; set; }
        public bool Anchorable { get; set; }
        public bool FittableNonSingleton { get; set; }
        public bool Published { get; set; }

        [JsonProperty("Name")]
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public Languages Names { get; set; }
        
        public override Dictionary<string, object> GetDict(LanguageEnum language)
        {
            return new Dictionary<string, object>
            {
                { "Id", Id },
                { "CategoryID", CategoryID },
                { "Name", Names.GetValue(language)},
                { "IconID", IconID },
                { "UseBasePrice", UseBasePrice },
                { "Anchored", Anchored },
                { "Anchorable", Anchorable },
                { "FittableNonSingleton", FittableNonSingleton },
                { "Published", Published },
            };
        }
    }
}
