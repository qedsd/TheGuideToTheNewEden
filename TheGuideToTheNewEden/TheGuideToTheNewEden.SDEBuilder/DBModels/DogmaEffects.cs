using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("dogmaEffects")]
    [SugarIndex("index_dogmaEffects_id", nameof(Id), OrderByType.Asc)]
    public class DogmaEffects
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string DisplayName { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        public int EffectCategoryID { get; set; }
        public bool Published { get; set; }
        public bool IsOffensive { get; set; }
        public bool IsAssistance { get; set; }
        public bool IsWarpSafe { get; set; }
        public bool ElectronicChance { get; set; }
        public bool PropulsionChance { get; set; }
        public bool RangeChance { get; set; }
        public bool DisallowAutoRepeat { get; set; }
        public int DischargeAttributeID { get; set; }
        public int DurationAttributeID { get; set; }
        public int FalloffAttributeID { get; set; }
        public int RangeAttributeID { get; set; }
        public int TrackingSpeedAttributeID { get; set; }
        public int Distribution { get; set; }
        public int IconID { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Guid { get; set; }
        public int PreExpression { get; set; }
        public int PostExpression { get; set; }
        public DogmaEffects() { }
        public DogmaEffects(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.DogmaEffects;
            if (data == null) return;
            Id = data.Id;
            Name = data.Name;
            DisplayName = data.DisplayName?.GetValue(language);
            Description = data.Description?.GetValue(language);
            EffectCategoryID = data.EffectCategoryID;
            Published = data.Published;
            IsOffensive = data.IsOffensive;
            IsAssistance = data.IsAssistance;
            IsWarpSafe = data.IsWarpSafe;
            ElectronicChance = data.ElectronicChance;
            PropulsionChance = data.PropulsionChance;
            RangeChance = data.RangeChance;
            DisallowAutoRepeat = data.DisallowAutoRepeat;
            DischargeAttributeID = data.DischargeAttributeID;
            DurationAttributeID = data.DurationAttributeID;
            FalloffAttributeID = data.FalloffAttributeID;
            RangeAttributeID = data.RangeAttributeID;
            TrackingSpeedAttributeID = data.TrackingSpeedAttributeID;
            Distribution = data.Distribution;
            IconID = data.IconID;
            Guid = data.Guid;
            PreExpression = data.PreExpression;
            PostExpression = data.PostExpression;
        }
    }
}
