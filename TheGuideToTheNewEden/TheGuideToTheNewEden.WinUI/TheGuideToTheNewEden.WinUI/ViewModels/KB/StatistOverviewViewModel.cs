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
    public class StatistOverviewViewModel : BaseViewModel
    {
        private ParamModifier _paramModifier;
        private Services.KBNavigationService _kbNavigationService;
        private EntityStatistic _statistic;
        public EntityStatistic Statistic { get => _statistic; set => SetProperty(ref _statistic, value); }
        private ObservableCollection<Core.Models.KB.KBItemInfo> kbItemInfos;
        public ObservableCollection<Core.Models.KB.KBItemInfo> KBItemInfos { get => kbItemInfos; set => SetProperty(ref kbItemInfos, value); }
        private int _page;
        public int Page
        {
            get => _page;
            set
            {
                if(SetProperty(ref _page, value))
                {
                    LoadPageData(value);
                }
            }
        }

        public StatistOverviewViewModel()
        {
        }
        public void SetData(EntityStatistic statistic, KBNavigationService kbNavigationService)
        {
            Statistic = statistic;
            _kbNavigationService = kbNavigationService;
            switch(_statistic.StatisticType)
            {
                case EntityStatisticType.Character: _paramModifier = ParamModifier.CharacterID;break;
                case EntityStatisticType.Corporation: _paramModifier = ParamModifier.CorporationID; break;
                case EntityStatisticType.Alliance: _paramModifier = ParamModifier.AllianceID; break;
                case EntityStatisticType.Faction: _paramModifier = ParamModifier.FactionID; break;
                case EntityStatisticType.ShipType: _paramModifier = ParamModifier.ShipTypeID; break;
                case EntityStatisticType.Group: _paramModifier = ParamModifier.GroupID; break;
                case EntityStatisticType.SolarSystem: _paramModifier = ParamModifier.SystemID; break;
                case EntityStatisticType.Region: _paramModifier = ParamModifier.RegionID; break;
            }
        }
        private Action _showWaitingAction;
        private Action _hideWaitingAction;
        public void SetWaitingAction(Action show, Action hide)
        {
            _showWaitingAction = show;
            _hideWaitingAction = hide;
        }
        private new void ShowWaiting()
        {
            _showWaitingAction?.Invoke();
        }
        private new void HideWaiting()
        {
            _hideWaitingAction?.Invoke();
        }
        public async Task InitAsync()
        {
            ShowWaiting();
            var kbList = await GetItemInfosAsync(1);
            HideWaiting();
            if(kbList.NotNullOrEmpty())
            {
                KBItemInfos = kbList.ToObservableCollection();
            }
            else
            {
                KBItemInfos = new ObservableCollection<KBItemInfo>();
            }
            if (_statistic.TopIskKills.NotNullOrEmpty())
            {
                var list = GetItemInfosAsync(_statistic.TopIskKills);
            }
        }
        private async Task<List<KBItemInfo>> GetItemInfosAsync(List<int> ids)
        {
            if (ids.NotNullOrEmpty())
            {
                ZKillmaill getZKillmail(int id)
                {
                    return ZKB.NET.ZKB.GetKillmaillAsync(new ParamModifierData[]
                    {
                        new ParamModifierData(ParamModifier.KillID, id.ToString()),
                    }).Result?.FirstOrDefault();
                }
                var killmails = await Core.Helpers.ThreadHelper.RunAsync(ids, getZKillmail);
                return await KBHelpers.CreateKBItemInfoAsync(killmails.ToList());
            }
            else
            {
                return null;
            }
        }

        private async Task<List<KBItemInfo>> GetItemInfosAsync(int page)
        {
            try
            {
                var killmails = await Helpers.ZKBHelper.GetKillmaillAsync(new List<ParamModifierData>
                    {
                        new ParamModifierData(_paramModifier, _statistic.Id.ToString()),
                        new ParamModifierData(ParamModifier.Page, page.ToString()),
                    },page);
                return await KBHelpers.CreateKBItemInfoAsync(killmails);
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                Window?.ShowError(ex.Message);
                return null;
            }
        }

        private async void LoadPageData(int page)
        {
            if(KBItemInfos == null)
            {
                return;
            }
            KBItemInfos.Clear();
            ShowWaiting();
            var kbList = await GetItemInfosAsync(page);
            HideWaiting() ;
            if (kbList.NotNullOrEmpty())
            {
                foreach(var kb in kbList)
                {
                    KBItemInfos.Add(kb);
                }
            }
        }
    }
}
