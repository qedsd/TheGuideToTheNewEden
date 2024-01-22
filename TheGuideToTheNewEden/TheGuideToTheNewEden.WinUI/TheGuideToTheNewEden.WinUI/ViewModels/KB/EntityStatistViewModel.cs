using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.Core.Services;
using ZKB.NET.Models.Statistics;
using TheGuideToTheNewEden.Core.Extensions;
using static TheGuideToTheNewEden.Core.DBModels.IdName;
using TheGuideToTheNewEden.WinUI.Services;

namespace TheGuideToTheNewEden.WinUI.ViewModels.KB
{
    public class EntityStatistViewModel:BaseViewModel
    {
        private Services.KBNavigationService _kbNavigationService;
        private EntityBaseInfo baseInfo;
        public EntityBaseInfo BaseInfo { get => baseInfo; set=> SetProperty(ref baseInfo, value); }

        private EntityStatistic _statistic;
        public EntityStatistic Statistic { get => _statistic; set => SetProperty(ref _statistic, value); }
        public void SetData(EntityStatistic statistic, KBNavigationService kbNavigationService)
        {
            Statistic = statistic;
            _kbNavigationService = kbNavigationService;
        }
        public async Task InitAsync()
        {
            Core.Models.KB.EntityBaseInfo info = null;
            await Task.Run(() =>
            {
                info = CreateEntityBaseInfo();
            });
            BaseInfo = info;
        }
        private Core.Models.KB.EntityBaseInfo CreateEntityBaseInfo()
        {
            Core.Models.KB.EntityBaseInfo info = new Core.Models.KB.EntityBaseInfo();
            switch (_statistic.Type)
            {
                case "characterID":
                    {
                        //info自带角色名称
                        info.CharacterName = new IdName()
                        {
                            Category = (int)CategoryEnum.Character,
                            Id = _statistic.Info.Id,
                            Name = _statistic.Info.Name
                        };
                        var result = ESIService.Current.EsiClient.Character.Affiliation(new int[]{ _statistic.Id}).Result;
                        if(result?.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            List<int> ids = new List<int>();
                            if (result.Data[0].CorporationId > 0)
                            {
                                ids.Add(result.Data[0].CorporationId);
                            }
                            if (result.Data[0].AllianceId > 0)
                            {
                                ids.Add(result.Data[0].AllianceId);
                            }
                            var names = Core.Services.IDNameService.GetByIds(ids);
                            if(names.NotNullOrEmpty())
                            {
                                info.CorpName = names.FirstOrDefault(p => p.GetCategory() == IdName.CategoryEnum.Corporation);
                                info.AllianceName = names.FirstOrDefault(p => p.GetCategory() == IdName.CategoryEnum.Alliance);
                            }
                            
                        }
                    }break;
                case "corporationID":
                    {
                        //corporation自带军团名字、ceo id、联盟id
                        info.CorpName = new IdName()
                        {
                            Category = (int)CategoryEnum.Corporation,
                            Id = _statistic.Info.Id,
                            Name = _statistic.Info.Name
                        };
                        List<int> ids = new List<int> { _statistic.Info.CEOID };
                        if (_statistic.Info.AllianceID > 0)
                        {
                            ids.Add(_statistic.Info.AllianceID);
                        }
                        var names = Core.Services.IDNameService.GetByIds(ids);
                        if (names.NotNullOrEmpty())
                        {
                            info.AllianceName = names.FirstOrDefault(p => p.GetCategory() == CategoryEnum.Alliance);
                            info.CEOName = names.FirstOrDefault((p => p.GetCategory() == CategoryEnum.Character));
                        }
                        info.Members = _statistic.Info.MemberCount;
                    }
                    break;
                case "allianceID":
                    {
                        //alliance自带联盟名字、执行军团id
                        info.AllianceName = new IdName()
                        {
                            Category = (int)CategoryEnum.Alliance,
                            Id = _statistic.Info.Id,
                            Name = _statistic.Info.Name
                        };
                        var names = Core.Services.IDNameService.GetByIds(new List<int>() { _statistic.Info.ExecutorCorpID });
                        if (names.NotNullOrEmpty())
                        {
                            info.ExecutorCorpName = names.FirstOrDefault(p => p.GetCategory() == CategoryEnum.Corporation);
                        }
                        info.Members = _statistic.Info.MemberCount;
                    }
                    break;
                case "factionID":
                    {
                        var names = Core.Services.IDNameService.GetByIds(new List<int>() { _statistic.Id });
                        if (names.NotNullOrEmpty())
                        {
                            info.FactionName = names.FirstOrDefault(p => p.GetCategory() == CategoryEnum.Faction);
                        }
                    }
                    break;
                case "shipTypeID":
                    {
                        var type = Core.Services.DB.InvTypeService.QueryType(_statistic.Id);
                        if (type != null)
                        {
                            info.ShipName = new IdName()
                            {
                                Category = (int)CategoryEnum.InventoryType,
                                Id = _statistic.Id,
                                Name = type.TypeName
                            };
                            var group = Core.Services.DB.InvGroupService.QueryGroup(type.GroupID);
                            if (group != null)
                            {
                                info.ClassName = new IdName()
                                {
                                    Category = (int)CategoryEnum.Group,
                                    Id = group.GroupID,
                                    Name = group.GroupName
                                };
                            }
                        }
                    }
                    break;
                case "groupID":
                    {
                        var group = Core.Services.DB.InvGroupService.QueryGroup(_statistic.Id);
                        if (group != null)
                        {
                            info.ClassName = new IdName()
                            {
                                Category = (int)CategoryEnum.Group,
                                Id = group.GroupID,
                                Name = group.GroupName
                            };
                        }
                    }
                    break;
                case "solarSystemID":
                    {
                        var system = Core.Services.DB.MapSolarSystemService.Query(_statistic.Id);
                        if (system != null)
                        {
                            info.SystemName = new IdName()
                            {
                                Category = (int)CategoryEnum.SolarSystem,
                                Id = system.SolarSystemID,
                                Name = system.SolarSystemName
                            };
                            var region = Core.Services.DB.MapRegionService.Query(system.RegionID);
                            if (region != null)
                            {
                                info.RegionName = new IdName()
                                {
                                    Category = (int)CategoryEnum.Region,
                                    Id = region.RegionID,
                                    Name = region.RegionName
                                };
                            }
                        }
                    }
                    break;
                case "regionID":
                    {
                        var region = Core.Services.DB.MapRegionService.Query(_statistic.Id);
                        if (region != null)
                        {
                            info.RegionName = new IdName()
                            {
                                Category = (int)CategoryEnum.Region,
                                Id = region.RegionID,
                                Name = region.RegionName
                            };
                        }
                    }
                    break;
            }
            return info;
        }
    }
}
