using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class Types : BaseModel
    {
        public double BasePrice { get; set; }
        public int GroupID { get; set; }
        public int IconID { get; set; }
        public int MarketGroupID { get; set; }
        public double Mass { get; set; }
        public int MetaGroupID { get; set; }
        public int PortionSize { get; set; }
        public bool Published { get; set; }
        public int RaceID { get; set; }
        public int VariationParentTypeID { get; set; }
        public double Volume { get; set; }
    }
}
