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
    internal class StatistGruopViewModel : StatistBaseViewModel
    {
        private List<GroupDataInfo> _groupDataInfos;
        public List<GroupDataInfo> GroupDataInfos { get => _groupDataInfos; set=>SetProperty(ref _groupDataInfos, value); }
        public override async Task InitAsync()
        {
            ShowWaiting();
            if(Statistic.TopAllTime.NotNullOrEmpty())
            {
                var list = await Task.Run(() =>
                {
                    var invGroups = Core.Services.DB.InvGroupService.QueryGroups(Statistic.Groups.Select(p => p.GroupID).ToList());
                    var dic = invGroups.ToDictionary(p => p.GroupID);
                    List<GroupDataInfo> infos = new List<GroupDataInfo>();
                    foreach (var data in Statistic.Groups)
                    {
                        GroupDataInfo groupDataInfo = new GroupDataInfo(data);
                        if(dic.TryGetValue(data.GroupID, out var g))
                        {
                            groupDataInfo.Name = g.GroupName;
                        }
                        else
                        {
                            groupDataInfo.Name = data.GroupID.ToString();
                        }
                        infos.Add(groupDataInfo);
                    }
                    infos = infos.OrderByDescending(p=>p.ItemDestroyed).ToList();
                    for(int i =0;i<infos.Count;i++)
                    {
                        infos[i].No = i + 1;
                    }
                    return infos;
                });
                GroupDataInfos = list;
            }
            HideWaiting();
        }
    }
}
