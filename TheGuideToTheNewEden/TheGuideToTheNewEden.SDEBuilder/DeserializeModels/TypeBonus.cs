using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class TypeBonusEntry
    {
        [JsonProperty("bonus")]
        public double Bonus { get; set; }
        [JsonProperty("bonusText")]
        public Languages BonusText { get; set; }
        [JsonProperty("importance")]
        public int Importance { get; set; }
        [JsonProperty("unitID")]
        public int UnitID { get; set; }
    }

    public class TypeBonusTypeItem
    {
        [JsonProperty("_key")]
        public int TypeID { get; set; }
        [JsonProperty("_value")]
        public List<TypeBonusEntry> Bonuses { get; set; }
    }

    public class TypeBonus : BaseModel
    {
        [JsonProperty("roleBonuses")]
        public List<TypeBonusEntry> RoleBonuses { get; set; }

        [JsonProperty("types")]
        public List<TypeBonusTypeItem> Types { get; set; }
    }
}
