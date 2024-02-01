using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using ZKB.NET.Models.Killmails;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class KBItemInfo
    {
        public KBItemInfo() { }
        public KBItemInfo(SKBDetail detail)
        {
            SKBDetail = detail;
            TotalDamage = detail.Attackers.Sum(p => p.DamageDone);
        }
        public SKBDetail SKBDetail { get;private set; }
        public MapSolarSystem SolarSystem { get; set; }
        public MapRegion Region { get; set; }
        public InvType Type { get; set; }
        public InvGroup Group { get; set; }


        public IdName VictimCharacterName { get; set; }
        public IdName VictimCorporationIdName { get; set; }
        public IdName VictimAllianceName { get; set; }
       
        public IdName FinalBlowCharacterName { get; set; }
        public IdName FinalBlowCorporationIdName { get; set; }
        public IdName FinalBlowAllianceName { get; set; }

        public IdName Victim
        {
            get
            {
                if(VictimCharacterName != null)
                {
                    return VictimCharacterName;
                }
                if(VictimCorporationIdName != null)
                {
                    return VictimCorporationIdName;
                }
                if(VictimAllianceName != null)
                {
                    return VictimAllianceName;
                }
                return null;
            }
        }

        public IdName FinalBlow
        {
            get
            {
                if (FinalBlowCharacterName != null)
                {
                    return FinalBlowCharacterName;
                }
                if (FinalBlowCorporationIdName != null)
                {
                    return FinalBlowCorporationIdName;
                }
                if (FinalBlowAllianceName != null)
                {
                    return FinalBlowAllianceName;
                }
                return null;
            }
        }

        public string Date { get => SKBDetail.KillmailTime.ToShortDateString(); }

        public string Time { get => SKBDetail.KillmailTime.ToShortTimeString(); }

        public int TotalDamage { get;set; }

        public Attacker GetFinalBlow()
        {
            return SKBDetail.Attackers.FirstOrDefault(p => p.FinalBlow);
        }

        public Attacker GetTopDamage()
        {
            return SKBDetail.Attackers.OrderByDescending(p=>p.DamageDone).FirstOrDefault();
        }
    }
}
