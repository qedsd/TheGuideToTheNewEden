using CommunityToolkit.Mvvm.ComponentModel;
using ESI.NET.Models.Location;
using Octokit;
using SqlSugar.DistributedSystem.Snowflake;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Text;
using System.Threading;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Helpers;
using ZKB.NET;
using ZKB.NET.Models.Statistics.Top;

namespace TheGuideToTheNewEden.Core.Models.CharacterScan
{
    public class CharacterScanInfo : ObservableObject
    {
        public int Id { get => Character.Id;}
        public string Name { get => Character.Name; }
        public IdName Character { get; set; }
        public IdName Corporation { get; set; }
        public IdName Alliance { get; set; }
        public IdName Faction { get; set; }

        private ZKB.NET.Models.Statistics.EntityStatistic _statistic;
        public ZKB.NET.Models.Statistics.EntityStatistic Statistic { get => _statistic; set => SetProperty(ref _statistic, value); }

        private int _itemLost;
        public int ItemLost { get => _itemLost; set => SetProperty(ref _itemLost, value); }

        private int _itemDestroyed;
        public int ItemDestroyed { get => _itemDestroyed; set => SetProperty(ref _itemDestroyed, value); }

        public int _soloKills;
        public int SoloKills { get => _soloKills; set => SetProperty(ref _soloKills, value); }

        private float _soloRatio;
        /// <summary>
        /// solo概率
        /// 小数形式，eg：0.01
        /// </summary>
        public float SoloRatio { get => _soloRatio; set => SetProperty(ref _soloRatio, value); }

        private string _soloStr;
        public string SoloStr { get => _soloStr; set => SetProperty(ref _soloStr, value); }

        private int _dangerRatio;
        public int DangerRatio { get => _dangerRatio; set => SetProperty(ref _dangerRatio, value); }

        private int gangRatio;
        public int GangRatio { get => gangRatio; set => SetProperty(ref gangRatio, value); }

        private bool _hasSupers;
        public bool HasSupers { get => _hasSupers; set => SetProperty(ref _hasSupers, value); }

        private bool _covertCyno;
        /// <summary>
        /// 是否使用过支持黑诱导的船
        /// </summary>
        public bool CovertCyno { get => _covertCyno; set => SetProperty(ref _covertCyno, value); }

