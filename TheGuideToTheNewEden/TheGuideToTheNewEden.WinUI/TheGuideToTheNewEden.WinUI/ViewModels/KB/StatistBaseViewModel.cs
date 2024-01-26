using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Services;
using ZKB.NET.Models.Statistics;

namespace TheGuideToTheNewEden.WinUI.ViewModels.KB
{
    public abstract class StatistBaseViewModel : BaseViewModel
    {
        internal Services.KBNavigationService _kbNavigationService;
        private EntityStatistic _statistic;
        public EntityStatistic Statistic { get => _statistic; set => SetProperty(ref _statistic, value); }

        public void SetData(EntityStatistic statistic, KBNavigationService kbNavigationService)
        {
            Statistic = statistic;
            _kbNavigationService = kbNavigationService;
        }
        private Action _showWaitingAction;
        private Action _hideWaitingAction;
        public void SetWaitingAction(Action show, Action hide)
        {
            _showWaitingAction = show;
            _hideWaitingAction = hide;
        }
        internal new void ShowWaiting()
        {
            _showWaitingAction?.Invoke();
        }
        internal new void HideWaiting()
        {
            _hideWaitingAction?.Invoke();
        }

        public abstract Task InitAsync();
    }
}
