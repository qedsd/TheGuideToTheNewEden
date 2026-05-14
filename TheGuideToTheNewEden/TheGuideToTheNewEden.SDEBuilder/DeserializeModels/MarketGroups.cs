using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class MarketGroups : BaseModel
    {
        public bool HasTypes { get; set; }
        public int IconID { get; set; }
        public int? ParentGroupID { get; set; }
    }
}
