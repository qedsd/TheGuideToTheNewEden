using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class KBItemInfo
    {
        public KBItemInfo(SKBDetail detail)
        {
            SKBDetail = detail;
        }
        public SKBDetail SKBDetail { get;private set; }
        public MapSolarSystem SolarSystem { get; set; }
        public MapRegion Region { get; set; }


        public IdName VictimCharacterName { get; set; }
        public IdName VictimCorporationIdName { get; set; }
        public IdName VictimAllianceName { get; set; }
        /// <summary>
        /// 受害者名称
        /// 不一定是人名，还可能是军团，如建筑物
        /// </summary>
        public string VictimName { get; set; }
        /// <summary>
        /// 受害者阵营名称
        /// 有联盟优先表示联盟名称，无则为军团
        /// </summary>
        public string VictimFactionName { get; set;}

        /// <summary>
        /// 最后一击名称
        /// 不一定是人名，还可能是军团，如建筑物
        /// </summary>
        public string FinalBlowName { get; set; }
        /// <summary>
        /// 最后一击阵营名称
        /// 有联盟优先表示联盟名称，无则为军团
        /// </summary>
        public string FinalBlowFactionName { get; set; }
        public IdName FinalBlowCharacterName { get; set; }
        public IdName FinalBlowCorporationIdName { get; set; }
        public IdName FinalBlowAllianceName { get; set; }
    }
}
