using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Services;
using ZKB.NET;
using ZKB.NET.Models.Statistics;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Services;
using ZKB.NET.Models.Killmails;
using ZKB.NET.Models.KillStream;
using TheGuideToTheNewEden.Core.Helpers;
using System.Collections.ObjectModel;
using ESI.NET.Models.Killmails;

namespace TheGuideToTheNewEden.WinUI.ViewModels.KB
{
    internal class StatistTopAllTimeViewModel : StatistBaseViewModel
    {
        private List<KillStatisticInfo> _killStatisticInfos;
        public List<KillStatisticInfo> KillStatisticInfos { get => _killStatisticInfos;set=>SetProperty(ref _killStatisticInfos, value); }
        public override async Task InitAsync()
        {
            ShowWaiting();
            if(Statistic.TopAllTime.NotNullOrEmpty())
            {
                var list = await Task.Run(() =>
                {
                    List<KillStatisticInfo> infos = new List<KillStatisticInfo>();
                    foreach (var datas in Statistic.TopAllTime)
                    {
                        if(datas != null && datas.Datas.NotNullOrEmpty())
                        {
                            KillStatisticInfo info;
                            switch (datas.Type)
                            {
                                case "character":
                                case "corporation":
                                case "alliance":
                                case "faction":
                                    {
                                        info = GetInfoByIdName(datas);
                                    }
                                    break;
                                case "ship": info = GetInfoOfShip(datas); break;
                                case "system": info = GetInfoOfSystem(datas); break;
                                default:
                                    {
                                        Core.Log.Error($"Unknown TopAllTime Type :{datas.Type}");
                                        continue;
                                    };
                            }
                            infos.Add(info);
                        }
                    }
                    return infos;
                });
                if(list.NotNullOrEmpty())
                {
                    foreach(var data in list)
                    {
                        data.Type = Helpers.ResourcesHelper.GetString($"StatistTopAllTimePage_{data.Type.Substring(0, 1).ToUpper() + data.Type.Substring(1)}");
                    }
                }
                KillStatisticInfos = list.OrderByDescending(p=>p.KillDataInfos.Count).ToList();
            }
            HideWaiting();
        }

        private KillStatisticInfo GetInfoByIdName(KillStatistic killStatistic)
        {
            if(killStatistic.Datas.NotNullOrEmpty())
            {
                var names = Core.Services.IDNameService.GetByIds(killStatistic.Datas.Select(p => p.Id).ToList());
                if (names.NotNullOrEmpty())
                {
                    Converters.GameImageConverter.ImgType? imgType;
                    switch(killStatistic.Type)
                    {
                        case "character": imgType = Converters.GameImageConverter.ImgType.Character; break;
                        case "corporation": imgType = Converters.GameImageConverter.ImgType.Corporation; break;
                        case "alliance": imgType = Converters.GameImageConverter.ImgType.Alliance; break;
                        default: imgType = null; break;
                    }
                    var nameDic = names.ToDictionary(p => p.Id);
                    List<KillDataInfo> infos = new List<KillDataInfo>();
                    foreach (var data in killStatistic.Datas)
                    {
                        KillDataInfo info;
                        if (nameDic.TryGetValue(data.Id, out var name))
                        {
                            info = new KillDataInfo(data)
                            {
                                Name = name.Name,
                                Type = killStatistic.Type,
                            };
                        }
                        else
                        {
                            info = new KillDataInfo(data)
                            {
                                Name = data.Id.ToString(),
                                Type = killStatistic.Type
                            };
                        }
                        if(imgType != null)
                        {
                            info.ImgUrl = Converters.GameImageConverter.GetImageUri(data.Id, (Converters.GameImageConverter.ImgType)imgType, 32);
                        }
                        infos.Add(info);
                    }
                    return new KillStatisticInfo()
                    {
                        Type = killStatistic.Type,
                        KillDataInfos = infos
                    };
                }
            }
            return null;
        }

        private KillStatisticInfo GetInfoOfShip(KillStatistic killStatistic)
        {
            if (killStatistic.Datas.NotNullOrEmpty())
            {
                var names = Core.Services.DB.InvTypeService.QueryTypes(killStatistic.Datas.Select(p => p.Id).ToList());
                if (names.NotNullOrEmpty())
                {
                    var nameDic = names.ToDictionary(p => p.TypeID);
                    List<KillDataInfo> infos = new List<KillDataInfo>();
                    foreach (var data in killStatistic.Datas)
                    {
                        if (nameDic.TryGetValue(data.Id, out var name))
                        {
                            infos.Add(new KillDataInfo(data)
                            {
                                Name = name.TypeName,
                                Type = killStatistic.Type,
                                ImgUrl = Converters.GameImageConverter.GetImageUri(data.Id, Converters.GameImageConverter.ImgType.Type, 32)
                            });
                        }
                        else
                        {
                            infos.Add(new KillDataInfo(data)
                            {
                                Name = data.Id.ToString(),
                                Type = killStatistic.Type,
                                ImgUrl = Converters.GameImageConverter.GetImageUri(data.Id, Converters.GameImageConverter.ImgType.Type, 32)
                            });
                        }
                    }
                    return new KillStatisticInfo()
                    {
                        Type = killStatistic.Type,
                        KillDataInfos = infos
                    };
                }
            }
            return null;
        }
        private KillStatisticInfo GetInfoOfSystem(KillStatistic killStatistic)
        {
            if (killStatistic.Datas.NotNullOrEmpty())
            {
                var names = Core.Services.DB.MapSolarSystemService.Query(killStatistic.Datas.Select(p => p.Id).ToList());
                if (names.NotNullOrEmpty())
                {
                    var nameDic = names.ToDictionary(p => p.SolarSystemID);
                    List<KillDataInfo> infos = new List<KillDataInfo>();
                    foreach (var data in killStatistic.Datas)
                    {
                        if (nameDic.TryGetValue(data.Id, out var name))
                        {
                            infos.Add(new KillDataInfo(data)
                            {
                                Name = name.SolarSystemName,
                                Type = killStatistic.Type
                            });
                        }
                        else
                        {
                            infos.Add(new KillDataInfo(data)
                            {
                                Name = data.Id.ToString(),
                                Type = killStatistic.Type
                            });
                        }
                    }
                    return new KillStatisticInfo()
                    {
                        Type = killStatistic.Type,
                        KillDataInfos = infos
                    };
                }
            }
            return null;
        }
    }
}
