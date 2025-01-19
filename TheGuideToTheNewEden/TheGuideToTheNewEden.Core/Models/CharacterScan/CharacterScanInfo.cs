using CommunityToolkit.Mvvm.ComponentModel;
using Octokit;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Helpers;
using ZKB.NET;

namespace TheGuideToTheNewEden.Core.Models.CharacterScan
{
    public class CharacterScanInfo
    {
        public int Id { get => Character.Id;}
        public string Name { get => Character.Name; }
        public IdName Character { get; set; }
        public IdName Corporation { get; set; }
        public IdName Alliance { get; set; }
        public IdName Faction { get; set; }

        public ZKB.NET.Models.Statistics.EntityStatistic Statistic { get; set; }

        public int ItemLost { get; set; }

        public int ItemDestroyed { get; set; }

        public int SoloKills { get; set; }

        public int DangerRatio { get; set; }

        public int GangRatio { get; set; }

        public bool HasSupers { get; set; }

        /// <summary>
        /// 最常用的船
        /// </summary>
        public InvType[] TopShips { get; set; }

        /// <summary>
        /// 最常用的船分类
        /// </summary>
        public List<InvGroup> TopGroups { get; set; }

        /// <summary>
        /// 最常出现的星系
        /// </summary>
        public MapSolarSystem[] TopSystem { get; set; }

        /// <summary>
        /// 最常出现的星域
        /// </summary>
        public MapRegion[] TopRegion { get; set; }


        public static CharacterScanInfo Create(int characterId, int corporationId, int allianceId)
        {
            List<int> ids = new List<int>();
            if(characterId > 0) ids.Add(characterId);
            if(corporationId > 0) ids.Add(corporationId);
            if(allianceId > 0) ids.Add(allianceId);
            var names = Core.Services.IDNameService.GetByIds(ids);
            IdName character = names.FirstOrDefault(p => p.Id == characterId);
            character = character ?? new IdName(characterId, characterId.ToString(), IdName.CategoryEnum.Character);

            IdName corp = names.FirstOrDefault(p => p.Id == corporationId);
            corp = corp ?? new IdName(corporationId, corporationId.ToString(), IdName.CategoryEnum.Corporation);

            IdName alliance = null;
            if(allianceId > 0)
            {
                alliance = names.FirstOrDefault(p => p.Id == allianceId);
                alliance = alliance ?? new IdName(allianceId, allianceId.ToString(), IdName.CategoryEnum.Alliance);
            }
            else
            {
                alliance = new IdName(allianceId, "No alliance", IdName.CategoryEnum.Alliance);
            }

            CharacterScanInfo characterScanInfo = new CharacterScanInfo()
            {
                Character = character,
                Corporation = corp,
                Alliance = alliance,
            };
            return characterScanInfo;
        }

        public bool GetZKBInfo(int days = 1)
        {
            try
            {
                Statistic = ZKB.NET.ZKB.GetStatisticAsync(ZKB.NET.EntityType.CharacterID, Id).Result;
                ItemLost = Statistic.ItemLost;
                ItemDestroyed = Statistic.ItemDestroyed;
                SoloKills = Statistic.SoloKills;
                DangerRatio = Statistic.DangerRatio;
                GangRatio = Statistic.GangRatio;
                HasSupers = Statistic.HasSupers;
                //var lostGroup = Statistic.Groups.Where(p => p.ItemLost > 0);
                //if(lostGroup.Any())
                //{
                //    var topGroup = lostGroup.OrderByDescending(p => p.ItemLost).Take(3).Select(p=>p.GroupID).ToList();
                //    TopGroups = Core.Services.DB.InvGroupService.QueryGroups(topGroup);
                //}

                //击杀、损失数据
                GetKBItemInfos(out var kills, out var losses);
                if (losses.NotNullOrEmpty())
                {
                    TopShips = losses.GroupBy(p => p.Type.TypeID).OrderByDescending(p => p.Count()).Take(3).Select(p => p.First().Type).ToArray();

                }
                


            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
            return true;
        }
        private void GetKBItemInfos(out List<KBItemInfoForScan> kills, out List<KBItemInfoForScan> losses)
        {
            var kill = GetKBItemInfos(TypeModifier.Kills);
            var loss = GetKBItemInfos(TypeModifier.Losses);
            // zkb可能返回重复的km，且kill和loss混杂一起
            Dictionary<string, KBItemInfoForScan> killsDict = new Dictionary<string, KBItemInfoForScan>();
            Dictionary<string, KBItemInfoForScan> lossesDict = new Dictionary<string, KBItemInfoForScan>();
            void classify(List<KBItemInfoForScan> items)
            {
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (item.SKBDetail.Victim.CharacterId == Id)//loss
                        {
                            lossesDict.TryAdd(item.SKBDetail.Zkb.Hash, item);
                        }
                        else
                        {
                            killsDict.TryAdd(item.SKBDetail.Zkb.Hash, item);
                        }
                    }
                }
            }
            classify(kill);
            classify(loss);
            kills = killsDict.Values.ToList();
            losses = lossesDict.Values.ToList();
        }

        private List<KBItemInfoForScan> GetKBItemInfos(TypeModifier typeModifier) 
        {
            DateTime now = DateTime.Now;
            TypeModifier[] modifiers = new TypeModifier[]
                {
                    typeModifier,
                };
            ParamModifierData[] param = new ParamModifierData[]
                {
                    new ParamModifierData(ParamModifier.CharacterID, Id.ToString()),
                    new ParamModifierData(ParamModifier.Page, "1")
                };
            ParamModifierData param1 = new ParamModifierData(ParamModifier.CharacterID, Id.ToString());
            var kms = ZKB.NET.ZKB.GetKillmaillAsync(param, modifiers).Result;
            if (kms.NotNullOrEmpty())
            {
                return KBHelpers.CreateKBItemInfoForScanBatch(kms);
            }
            else
            {
                return null;
            }
        }
    }
}
