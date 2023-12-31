using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Services.Settings;

namespace TheGuideToTheNewEden.WinUI.ViewModels.Setting
{
    public class GameLogSettingViewModel : BaseViewModel
    {
        private int channelDuration = GameLogsSettingService.EVELogsChannelDurationValue;
        public int ChannelDuration
        {
            get => channelDuration;
            set
            {
                if (SetProperty(ref channelDuration, value))
                {
                    GameLogsSettingService.SetValue(GameLogsSettingService.GameLogKey.EVELogsChannelDuration, value.ToString());
                }
            }
        }
        private string evelogsPath = GameLogsSettingService.EVELogsPathValue;
        public string EvelogsPath
        {
            get => evelogsPath;
            set
            {
                if(SetProperty(ref evelogsPath, value))
                {
                    GameLogsSettingService.SetValue(GameLogsSettingService.GameLogKey.EVELogsPath, value);
                }
            }
        }

        private int maxShowItems = GameLogsSettingService.MaxShowItems;
        public int MaxShowItems
        {
            get => maxShowItems;
            set
            {
                if (SetProperty(ref maxShowItems, value))
                {
                    GameLogsSettingService.SetValue(GameLogsSettingService.GameLogKey.MaxShowItems, value.ToString());
                }
            }
        }
    }
}
