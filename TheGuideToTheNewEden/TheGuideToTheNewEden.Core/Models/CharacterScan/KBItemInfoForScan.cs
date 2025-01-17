using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.Core.Models.CharacterScan
{
    public class KBItemInfoForScan
    {
        public KBItemInfoForScan() { }
        public KBItemInfoForScan(SKBDetail detail)
        {
            SKBDetail = detail;
        }
        public SKBDetail SKBDetail { get; private set; }
        public MapSolarSystem SolarSystem { get; set; }
        public MapRegion Region { get; set; }
        public InvType Type { get; set; }
        public InvGroup Group { get; set; }

        public bool IsLoss(int characterId)
        {
            return SKBDetail.Victim.CharacterId == characterId;
        }
        public bool IsKill(int characterId)
        {
            return !IsLoss(characterId);
        }
        /// <summary>
        /// 是否发生在最近一个星期
        /// </summary>
        /// <param name="now">UTC</param>
        /// <returns></returns>
        public bool IsLastWeek(DateTime now)
        {
            return (now - SKBDetail.KillmailTime).TotalDays < 7;
        }

        /// <summary>
        /// 是否黑诱导
        /// </summary>
        /// <param name="now"></param>
        /// <returns></returns>
        public bool IsCovertCyno()
        {
            return Group.GroupID == 0;//TODO:找出能开黑诱导的船型
        }
    }
}
