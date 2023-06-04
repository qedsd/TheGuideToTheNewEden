using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Universe
{
    public class Structure
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int SolarSystemId { get; set; }
        public string SolarSystemName { get; set; }
        public int RegionId { get;set; }
        public string RegionName { get; set; }
        public int CharacterId { get; set; }
    }
}
