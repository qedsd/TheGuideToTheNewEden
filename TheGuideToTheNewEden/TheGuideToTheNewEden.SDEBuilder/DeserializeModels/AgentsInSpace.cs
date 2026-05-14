using System;
using System.Collections.Generic;
using System.Text;
using static Azure.Core.HttpHeader;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class AgentsInSpace : BaseModel
    {
        public int DungeonID { get; set; }
        public int SolarSystemID { get; set; }
        public int SpawnPointID { get; set; }
        public int TypeID { get; set; }
    }
}
