using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class MapStars : BaseModel
    {
        public double Radius { get; set; }
        public int SolarSystemID { get; set; }
        public StarStatistics Statistics { get; set; }
        public int TypeID { get; set; }
    }

    public class StarStatistics
    {
        public double Age { get; set; }
        public double Life { get; set; }
        public double Luminosity { get; set; }
        public string SpectralClass { get; set; }
        public double Temperature { get; set; }
    }
}
