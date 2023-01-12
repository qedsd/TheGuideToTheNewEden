using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("invGroups")]
    public class InvGroup: InvGroupBase
    {
        public int CategoryID { get; set; }
        public int IconID { get; set; }
        public bool UseBasePrice { get; set; }
        public bool Anchored { get; set; }
        public bool Anchorable { get; set; }
        public bool FittableNonSingleton { get; set; }
        public bool Published { get; set; }
    }

    [SugarTable("invGroups")]
    public class InvGroupBase
    {
        public int GroupID { get; set; }
        [SugarColumn(IsNullable = true)]
        public string GroupName { get; set; }
    }
}
