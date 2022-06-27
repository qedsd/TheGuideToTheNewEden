using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class Location
    {
        public int Solar_system_id { get; set; }
        public string Solar_system { get; set; }
        public long Structure_id { get; set; }
        public string Structure { get; set; }
        public int Station_id { get; set; }
        public string Station { get; set; }

        public string LocationName
        {
            get => Station == null ? Structure : Station;
        }
    }
}
