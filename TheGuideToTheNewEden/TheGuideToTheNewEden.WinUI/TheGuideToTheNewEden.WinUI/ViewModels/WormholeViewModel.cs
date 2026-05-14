using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Models;
using ZKB.NET;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Services;
using ZKB.NET.Models.Killmails;
using Octokit;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using TheGuideToTheNewEden.WinUI.Services;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class WormholeFactionStatistic
    {
        public IdName Name { get; set; }
        public double LastActiveDay { get; set; }
        public int KMCount { get; set; }
    }
    public class WormholeActive
    {
        /// <summary>
        /// 将DayOfWeek转换为周一=0，周天=6格式
        /// </summary>
        public int Day
        {
            get
            {
                int dayOfWeek = (int)DayOfWeek - 1;
                return dayOfWeek < 0 ? 6 : dayOfWeek;
            }
        }
        public DayOfWeek DayOfWeek { get; set; }
        public int Hour {  get; set; }
        public int Count {  get; set; }

        public string ToolTip
        {
            get
            {
                string hourStart = $"{Hour}:00";
                string hourEnd = $"{Hour}:59";
                return $"{Helpers.ResourcesHelper.GetString($"WormholePage_ZKB_DayOfWeek{Day + 1}")} {hourStart}-{hourEnd} {Count} Kills";
            }
        }
    }
    public class WormholeViewModel : BaseViewModel
    {
        private List<WormholeActive> _wormholeActives1;

        /// <summary>
        /// 周一
        /// </summary>
        public List<WormholeActive> WormholeActives1 { get => _wormholeActives1; set => SetProperty(ref _wormholeActives1, value); }

        private List<WormholeActive> _wormholeActives2;
        public List<WormholeActive> WormholeActives2 { get => _wormholeActives2; set => SetProperty(ref _wormholeActives2, value); }

        private List<WormholeActive> _wormholeActives3;
        public List<WormholeActive> WormholeActives3 { get => _wormholeActives3; set => SetProperty(ref _wormholeActives3, value); }

        private List<WormholeActive> _wormholeActives4;
        public List<WormholeActive> WormholeActives4 { get => _wormholeActives4; set => SetProperty(ref _wormholeActives4, value); }

        private List<WormholeActive> _wormholeActives5;
        public List<WormholeActive> WormholeActives5 { get => _wormholeActives5; set => SetProperty(ref _wormholeActives5, value); }

        private List<WormholeActive> _wormholeActives6;
        public List<WormholeActive> WormholeActives6 { get => _wormholeActives6; set => SetProperty(ref _wormholeActives6, value); }

        private List<WormholeActive> _wormholeActives7;
        public List<WormholeActive> WormholeActives7 { get => _wormholeActives7; set => SetProperty(ref _wormholeActives7, value); }

        private Wormhole _selectedWormhole;
        public Wormhole SelectedWormhole 
        { 
            get => _selectedWormhole;
            set
            {
                if(SetProperty(ref _selectedWormhole, value))
                {
                    WormholeDetail = WormholeDetail.Create(value);
                    _=AnalyzeKBAsync(WormholeDetail.Id);
                }
            }
        }

        private List<Wormhole> _searchWormholes;
        public List<Wormhole> SearchWormholes { get => _searchWormholes; set => SetProperty(ref _searchWormholes, value); }

        private int _kmCount;
        public int KmCount { get => _kmCount; set => SetProperty(ref _kmCount, value); }

        private string _kmDataFrom;
        public string KMDataFrom { get => _kmDataFrom; set => SetProperty(ref _kmDataFrom, value); }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    Search(value);
                }
            }
        }

        private WormholeDetail _wormholeDetail;
        public WormholeDetail WormholeDetail { get => _wormholeDetail; set => SetProperty(ref _wormholeDetail, value); }

        private bool _loadingZKB;
        public bool LoadingZKB { get => _loadingZKB; set => SetProperty(ref _loadingZKB, value); }

        private List<WormholeFactionStatistic> _corpStatistics;
        public List<WormholeFactionStatistic> CorpStatistics { get => _corpStatistics; set => SetProperty(ref _corpStatistics, value); }

        private List<WormholeFactionStatistic> _allianceStatistics;
        public List<WormholeFactionStatistic> AllianceStatistics { get => _allianceStatistics; set => SetProperty(ref _allianceStatistics, value); }

        private List<string> _links;
        public List<string> Links { get => _links; set => SetProperty(ref _links, value); }

        public WormholeViewModel()
        {
            _links = new List<string>()
            {
                "Anoik","Dotlan","Ellatha","Zkillboard","Chruker"
            };
        }

        private async void Search(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                SearchWormholes = null;
            }
            else
            {
                SearchWormholes = await Core.Services.DB.WormholeService.QueryWormholeAsync(text);
            }
        }

        private async Task AnalyzeKBAsync(int id)
        {
            try
            {
                LoadingZKB = true;
                WormholeActives1 = null;
                WormholeActives2 = null;
                WormholeActives3 = null;
                WormholeActives4 = null;
                WormholeActives5 = null;
                WormholeActives6 = null;
                WormholeActives7 = null;
                KmCount = 0;
                ParamModifierData[] param = new ParamModifierData[]
                {
                    new ParamModifierData(ParamModifier.SystemID, id.ToString()),
                    new ParamModifierData(ParamModifier.Page, "1")
                };
                var killmaills = await ZKB.NET.ZKB.GetKillmaillAsync(param, null);//一页最多200个kb
                if (killmaills.NotNullOrEmpty())
                {
                    KmCount = killmaills.Count;
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
                    var kmInfos = await Core.Helpers.ThreadHelper.RunAsync(killmaills, getInfo);
                    if (kmInfos.NotNullOrEmpty())
                    {
                        KMDataFrom = string.Format(Helpers.ResourcesHelper.GetString("WormholePage_ZKB_DataFrom"), (DateTime.UtcNow - kmInfos.Min(p => p.KillmailTime)).TotalDays.ToString("N0"), killmaills.Count);
                        var groupByDayOfWeek = kmInfos.GroupBy(p => p.KillmailTime.ToLocalTime().DayOfWeek);
                        List<WormholeActive>[] wormholeActivesItemSource = new List<WormholeActive>[7];
                        int maxKM = 0;
                        foreach (var groupItem in groupByDayOfWeek)
                        {
                            List<WormholeActive> wormholeActives = new List<WormholeActive>();
                            Dictionary<int, WormholeActive> wormholeActiveDict = new Dictionary<int, WormholeActive>();//key=小时
                            foreach (var km in groupItem)
                            {
                                int hour = km.KillmailTime.ToLocalTime().Hour;
                                WormholeActive wormholeActive = null;
                                if (!wormholeActiveDict.TryGetValue(hour, out wormholeActive))
                                {
                                    wormholeActive = new WormholeActive()
                                    {
                                        DayOfWeek = groupItem.Key,
                                        Hour = hour
                                    };
                                    wormholeActiveDict.Add(hour, wormholeActive);
                                }
                                wormholeActive.Count++;
                            }
                            int max = wormholeActiveDict.Values.Max(p => p.Count);
                            if (max > maxKM)
                            {
                                maxKM = max;
                            }
                            wormholeActivesItemSource[(int)groupItem.Key] = wormholeActiveDict.Values.OrderBy(p => p.Hour).ToList();
                        }

                        foreach(var w in wormholeActivesItemSource)//统一数据最大最小范围
                        {
                            if(w .NotNullOrEmpty())
                            {
                                w.Add(new WormholeActive()
                                {
                                    DayOfWeek = w.First().DayOfWeek,
                                    Count = 0,
                                    Hour = -6
                                });
                                w.Add(new WormholeActive()
                                {
                                    DayOfWeek = w.First().DayOfWeek,
                                    Count = maxKM,
                                    Hour = 30
                                });
                            }
                        }
                        WormholeActives1 = wormholeActivesItemSource[1];
                        WormholeActives2 = wormholeActivesItemSource[2];
                        WormholeActives3 = wormholeActivesItemSource[3];
                        WormholeActives4 = wormholeActivesItemSource[4];
                        WormholeActives5 = wormholeActivesItemSource[5];
                        WormholeActives6 = wormholeActivesItemSource[6];
                        WormholeActives7 = wormholeActivesItemSource[0];

                        //统计活跃军团联盟
                        Dictionary<int, List<ESI.NET.Models.Killmails.Information>> corpStatistic = new Dictionary<int, List<ESI.NET.Models.Killmails.Information>>();//key = id
                        Dictionary<int, List<ESI.NET.Models.Killmails.Information>> allianceStatistic = new Dictionary<int, List<ESI.NET.Models.Killmails.Information>>();//key = id

                        void corpAddOne(int id, ESI.NET.Models.Killmails.Information km)
                        {
                            List<ESI.NET.Models.Killmails.Information> kms;
                            if(!corpStatistic.TryGetValue(id, out kms))
                            {
                                kms = new List<ESI.NET.Models.Killmails.Information>();
                                corpStatistic[id] = kms;
                            }
                            kms.Add(km);
                        }
                        void allianceAddOne(int id, ESI.NET.Models.Killmails.Information km)
                        {
                            List<ESI.NET.Models.Killmails.Information> kms;
                            if (!allianceStatistic.TryGetValue(id, out kms))
                            {
                                kms = new List<ESI.NET.Models.Killmails.Information>();
                                allianceStatistic[id] = kms;
                            }
                            kms.Add(km);
                        }

                        foreach (var km in kmInfos)
                        {
                            if(km.Victim.AllianceId > 0)
                            {
                                allianceAddOne(km.Victim.AllianceId, km);
                            }
                            if (km.Victim.CorporationId > 0)
                            {
                                corpAddOne(km.Victim.CorporationId, km);
                            }

                            if (km.Attackers.NotNullOrEmpty())
                            {
                                foreach (var attacker in km.Attackers)
                                {
                                    if (attacker.AllianceId > 0)
                                    {
                                        allianceAddOne(attacker.AllianceId, km);
                                    }
                                    if (attacker.CorporationId > 0)
                                        corpAddOne(attacker.CorporationId, km);
                                }
                            }
                        }

                        var topCorpStatistic = corpStatistic.OrderByDescending(p => p.Value.Count).Take(3);
                        var topAllianceStatistic = allianceStatistic.OrderByDescending(p => p.Value.Count).Take(3);
                        List<int> ids = topCorpStatistic.Select(p => p.Key).ToList();
                        ids.AddRange(topAllianceStatistic.Select(p=>p.Key).ToList());
                        var names = await IDNameService.GetByIdsAsync(ids);
                        names ??= new List<IdName>();
                        List<WormholeFactionStatistic> corpS = new List<WormholeFactionStatistic>();
                        foreach (var statistic in topCorpStatistic)
                        {
                            var name = names.FirstOrDefault(p => p.Id == statistic.Key);
                            name ??= new IdName(statistic.Key, statistic.Key.ToString(), IdName.CategoryEnum.Corporation);
                            corpS.Add(new WormholeFactionStatistic()
                            {
                                Name = name,
                                KMCount = statistic.Value.Count,
                                LastActiveDay = (DateTime.UtcNow - statistic.Value.Max(p => p.KillmailTime)).TotalDays,
                            });
                        }
                        CorpStatistics = corpS;

                        List<WormholeFactionStatistic> allianceS = new List<WormholeFactionStatistic>();
                        foreach (var statistic in topAllianceStatistic)
                        {
                            var name = names.FirstOrDefault(p => p.Id == statistic.Key);
                            name ??= new IdName(statistic.Key, statistic.Key.ToString(), IdName.CategoryEnum.Corporation);
                            allianceS.Add(new WormholeFactionStatistic()
                            {
                                Name = name,
                                KMCount = statistic.Value.Count,
                                LastActiveDay = (DateTime.UtcNow - statistic.Value.Max(p => p.KillmailTime)).TotalDays,
                            });
                        }

                        AllianceStatistics = allianceS;
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                ShowError(ex.Message);
            }
            finally
            {
                LoadingZKB = false;
            }
        }

        public ICommand RefreshZKBDataCommand => new RelayCommand(() =>
        {
            if(WormholeDetail != null)
                _ = AnalyzeKBAsync(WormholeDetail.Id);
        });
        public ICommand ZKBDetailCommand => new RelayCommand(async() =>
        {
            if (WormholeDetail != null)
                await ClientServiceHelper.GetRequiredService<KBNavigationService>().NavigationTo(WormholeDetail.Id, ZKB.NET.EntityType.SolarSystemID, WormholeDetail.Name);
        });
    }
}
