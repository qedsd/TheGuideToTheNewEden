using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using static Azure.Core.HttpHeader;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class Groups : BaseModel
    {
        public int CategoryID { get; set; }
        public int IconID { get; set; }
        public bool UseBasePrice { get; set; }
        public bool Anchored { get; set; }
        public bool Anchorable { get; set; }
        public bool FittableNonSingleton { get; set; }
        public bool Published { get; set; }
    }
}
