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
    internal class StatistMonthViewModel : StatistBaseViewModel
    {
        private List<MonthData> _monthDatas;
        public List<MonthData> MonthDatas { get => _monthDatas; set=>SetProperty(ref _monthDatas, value); }
        public override async Task InitAsync()
        {
            ShowWaiting();
            if(Statistic.Months.NotNullOrEmpty())
            {
                //var list = await Task.Run(() =>
                //{
                //    List<MonthGroupData> months = new List<MonthGroupData>();
                //    var groups = Statistic.Months.GroupBy(p => p.Year);
                //    foreach( var group in groups)
                //    {
                //        MonthGroupData monthGroupData = new MonthGroupData()
                //        {
                //            Year = group.Key,
                //        };
                //        monthGroupData.MonthDatas = group.ToList().OrderBy(p => p.Month).ToList();
                //        months.Add(monthGroupData);
                //    }
                //    return months;
                //});
                MonthDatas = Statistic.Months.OrderBy(p => p.Month).ToList();
            }
            HideWaiting();
        }
    }
}
