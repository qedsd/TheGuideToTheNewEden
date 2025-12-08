using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class DbuffCollections : BaseModel
    {
        [JsonProperty("aggregateMode")]
        public string AggregateMode { get; set; }

        [JsonProperty("developerDescription")]
        public string DeveloperDescription { get; set; }

        [JsonProperty("itemModifiers")]
        public List<ItemModifier> ItemModifiers { get; set; }

        [JsonProperty("locationGroupModifiers")]
        public List<LocationGroupModifier> LocationGroupModifiers { get; set; }

        [JsonProperty("locationModifiers")]
        public List<LocationModifier> LocationModifiers { get; set; }

        [JsonProperty("locationRequiredSkillModifiers")]
        public List<LocationRequiredSkillModifier> LocationRequiredSkillModifiers { get; set; }

        [JsonProperty("operationName")]
        public string OperationName { get; set; }

        [JsonProperty("showOutputValueInUI")]
        public string ShowOutputValueInUI { get; set; }
    }

    public class ItemModifier
    {
        [JsonProperty("dogmaAttributeID")]
        public int DogmaAttributeID { get; set; }
    }

    public class LocationGroupModifier
    {
        [JsonProperty("dogmaAttributeID")]
        public int DogmaAttributeID { get; set; }

        [JsonProperty("groupID")]
        public int GroupID { get; set; }
    }

    public class LocationModifier
    {
        [JsonProperty("dogmaAttributeID")]
        public int DogmaAttributeID { get; set; }
    }

    public class LocationRequiredSkillModifier
    {
        [JsonProperty("dogmaAttributeID")]
        public int DogmaAttributeID { get; set; }

        [JsonProperty("skillID")]
        public int SkillID { get; set; }
    }
}
