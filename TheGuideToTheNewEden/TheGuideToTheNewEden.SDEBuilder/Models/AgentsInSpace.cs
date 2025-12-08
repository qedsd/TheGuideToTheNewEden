using System;
using System.Collections.Generic;
using System.Text;
using static Azure.Core.HttpHeader;

namespace TheGuideToTheNewEden.SDEBuilder.Models
{
    public class AgentsInSpace : BaseModel
    {
        public int DungeonID { get; set; }
        public int SolarSystemID { get; set; }
        public int SpawnPointID { get; set; }
        public int TypeID { get; set; }
        public override Dictionary<string, object> GetDict(LanguageEnum language)
        {
            return new Dictionary<string, object>
            {
                { "Id", Id },
                { "DungeonID", DungeonID },
                { "SolarSystemID", SolarSystemID},
                { "SpawnPointID", SpawnPointID},
                { "TypeID", TypeID},
            };
        }
    }
}
