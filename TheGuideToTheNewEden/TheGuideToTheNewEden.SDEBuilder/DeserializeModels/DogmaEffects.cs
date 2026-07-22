using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class DogmaEffects : BaseModel
    {
        [JsonProperty("description")]
        public Languages Description { get; set; }

        [JsonProperty("disallowAutoRepeat")]
        public bool DisallowAutoRepeat { get; set; }

        [JsonProperty("dischargeAttributeID")]
        public int DischargeAttributeID { get; set; }

        [JsonProperty("displayName")]
        public Languages DisplayName { get; set; }

        [JsonProperty("distribution")]
        public int Distribution { get; set; }

        [JsonProperty("durationAttributeID")]
        public int DurationAttributeID { get; set; }

        [JsonProperty("effectCategoryID")]
        public int EffectCategoryID { get; set; }

        [JsonProperty("electronicChance")]
        public bool ElectronicChance { get; set; }

        [JsonProperty("falloffAttributeID")]
        public int FalloffAttributeID { get; set; }

        [JsonProperty("guid")]
        public string Guid { get; set; }

        [JsonProperty("iconID")]
        public int IconID { get; set; }

        [JsonProperty("isAssistance")]
        public bool IsAssistance { get; set; }

        [JsonProperty("isOffensive")]
        public bool IsOffensive { get; set; }

        [JsonProperty("isWarpSafe")]
        public bool IsWarpSafe { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("postExpression")]
        public int PostExpression { get; set; }

        [JsonProperty("preExpression")]
        public int PreExpression { get; set; }

        [JsonProperty("propulsionChance")]
        public bool PropulsionChance { get; set; }

        [JsonProperty("published")]
        public bool Published { get; set; }

        [JsonProperty("rangeAttributeID")]
        public int RangeAttributeID { get; set; }

        [JsonProperty("rangeChance")]
        public bool RangeChance { get; set; }

        [JsonProperty("trackingSpeedAttributeID")]
        public int TrackingSpeedAttributeID { get; set; }

        [JsonProperty("modifierInfo")]
        public List<ModifierInfoItem> ModifierInfo { get; set; }
    }

    public class ModifierInfoItem
    {
        [JsonProperty("domain")]
        public string Domain { get; set; }

        [JsonProperty("func")]
        public string Func { get; set; }

        [JsonProperty("modifiedAttributeID")]
        public int ModifiedAttributeID { get; set; }

        [JsonProperty("modifyingAttributeID")]
        public int ModifyingAttributeID { get; set; }

        [JsonProperty("operation")]
        public int Operation { get; set; }
    }
}
