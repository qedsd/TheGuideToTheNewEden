using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Xml.Linq;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("invTypes")]
    public class InvType: InvTypeBase
    {
        public int GroupID { get; set; }
        public double Mass { get; set; }
        public double Volume { get; set; }
        public double PackagedVolume { get; set; }
        public double Capacity { get; set; }
        public int PortionSize { get; set; }
        //public int FactionID { get; set; }
        public int RaceID { get; set; }
        public double BasePrice { get; set; }
        public int Published { get; set; }
        public int? MarketGroupID { get; set; }
        public int IconID { get; set; }
        public int SoundID { get; set; }
        [SugarColumn()]
        public int GraphicID { get; set; }
    }

    [SugarTable("invTypes")]
    public class InvTypeBase
    {
        [Display(Order = 1)]
        public int TypeID { get; set; }

        [Display(Order = 2)]
        [SugarColumn(IsNullable = true)]
        public string TypeName { get; set; }

        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
    }
}