        private List<InvType> _topShips;
        /// <summary>
        /// 最常用的船
        /// </summary>
        public List<InvType> TopShips
        { 
            get => _topShips;
            set 
            {
                SetProperty(ref _topShips, value);
                if (value.NotNullOrEmpty())
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var item in value)
                    {
                        sb.Append(item.TypeName);
                        sb.Append("  ");
                    }
                    TopShipsStr = sb.Remove(sb.Length - 2, 2).ToString();
                }
                else
                {
                    TopShipsStr = string.Empty;
                }
            }
        }

        private string _topShipsStr;
        public string TopShipsStr { get => _topShipsStr; set => SetProperty(ref _topShipsStr, value); }

        private List<InvGroup> _topGroups;
        /// <summary>
        /// 最常用的船分类
        /// </summary>
        public List<InvGroup> TopGroups
        {
            get => _topGroups;
            set
            {
                SetProperty(ref _topGroups, value);
                if (value.NotNullOrEmpty())
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var item in value)
                    {
                        sb.Append(item.GroupName);
                        sb.Append("  ");
                    }
                    TopGroupsStr = sb.Remove(sb.Length - 2, 2).ToString();
                }
                else
                {
                    TopGroupsStr = string.Empty;
                }
            }
        }

        private string _topGroupsStr;
        public string TopGroupsStr { get => _topGroupsStr; set => SetProperty(ref _topGroupsStr, value); }

        private List<MapSolarSystem> _topSystems;
        /// <summary>
        /// 最常出现的星系
        /// </summary>
        public List<MapSolarSystem> TopSystems
        {
            get => _topSystems;
            set
            {
                SetProperty(ref _topSystems, value);
                if (TopSystems.NotNullOrEmpty())
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var item in TopSystems)
                    {
                        sb.Append(item.SolarSystemName);
                        sb.Append("  ");
                    }
                    TopSystemsStr = sb.Remove(sb.Length - 2, 2).ToString();
                }
                else
                {
                    TopSystemsStr = string.Empty;
                }
            }
        }

        private string _topSystemsStr;
        public string TopSystemsStr { get => _topSystemsStr; set => SetProperty(ref _topSystemsStr, value); }

        private List<MapRegion> _topRegions;
        /// <summary>
        /// 最常出现的星域
        /// </summary>
        public List<MapRegion> TopRegions
        {
            get => _topRegions;
            set
            {
                SetProperty(ref _topRegions, value);
                if (TopRegions.NotNullOrEmpty())
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var item in TopRegions)
                    {
                        sb.Append(item.RegionName);
                        sb.Append("  ");
                    }
                    TopRegionsStr = sb.Remove(sb.Length - 2, 2).ToString();
                }
                else
                {
                    TopRegionsStr = string.Empty;
                }
            }
        }

        private string _topRegionsStr;
        public string TopRegionsStr { get => _topRegionsStr; set => SetProperty(ref _topRegionsStr, value); }

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

        public bool GetZKBInfo()
        {
            try
            {
                try
                {
                    Statistic = ZKB.NET.ZKB.GetStatisticAsync(ZKB.NET.EntityType.CharacterID, Id).Result;
                }
                catch
                {
                    Thread.Sleep(1000);
                    Statistic = ZKB.NET.ZKB.GetStatisticAsync(ZKB.NET.EntityType.CharacterID, Id).Result;
                }
                ItemLost = Statistic.ItemLost;
                ItemDestroyed = Statistic.ItemDestroyed;
                SoloKills = Statistic.SoloKills;
                SoloRatio = (float)SoloKills / ItemDestroyed;
                if (SoloKills > 0)
                {
                    SoloStr = $"{SoloKills} ({(SoloRatio * 100).ToString("N2")}%)";
                }
                else
                {
                    SoloStr = "0";
                }
                DangerRatio = Statistic.DangerRatio;
                GangRatio = Statistic.GangRatio;
                HasSupers = Statistic.HasSupers;
                var lostGroup = Statistic.Groups.Where(p => p.GroupID != 29 && p.ItemLost > 0);//排除太空舱29
                if (lostGroup.Any())
                {
                    var topGroupIds = lostGroup.OrderByDescending(p => p.ItemLost).Take(3).Select(p => p.GroupID).ToList();
                    var groups = Core.Services.DB.InvGroupService.QueryGroups(topGroupIds);
                    var topGroups = new List<InvGroup>(topGroupIds.Count);
                    foreach (var group in topGroupIds)
                    {
                        topGroups.Add(groups.FirstOrDefault(p => p.GroupID == group));
                    }
                    TopGroups = topGroups;

                    foreach(var group in groups)
                    {
                        if(CovertCynoGroup.Contains(group.GroupID))
                        {
                            CovertCyno = true;
                            break;
                        }
                    }
                }
                if(Statistic.TopAllTime.NotNullOrEmpty())
                {
                    var topKillShip = Statistic.TopAllTime.FirstOrDefault(p => p.Type == "ship");
                    if (topKillShip != null && topKillShip.Datas.NotNullOrEmpty())
                    {
                        var topKillShipIds = topKillShip.Datas.Take(3).Select(p => p.Id).ToList();
                        var ships = Services.DB.InvTypeService.QueryTypes(topKillShipIds);
                        var topShips = new List<InvType>(topKillShipIds.Count);
                        foreach (var id in topKillShipIds)
                        {
                            topShips.Add(ships.FirstOrDefault(p => p.TypeID == id));
                        }
                        TopShips = topShips;
                    }

                    var topSystem = Statistic.TopAllTime.FirstOrDefault(p => p.Type == "system");
                    if (topSystem != null && topSystem.Datas.NotNullOrEmpty())
                    {
                        var allSystemIds = topSystem.Datas.Select(p => p.Id).ToList();
                        var allsSystemsDict = Services.DB.MapSolarSystemService.Query(allSystemIds).ToDictionary(p => p.SolarSystemID);
                        var topSystemIds = allSystemIds.Take(3).ToList();
                        var topSystems = new List<MapSolarSystem>(topSystemIds.Count);
                        foreach (var id in topSystemIds)
                        {
                            if (allsSystemsDict.TryGetValue(id, out var mapSolarSystem))
                            {
                                topSystems.Add(mapSolarSystem);
                            }
                        }
                        TopSystems = topSystems;

                        Dictionary<int, int> regionKills = new Dictionary<int, int>();
                        foreach (var data in topSystem.Datas)
                        {
                            if (allsSystemsDict.TryGetValue(data.Id, out var mapSolarSystem))
                            {
                                int regionId = mapSolarSystem.RegionID;
                                int oldCount = 0;
                                if (!regionKills.TryGetValue(regionId, out oldCount))
                                {
                                    oldCount = 0;
                                }
                                regionKills.Remove(regionId);
                                regionKills.Add(regionId, oldCount + data.Kills);
                            }
                        }
                        var topRegionIds = regionKills.OrderBy(p => p.Value).Take(3).Select(p => p.Key).ToList();
                        var topRegions = Services.DB.MapRegionService.Query(topRegionIds);
                        var topRegions2 = new List<MapRegion>(topRegionIds.Count);
                        foreach (var id in topRegionIds)
                        {
                            topRegions2.Add(topRegions.FirstOrDefault(p => p.RegionID == id));
                        }
                        TopRegions = topRegions2;
                    }
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

        /// <summary>
        /// 支持开黑诱导的船
        /// 隐形特勤舰 黑隐特勤舰 战略巡洋舰
        /// </summary>
        private static readonly HashSet<int> CovertCynoGroup = new HashSet<int>
        {
            830,898,963
        };
    }
}
