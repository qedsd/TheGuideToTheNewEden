using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.Core.Services.DB;
using ZKB.NET.Models.Killmails;
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
            ids = ids.Distinct().ToList();
            ids.Remove(0);
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
            kbItemInfo.SolarSystem = await MapSolarSystemService.QueryAsync(detail.SolarSystemId);
            if(kbItemInfo.SolarSystem != null)
            {
                kbItemInfo.Region = await MapRegionService.QueryAsync(kbItemInfo.SolarSystem.RegionID);
            }
            kbItemInfo.Type = await InvTypeService.QueryTypeAsync(detail.Victim.ShipTypeId);
            if(kbItemInfo.Type != null)
            {
                kbItemInfo.Group = await InvGroupService.QueryGroupAsync(kbItemInfo.Type.GroupID);
            }
            return kbItemInfo;
        }

        public static KBItemInfo CreateKBItemInfo(SKBDetail detail)
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
            ids = ids.Distinct().ToList();
            ids.Remove(0);
            var results = IDNameService.GetByIds(ids.Distinct().ToList());
            if (results.NotNullOrEmpty())
            {
                kbItemInfo.VictimCharacterName = results.FirstOrDefault(p => p.Id == detail.Victim.CharacterId);
                kbItemInfo.VictimCorporationIdName = results.FirstOrDefault(p => p.Id == detail.Victim.CorporationId);
                kbItemInfo.VictimAllianceName = results.FirstOrDefault(p => p.Id == detail.Victim.AllianceId);

                if (finalBlow != null)
                {
                    kbItemInfo.FinalBlowCharacterName = results.FirstOrDefault(p => p.Id == finalBlow.CharacterId);
                    kbItemInfo.FinalBlowCorporationIdName = results.FirstOrDefault(p => p.Id == finalBlow.CorporationId);
                    kbItemInfo.FinalBlowAllianceName = results.FirstOrDefault(p => p.Id == finalBlow.AllianceId);
                }
            }
            kbItemInfo.SolarSystem = MapSolarSystemService.Query(detail.SolarSystemId);
            if (kbItemInfo.SolarSystem != null)
            {
                kbItemInfo.Region = MapRegionService.Query(kbItemInfo.SolarSystem.RegionID);
            }
            kbItemInfo.Type = InvTypeService.QueryType(detail.Victim.ShipTypeId);
            if (kbItemInfo.Type != null)
            {
                kbItemInfo.Group = InvGroupService.QueryGroup(kbItemInfo.Type.GroupID);
            }
            return kbItemInfo;
        }

        public static List<KBItemInfo> CreateKBItemInfo(List<ZKillmaill> killmaills)
        {
            if (killmaills.NotNullOrEmpty())
            {
                ESI.NET.Models.Killmails.Information getInfo(ZKillmaill zKillmaill)
                {
                    var resp = ESIService.Current.EsiClient.Killmails.Information(zKillmaill.Zkb.Hash.ToString(), zKillmaill.KillmailId).Result;
                    if (resp.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        return resp.Data;
                    }
                    else
                    {
                        Core.Log.Error($"Can not get Killmail info of {zKillmaill.KillmailId} {zKillmaill.Zkb.Hash}");
                        return null;
                    }
                }
                var kmInfos = Core.Helpers.ThreadHelper.Run(killmaills, getInfo);
                if (kmInfos.NotNullOrEmpty())
                {
                    var kmDic = killmaills.ToDictionary(p => p.KillmailId);
                    List<SKBDetail> kmDetails = new List<SKBDetail>();
                    foreach (var kmInfo in kmInfos)
                    {
                        if (kmInfo != null)
                        {
                            if (kmDic.TryGetValue((int)kmInfo.KillmailId, out var km))
                            {
                                SKBDetail detail = kmInfo.DepthClone<SKBDetail>();
                                detail.Zkb = km.Zkb;
                                kmDetails.Add(detail);
                            }
                        }
                    }
                    return Core.Helpers.ThreadHelper.Run(kmDetails, Core.Helpers.KBHelpers.CreateKBItemInfo).ToList();
                }
            }
            return null;
        }

        public static async Task<List<KBItemInfo>> CreateKBItemInfoAsync(List<ZKillmaill> killmaills)
        {
            return await Task.Run(() => CreateKBItemInfo(killmaills));
        }
        public static async Task<KBItemInfo> CreateKBItemInfoAsync(ZKillmaill killmaill)
        {
            return (await Task.Run(() => CreateKBItemInfo(new List<ZKillmaill>() { killmaill})))?.FirstOrDefault();
        }
    }
}
