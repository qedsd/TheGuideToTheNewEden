using ESI.NET.Models.SSO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class OfflineMonitorViewModel:BaseViewModel
    {
        public ObservableCollection<AuthorizedCharacterData> Characters { get; set; } = Services.CharacterService.CharacterOauths;
        public ObservableCollection<AuthorizedCharacterData> SelectedCharacters = new ObservableCollection<AuthorizedCharacterData>();
        private bool isRunning;
        /// <summary>
        /// 是否监控中
        /// </summary>
        public bool IsRunning
        {
            get => isRunning;
            set => SetProperty(ref isRunning, value);
        }

        private OfflineMonitorSetting setting;
        public OfflineMonitorSetting Setting
        {
            get => setting;
            set => SetProperty(ref setting, value);
        }
    }
}
