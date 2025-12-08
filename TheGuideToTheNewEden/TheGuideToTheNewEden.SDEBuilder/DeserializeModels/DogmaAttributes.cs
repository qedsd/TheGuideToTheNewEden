using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class DogmaAttributes : BaseModel
    {
        public int AttributeCategoryID { get; set; }
        public int DataType { get; set; }
        public double DefaultValue { get; set; }
        public new string Names { get; set; }
        public new string Descriptions { get; set; }
        public string Description { get; set; }
        public Languages DisplayName { get; set; }
        public string DisplayWhenZero { get; set; }
        public string HighIsGood { get; set; }
        public int IconID { get; set; }
        public string Name { get; set; }
        public string Published { get; set; }
        public string Stackable { get; set; }
        public Languages TooltipDescription { get; set; }
        public Languages TooltipTitle { get; set; }
        public int UnitID { get; set; }
    }
}
