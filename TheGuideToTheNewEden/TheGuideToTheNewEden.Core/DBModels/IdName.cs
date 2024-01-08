using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    public class IdName
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Category { get; set; }
        public CategoryEnum GetCategory()
        {
            return (CategoryEnum)Category;
        }
        public enum CategoryEnum
        {
            [EnumMember(Value = "alliance")]
            Alliance,
            [EnumMember(Value = "character")]
            Character,
            [EnumMember(Value = "constellation")]
            Constellation,
            [EnumMember(Value = "corporation")]
            Corporation,
            [EnumMember(Value = "inventory_type")]
            InventoryType,
            [EnumMember(Value = "region")]
            Region,
            [EnumMember(Value = "solar_system")]
            SolarSystem,
            [EnumMember(Value = "station")]
            Station,
            [EnumMember(Value = "faction")]
            Faction,
            [EnumMember(Value = "structure")]
            Structure
        }
    }
}
