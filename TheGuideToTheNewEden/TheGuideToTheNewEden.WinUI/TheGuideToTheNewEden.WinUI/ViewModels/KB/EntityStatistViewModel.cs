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

namespace TheGuideToTheNewEden.WinUI.ViewModels.KB
{
    public class EntityStatistViewModel:BaseViewModel
    {
        private EntityBaseInfo baseInfo;
        public EntityBaseInfo BaseInfo { get => baseInfo; set=> SetProperty(ref baseInfo, value); }

        private EntityStatistic _statistic;
        public void SetData(EntityStatistic statistic)
        {
            _statistic = statistic;
        }
        public async Task InitAsync()
        {
            
        }
        private Core.Models.KB.EntityBaseInfo CreateEntityBaseInfo()
        {
            Core.Models.KB.EntityBaseInfo info = new Core.Models.KB.EntityBaseInfo();
            switch (_statistic.Type)
            {
                case "characterID":
                    {
                        //info自带角色名称，但顺便一块和军团、联盟查名字了
                        var result = ESIService.Current.EsiClient.Character.Affiliation(new int[]{ _statistic.Id}).Result;
                        if(result?.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            List<int> ids = new List<int>
                            {
                                result.Data[0].CharacterId
                            };
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
                                info.CharacterName = names.FirstOrDefault(p => p.GetCategory() == IdName.CategoryEnum.Character);
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
                        if (_statistic.Info.AllianceID > 0)
                        {
                            var names = Core.Services.IDNameService.GetByIds(new List<int>() { _statistic.Info.AllianceID });
                            if (names.NotNullOrEmpty())
                            {
                                info.AllianceName = names.FirstOrDefault(p=>p.GetCategory() == CategoryEnum.Alliance);
                            }
                        }
                    }
                    break;
                case "allianceID": break;
                case "factionID": break;
                case "shipTypeID": break;
                case "groupID": break;
                case "solarSystemID": break;
                case "regionID": break;
            }
            return info;
        }
    }
}
