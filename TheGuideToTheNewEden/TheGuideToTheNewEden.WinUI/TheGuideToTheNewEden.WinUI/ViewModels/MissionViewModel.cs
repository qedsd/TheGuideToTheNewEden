using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class MissionViewModel:BaseViewModel
    {
        private List<Mission> missions;
        public List<Mission> Missions
        {
            get => missions;
            set => SetProperty(ref missions, value);
        }
        private Mission selectedMission;
        public Mission SelectedMission
        {
            get => selectedMission;
            set
            {
                if (SetProperty(ref selectedMission, value))
                {
                    MissionContent = value == null ? null : MissionService.QueryMissionContent(value.Id);
                }
            }
        }
        private Mission selectedListMission;
        public Mission SelectedListMission
        {
            get => selectedListMission;
            set
            {
                if (SetProperty(ref selectedListMission, value))
                {
                    SelectedMission = value;
                }
            }
        }
        private int level;
        /// <summary>
        /// 等级需要+1
        /// </summary>
        public int Level
        {
            get => level;
            set
            {
                if (SetProperty(ref level, value))
                {
                    Load();
                }
            }
        }

        private MissionContent missionContent;
        public MissionContent MissionContent
        {
            get => missionContent;
            set
            {
                if (SetProperty(ref missionContent, value))
                {
                    
                }
            }
        }
        public MissionViewModel()
        {
            Load();
        }
        private async void Load()
        {
            Missions = await MissionService.QueryMissionAsync(Level + 1);
        }
    }
}
