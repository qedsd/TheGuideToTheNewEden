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
    public class StatistTopValueViewModel : BaseViewModel
    {
        private Services.KBNavigationService _kbNavigationService;
        private EntityStatistic _statistic;
        public EntityStatistic Statistic { get => _statistic; set => SetProperty(ref _statistic, value); }
        
        private List<Core.Models.KB.KBItemInfo> _topKillAllTime;
        public List<Core.Models.KB.KBItemInfo> TopKillAllTime { get => _topKillAllTime; set => SetProperty(ref _topKillAllTime, value); }
        private List<Core.Models.KB.KBItemInfo> _topKill7d;
        public List<Core.Models.KB.KBItemInfo> TopKillA7d { get => _topKill7d; set => SetProperty(ref _topKill7d, value); }

        public StatistTopValueViewModel()
        {
        }
        public void SetData(EntityStatistic statistic, KBNavigationService kbNavigationService)
        {
            Statistic = statistic;
            _kbNavigationService = kbNavigationService;
            InitAsync();
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
        public async void InitAsync()
        {
            ShowWaiting();
            if (_statistic.TopIskKills.NotNullOrEmpty())
            {
                TopKillAllTime = await GetItemInfosAsync(_statistic.TopIskKills);
            }
            if (_statistic.TopIskKills7d.NotNullOrEmpty())
            {
                TopKillA7d = await GetItemInfosAsync(_statistic.TopIskKills);
            }
            HideWaiting();
        }
        private async Task<List<KBItemInfo>> GetItemInfosAsync(List<int> ids)
        {
            try
            {
                if (ids.NotNullOrEmpty())
                {
                    List<ZKillmaill> killmails = new List<ZKillmaill>();
                    foreach (var id in ids)
                    {
                        var km = await ZKB.NET.ZKB.GetKillmaillAsync(new ParamModifierData[]
                        {
                        new ParamModifierData(ParamModifier.KillID, id.ToString()),
                        });
                        if (km.NotNullOrEmpty())
                        {
                            killmails.Add(km.FirstOrDefault());
                        }
                    }
                    return await KBHelpers.CreateKBItemInfoAsync(killmails);
                }
                else
                {
                    return null;
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                ShowError(ex.Message);
                return null;
            }
        }
    }
}
