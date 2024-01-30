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
using CommunityToolkit.WinUI.UI.Controls.TextToolbarSymbols;

namespace TheGuideToTheNewEden.WinUI.ViewModels.KB
{
    internal class StatistSuperViewModel : StatistBaseViewModel
    {
        private List<KillDataInfo> _titans;
        public List<KillDataInfo> Titans { get => _titans; set=>SetProperty(ref _titans, value); }
        private List<KillDataInfo> _supercarriers;
        public List<KillDataInfo> Supercarriers { get => _supercarriers; set => SetProperty(ref _supercarriers, value); }
        public override async Task InitAsync()
        {
            ShowWaiting();
            if(Statistic.Supers?.Titans != null && Statistic.Supers.Titans.Datas.NotNullOrEmpty())
            {
                var list = await GetInfosAsync(Statistic.Supers.Titans.Datas);
                list = list.OrderByDescending(p => p.Kills).ToList();
                for(int i = 0;i<list.Count;i++)
                {
                    list[i].No = i + 1;
                }
                Titans = list;
            }
            if (Statistic.Supers?.Supercarriers != null && Statistic.Supers.Supercarriers.Datas.NotNullOrEmpty())
            {
                var list = await GetInfosAsync(Statistic.Supers.Supercarriers.Datas);
                list = list.OrderByDescending(p => p.Kills).ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].No = i + 1;
                }
                Supercarriers = list;
            }
            HideWaiting();
        }
        private async Task<List<KillDataInfo>> GetInfosAsync(List<KillData> killDatas)
        {
            return await Task.Run(() =>
            {
                if (killDatas.NotNullOrEmpty())
                {
                    var names = Core.Services.IDNameService.GetByIds(killDatas.Select(p => p.Id).ToList());
                    if (names.NotNullOrEmpty())
                    {
                        var nameDic = names.ToDictionary(p => p.Id);
                        List<KillDataInfo> infos = new List<KillDataInfo>();
                        foreach (var data in killDatas)
                        {
                            KillDataInfo info;
                            if (nameDic.TryGetValue(data.Id, out var name))
                            {
                                info = new KillDataInfo(data)
                                {
                                    Name = name.Name,
                                };
                            }
                            else
                            {
                                info = new KillDataInfo(data)
                                {
                                    Name = data.Id.ToString()
                                };
                            }
                            info.ImgUrl = Converters.GameImageConverter.GetImageUri(data.Id, Converters.GameImageConverter.ImgType.Character, 32);
                            infos.Add(info);
                        }
                        return infos;
                    }
                }
                return null;
            });
        }
    }
}
