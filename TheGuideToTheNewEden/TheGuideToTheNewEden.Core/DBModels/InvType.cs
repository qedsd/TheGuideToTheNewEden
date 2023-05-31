using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("invTypes")]
    public class InvType: InvTypeBase
    {
        //public int TypeID { get; set; }
        public int GroupID { get; set; }
        //[SugarColumn(IsNullable = true)]
        //public string TypeName { get; set; }
        //[SugarColumn(IsNullable = true)]
        //public string Description { get; set; }
        public double Mass { get; set; }
        public double Volume { get; set; }
        //public double PackagedVolume { get; set; }
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
        public int TypeID { get; set; }
        [SugarColumn(IsNullable = true)]
        public string TypeName { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
    }
}
