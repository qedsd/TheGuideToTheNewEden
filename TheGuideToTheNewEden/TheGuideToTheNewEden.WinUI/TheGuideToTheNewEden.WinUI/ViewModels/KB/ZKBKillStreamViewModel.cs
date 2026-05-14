using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Notifications;
using TheGuideToTheNewEden.WinUI.Services;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WinUI.ViewModels.KB
{
    public class ZKBKillStreamViewModel : BaseViewModel
    {
        /// <summary>
        /// 筛选后的数据
        /// </summary>
        public ObservableCollection<Core.Models.KB.KBItemInfo> KBItemInfos { get; set; }
        /// <summary>
        /// 被过滤的数据
        /// </summary>
        public ObservableCollection<Core.Models.KB.KBItemInfo> FilteredOutKBItemInfos { get; set; }
        public ZKBStreamConfig Config { get; set; }
        private int _totalReceivedCount;
        public int TotalReceivedCount
        {
            get => _totalReceivedCount;
            set => SetProperty(ref _totalReceivedCount, value);
        }
        private int _passedCount;
        public int PassedCount
        {
            get => _passedCount;
            set => SetProperty(ref _passedCount, value);
        }
        private int _filteredCount;
        public int FilteredCount
        {
            get => _filteredCount;
            set => SetProperty(ref _filteredCount, value);
        }
        public ZKBKillStreamViewModel()
        {
            KBItemInfos = new ObservableCollection<Core.Models.KB.KBItemInfo>();
            FilteredOutKBItemInfos = new ObservableCollection<KBItemInfo>();
            Config = Services.Settings.ZKBSettingService.Setting;
            Config.EnsureRoleFiltersInitialized();
        }
        private Task _killStreamMessageThread;
        private CancellationTokenSource _cancellationTokenSource;
        private KBStreamCommonFilter _commonExclusionsFilter = new KBStreamCommonFilter() { EmptyAlwaysContains = false };
        private KBStreamCommonFilter _commonInclusionsFilter = new KBStreamCommonFilter() { EmptyAlwaysContains = true };
        private KBStreamRoleFilter _victimExclusionsFilter = new KBStreamRoleFilter() { EmptyAlwaysContains = false };
        private KBStreamRoleFilter _victimInclusionsFilter = new KBStreamRoleFilter() { EmptyAlwaysContains = true };
        private KBStreamRoleFilter _attackerExclusionsFilter = new KBStreamRoleFilter() { EmptyAlwaysContains = false };
        private KBStreamRoleFilter _attackerInclusionsFilter = new KBStreamRoleFilter() { EmptyAlwaysContains = true };
        public async Task<bool> InitAsync()
        {
            try
            {
                BuildFilters();
                await Core.Services.ZKBStreamService.Current.Sub();
                Core.Services.ZKBStreamService.Current.OnMessage += KillStream_OnMessage;
                Core.Services.ZKBStreamService.Current.OnError += KillStream_OnError;
                _killStreamMsgQueue ??= new ConcurrentQueue<SKBDetail>();
                _cancellationTokenSource ??= new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;
                //使用一个线程来执行查询KB具体信息，避免ESI查名字时数据库冲突
                _killStreamMessageThread ??= new Task(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (_killStreamMsgQueue.TryDequeue(out var detail))
                        {
                            var info = Core.Helpers.KBHelpers.CreateKBItemInfo(detail);
                            if (info != null)
                            {
                                //detail.Victim.AllianceId = 99003581;
                                bool pass = PassFilters(detail);
                                if (pass)
                                {
                                    ZKBToast.SendToast(info);
                                }
                                Window?.DispatcherQueue?.SafelyTryEnqueue(() =>
                                {
                                    TotalReceivedCount++;
                                    if (pass)
                                    {
                                        if(Config.SortWay == 0)
                                        {
                                            KBItemInfos.Insert(0, info);
                                        }
                                        else
                                        {
                                            int insertIndex = 0;
                                            for (int i = 0; i < KBItemInfos.Count; i++)
                                            {
                                                if (KBItemInfos[i].SKBDetail.KillmailTime > info.SKBDetail.KillmailTime)
                                                {
                                                    insertIndex = i + 1;
                                                }
                                                else
                                                {
                                                    break;
                                                }
                                            }
                                            KBItemInfos.Insert(insertIndex, info);
                                        }
                                        
                                        if (KBItemInfos.Count > Config.MaxKBItems)
                                        {
                                            KBItemInfos.RemoveAt(KBItemInfos.Count - 1);
                                        }
                                        PassedCount++;
                                    }
                                    else
                                    {
                                        if (Config.SortWay == 0)
                                        {
                                            FilteredOutKBItemInfos.Insert(0, info);
                                        }
                                        else
                                        {
                                            int insertIndex = 0;
                                            for (int i = 0; i < FilteredOutKBItemInfos.Count; i++)
                                            {
                                                if (FilteredOutKBItemInfos[i].SKBDetail.KillmailTime > info.SKBDetail.KillmailTime)
                                                {
                                                    insertIndex = i + 1;
                                                }
                                                else
                                                {
                                                    break;
                                                }
                                            }
                                            FilteredOutKBItemInfos.Insert(insertIndex, info);
                                        }
                                        if (FilteredOutKBItemInfos.Count > Config.MaxKBItems)
                                        {
                                            FilteredOutKBItemInfos.RemoveAt(FilteredOutKBItemInfos.Count - 1);
                                        }
                                        FilteredCount++;
                                    }
                                    
                                });
                            }
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                    }
                });
                if(_killStreamMessageThread.Status != TaskStatus.Running)
                {
                    _killStreamMessageThread.Start();
                }
                ShowSuccess(Helpers.ResourcesHelper.GetString("ZKBHomePage_Connected"));
                Services.Settings.ZKBSettingService.Save();
                return true;
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                ShowError(ex.Message);
                return false;
            }
        }
        public void Stop()
        {
            Core.Services.ZKBStreamService.Current.UnSub();
            Core.Services.ZKBStreamService.Current.OnMessage -= KillStream_OnMessage;
            Core.Services.ZKBStreamService.Current.OnError -= KillStream_OnError;
        }
        private void KillStream_OnError(object sender, Exception e)
        {
            Core.Log.Error(e);
            ShowError(e);
        }

        private ConcurrentQueue<SKBDetail> _killStreamMsgQueue;
        private void KillStream_OnMessage(object sender, SKBDetail detail, string sourceData)
        {
            _killStreamMsgQueue.Enqueue(detail);
        }

        public void Dispose()
        {
            Core.Services.ZKBStreamService.Current.UnSub();
            Core.Services.ZKBStreamService.Current.OnMessage -= KillStream_OnMessage;
            Core.Services.ZKBStreamService.Current.OnError -= KillStream_OnError;
            _cancellationTokenSource?.Cancel();
        }

        private void BuildFilters()
        {
            _commonExclusionsFilter.Clear();
            _commonInclusionsFilter.Clear();
            _victimExclusionsFilter.Clear();
            _victimInclusionsFilter.Clear();
            _attackerExclusionsFilter.Clear();
            _attackerInclusionsFilter.Clear();

            Config.EnsureRoleFiltersInitialized();
            _commonExclusionsFilter.Add(Config.CommonExclusions);
            _commonInclusionsFilter.Add(Config.CommonInclusions);
            _victimExclusionsFilter.Add(Config.VictimExclusions);
            _victimInclusionsFilter.Add(Config.VictimInclusions);
            _attackerExclusionsFilter.Add(Config.AttackerExclusions);
            _attackerInclusionsFilter.Add(Config.AttackerInclusions);
        }

        private bool PassFilters(SKBDetail detail)
        {
            if (detail?.Victim == null)
            {
                return false;
            }

            if (!PassCommonFilters(detail))
            {
                return false;
            }

            if (!PassVictimFilters(detail))
            {
                return false;
            }

            if (!PassAttackerFilters(detail))
            {
                return false;
            }

            return true;
        }

        private bool PassCommonFilters(SKBDetail detail)
        {
            if (_commonExclusionsFilter.Contains(IdName.CategoryEnum.SolarSystem, detail.SolarSystemId)
                || !_commonInclusionsFilter.Contains(IdName.CategoryEnum.SolarSystem, detail.SolarSystemId))
            {
                return false;
            }

            if (_commonExclusionsFilter.Contains(IdName.CategoryEnum.InventoryType, detail.Victim.ShipTypeId)
                || !_commonInclusionsFilter.Contains(IdName.CategoryEnum.InventoryType, detail.Victim.ShipTypeId))
            {
                return false;
            }

            var regionId = Core.Services.DB.MapSolarSystemService.Query(detail.SolarSystemId)?.RegionID ?? 0;
            if (_commonExclusionsFilter.Contains(IdName.CategoryEnum.Region, regionId)
                || !_commonInclusionsFilter.Contains(IdName.CategoryEnum.Region, regionId))
            {
                return false;
            }

            return true;
        }

        private bool PassVictimFilters(SKBDetail detail)
        {
            if (_victimExclusionsFilter.Contains(IdName.CategoryEnum.Character, detail.Victim.CharacterId)
                || !_victimInclusionsFilter.Contains(IdName.CategoryEnum.Character, detail.Victim.CharacterId))
            {
                return false;
            }

            if (_victimExclusionsFilter.Contains(IdName.CategoryEnum.Corporation, detail.Victim.CorporationId)
                || !_victimInclusionsFilter.Contains(IdName.CategoryEnum.Corporation, detail.Victim.CorporationId))
            {
                return false;
            }

            if (_victimExclusionsFilter.Contains(IdName.CategoryEnum.Alliance, detail.Victim.AllianceId)
                || !_victimInclusionsFilter.Contains(IdName.CategoryEnum.Alliance, detail.Victim.AllianceId))
            {
                return false;
            }

            return true;
        }

        private bool PassAttackerFilters(SKBDetail detail)
        {
            var attackerCharacterIds = detail.Attackers?.Select(p => p.CharacterId) ?? Enumerable.Empty<int>();
            if (_attackerExclusionsFilter.Contains(IdName.CategoryEnum.Character, attackerCharacterIds)
                || !_attackerInclusionsFilter.Contains(IdName.CategoryEnum.Character, attackerCharacterIds))
            {
                return false;
            }

            var attackerCorpIds = detail.Attackers?.Select(p => p.CorporationId) ?? Enumerable.Empty<int>();
            if (_attackerExclusionsFilter.Contains(IdName.CategoryEnum.Corporation, attackerCorpIds)
                || !_attackerInclusionsFilter.Contains(IdName.CategoryEnum.Corporation, attackerCorpIds))
            {
                return false;
            }

            var attackerAllianceIds = detail.Attackers?.Select(p => p.AllianceId) ?? Enumerable.Empty<int>();
            if (_attackerExclusionsFilter.Contains(IdName.CategoryEnum.Alliance, attackerAllianceIds)
                || !_attackerInclusionsFilter.Contains(IdName.CategoryEnum.Alliance, attackerAllianceIds))
            {
                return false;
            }

            return true;
        }

        public ICommand ClearListCommand => new RelayCommand(() =>
        {
            KBItemInfos.Clear();
            FilteredOutKBItemInfos.Clear();
            TotalReceivedCount = 0;
            FilteredCount = 0;
            PassedCount = 0;
        });
    }

    public class KBStreamCommonFilter
    {
        public HashSet<int> SolarSystems { get; set; } = new HashSet<int>();
        public HashSet<int> Regions { get; set; } = new HashSet<int>();
        public HashSet<int> Types { get; set; } = new HashSet<int>();

        public bool EmptyAlwaysContains { get; set; }

        public void Clear()
        {
            SolarSystems.Clear();
            Regions.Clear();
            Types.Clear();
        }

        public void Add(IEnumerable<IdName> idNames)
        {
            if (idNames == null)
            {
                return;
            }
            foreach (var idName in idNames)
            {
                switch (idName.GetCategory())
                {
                    case IdName.CategoryEnum.SolarSystem: SolarSystems.Add(idName.Id); break;
                    case IdName.CategoryEnum.Region: Regions.Add(idName.Id); break;
                    case IdName.CategoryEnum.InventoryType: Types.Add(idName.Id); break;
                }
            }
        }

        public bool Contains(IdName.CategoryEnum category, int id)
        {
            if (id <= 0)
            {
                return false;
            }
            switch (category)
            {
                case IdName.CategoryEnum.SolarSystem: return SolarSystems.Count == 0 ? EmptyAlwaysContains : SolarSystems.Contains(id);
                case IdName.CategoryEnum.Region: return Regions.Count == 0 ? EmptyAlwaysContains : Regions.Contains(id);
                case IdName.CategoryEnum.InventoryType: return Types.Count == 0 ? EmptyAlwaysContains : Types.Contains(id);
                default: return false;
            }
        }
    }

    public class KBStreamRoleFilter
    {
        public HashSet<int> Characters { get; set; } = new HashSet<int>();
        public HashSet<int> Corporations { get; set; } = new HashSet<int>();
        public HashSet<int> Alliances { get; set; } = new HashSet<int>();

        public bool EmptyAlwaysContains { get; set; }

        public void Clear()
        {
            Characters.Clear();
            Corporations.Clear();
            Alliances.Clear();
        }

        public void Add(IEnumerable<IdName> idNames)
        {
            if (idNames == null)
            {
                return;
            }
            foreach (var idName in idNames)
            {
                switch (idName.GetCategory())
                {
                    case IdName.CategoryEnum.Character: Characters.Add(idName.Id); break;
                    case IdName.CategoryEnum.Corporation: Corporations.Add(idName.Id); break;
                    case IdName.CategoryEnum.Alliance: Alliances.Add(idName.Id); break;
                }
            }
        }

        public bool Contains(IdName.CategoryEnum category, int id)
        {
            if (id <= 0)
            {
                return false;
            }
            switch (category)
            {
                case IdName.CategoryEnum.Character: return Characters.Count == 0 ? EmptyAlwaysContains : Characters.Contains(id);
                case IdName.CategoryEnum.Corporation: return Corporations.Count == 0 ? EmptyAlwaysContains : Corporations.Contains(id);
                case IdName.CategoryEnum.Alliance: return Alliances.Count == 0 ? EmptyAlwaysContains : Alliances.Contains(id);
                default: return false;
            }
        }

        public bool Contains(IdName.CategoryEnum category, IEnumerable<int> ids)
        {
            if (ids == null)
            {
                return false;
            }
            foreach (var id in ids)
            {
                if (Contains(category, id))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
