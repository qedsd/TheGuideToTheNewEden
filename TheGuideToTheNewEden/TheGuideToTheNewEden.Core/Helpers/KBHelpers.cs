using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.Core.Services;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public static class KBHelpers
    {
        public static async Task<KBItemInfo> CreateKBItemInfoAsync(SKBDetail detail)
        {
            KBItemInfo kbItemInfo = new KBItemInfo(detail);
            List<int> ids = new List<int>
            {
                detail.Victim.CharacterId,
                detail.Victim.CorporationId,
                detail.Victim.AllianceId,
            };
            var finalBlow = detail.Attackers.FirstOrDefault(p => p.FinalBlow);
            if (finalBlow != null)
            {
                ids.Add(finalBlow.CharacterId);
                ids.Add(finalBlow.CorporationId);
                ids.Add(finalBlow.AllianceId);
            }
            var results = await IDNameService.GetByIdsAsync(ids.Distinct().ToList());
            if(results.NotNullOrEmpty())
            {
                kbItemInfo.VictimCharacterName = results.FirstOrDefault(p=>p.Id ==  detail.Victim.CharacterId);
                kbItemInfo.VictimCorporationIdName = results.FirstOrDefault(p => p.Id == detail.Victim.CorporationId);
                kbItemInfo.VictimAllianceName = results.FirstOrDefault(p => p.Id == detail.Victim.AllianceId);

                if(finalBlow != null)
                {
                    kbItemInfo.FinalBlowCharacterName = results.FirstOrDefault(p => p.Id == finalBlow.CharacterId);
                    kbItemInfo.FinalBlowCorporationIdName = results.FirstOrDefault(p => p.Id == finalBlow.CorporationId);
                    kbItemInfo.FinalBlowAllianceName = results.FirstOrDefault(p => p.Id == finalBlow.AllianceId);
                }
            }
            return kbItemInfo;
        }
    }
}
