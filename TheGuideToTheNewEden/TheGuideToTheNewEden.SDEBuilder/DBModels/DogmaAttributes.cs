using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("dogmaAttributes")]
    [SugarIndex("index_dogmaAttributes_id", nameof(Id), OrderByType.Asc)]
    public class DogmaAttributes
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public int AttributeCategoryID { get; set; }
        public int DataType { get; set; }
        public double DefaultValue { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        [SugarColumn(IsNullable = true)]
        public string DisplayName { get; set; }
        public int IconID { get; set; }
        public bool Published { get; set; }
        public int UnitID { get; set; }
        public bool Stackable { get; set; }
        public bool HighIsGood { get; set; }
        public DogmaAttributes() { }
        public DogmaAttributes(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.DogmaAttributes;
            Id = data.Id;
            AttributeCategoryID = data.AttributeCategoryID;
            DataType = data.DataType;
            DefaultValue = data.DefaultValue;
            Name = data.Name;
            Description = data.Descriptions ?? data.Description;
            DisplayName = data.DisplayName?.GetValue(language);
            IconID = data.IconID;
            Published = data.Published == "True" || data.Published == "true" || data.Published == "1";
            UnitID = data.UnitID;
            Stackable = data.Stackable == "True" || data.Stackable == "true" || data.Stackable == "1";
            HighIsGood = data.HighIsGood == "True" || data.HighIsGood == "true" || data.HighIsGood == "1";
        }
    }
}
