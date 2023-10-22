using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Services.Settings;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class GameLogMonitorViewModel : BaseViewModel
    {
        private readonly string _logPath = System.IO.Path.Combine(EVELogsPathSelectorService.Value, "Gamelogs");
        public ObservableCollection<GameLogInfo> GameLogInfos { get; } = new ObservableCollection<GameLogInfo>();
        private GameLogInfo _selectedGameLogInfo;
        public GameLogInfo SelectedGameLogInfo
        {
            get => _selectedGameLogInfo;
            set
            {
                if(SetProperty(ref _selectedGameLogInfo, value))
                {

                }
            }
        }
        internal GameLogMonitorViewModel()
        {
            InitAsync();
        }

        private async void InitAsync()
        {
            GameLogInfos.Clear();
            if (System.IO.Directory.Exists(_logPath))
            {
                ShowWaiting();
                var infos = await Task.Run(() => GameLogHelper.GetLatestGameLogInfos(_logPath));
                HideWaiting();
                if (infos != null)
                {
                    foreach (var item in infos)
                    {
                        GameLogInfos.Add(item);
                    }
                }
                SelectedGameLogInfo = null;
            }
        }
    }
}
