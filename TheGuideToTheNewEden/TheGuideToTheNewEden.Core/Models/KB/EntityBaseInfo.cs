using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class EntityBaseInfo
    {
        public IdName CharacterName { get; set; }
        public IdName CorpName { get; set; }
        public IdName AllianceName { get; set; }
        public IdName SystemName { get; set; }
        public IdName ConstellationName { get; set; }
        public IdName RegionName { get; set; }
        public IdName ShipName { get; set; }
        public IdName ClassName { get; set; }
        public int? Members { get; set; }
        public float? Sec { get; set; }

    }
}
