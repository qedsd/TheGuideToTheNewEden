﻿using Newtonsoft.Json.Linq;
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
        public IdName(int id, string name, CategoryEnum category)
        {
            Id = id;
            Name = name;
            Category = (int)category;
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
