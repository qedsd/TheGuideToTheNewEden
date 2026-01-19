using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Xml.Linq;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("types")]
    public class InvType: InvTypeBase
    {
        public int GroupID { get; set; }
        public double Volume { get; set; }
        //TODO:新版本SDE无打包体积
        public double PackagedVolume { get; set; }
        public int? MarketGroupID { get; set; }
    }

    [SugarTable("types")]
    public class InvTypeBase
    {
        [Display(Order = 1)]
        [SugarColumn(ColumnName = "Id")]
        public int TypeID { get; set; }

        [Display(Order = 2)]
        [SugarColumn(IsNullable = true, ColumnName = "Name")]
        public string TypeName { get; set; }

        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
    }
}
