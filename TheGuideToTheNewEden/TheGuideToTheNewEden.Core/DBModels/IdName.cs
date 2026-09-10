using Newtonsoft.Json.Linq;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    public class IdName
    {
        public IdName() { }
        public IdName(long id, string name, CategoryEnum category)
        {
            Id = (int)id;
            Name = name;
            Category = (int)category;
        }
        public IdName(long id, string name, int category)
        {
            Id = (int)id;
            Name = name;
            Category = category;
        }
        public IdName(int id, string name, CategoryEnum category)
        {
            Id = id;
            Name = name;
            Category = (int)category;
        }
        public IdName(int id, string name, string category)
        {
            Id = id;
            Name = name;
            Category = (int)ParseCategory(category);
        }

        /// <summary>
        /// ESI 的 /universe/names 返回小写类别（如 character / station / inventory_type / solar_system），
        /// 与枚举成员名（PascalCase）并不一致，因此按 [EnumMember] 的取值显式映射。
        /// </summary>
        private static CategoryEnum ParseCategory(string category)
        {
            switch ((category ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "alliance": return CategoryEnum.Alliance;
                case "character": return CategoryEnum.Character;
                case "constellation": return CategoryEnum.Constellation;
                case "corporation": return CategoryEnum.Corporation;
                case "inventory_type": return CategoryEnum.InventoryType;
                case "region": return CategoryEnum.Region;
                case "solar_system": return CategoryEnum.SolarSystem;
                case "station": return CategoryEnum.Station;
                case "faction": return CategoryEnum.Faction;
                case "structure": return CategoryEnum.Structure;
                case "group": return CategoryEnum.Group;
                default:
                    return Enum.TryParse<CategoryEnum>(category, ignoreCase: true, out var parsed)
                        ? parsed
                        : CategoryEnum.InventoryType;
            }
        }
        public IdName(InvType type)
        {
            Id = type.TypeID;
            Name = type.TypeName;
            Category = (int)CategoryEnum.InventoryType;
        }
        [SugarColumn(IsPrimaryKey = true)]
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
            Structure,
            [EnumMember(Value = "group")]
            Group
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
