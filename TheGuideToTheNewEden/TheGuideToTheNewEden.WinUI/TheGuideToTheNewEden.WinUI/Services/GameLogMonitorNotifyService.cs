using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Wins;
using Windows.Media.Core;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal class GameLogMonitorNotifyService
    {
        private static GameLogMonitorNotifyService current;
        private static GameLogMonitorNotifyService Current
        {
            get
            {
                if (current == null)
                {
                    current = new GameLogMonitorNotifyService();
                }
                return current;
            }
        }
        private Dictionary<int, MessageWindow> NotifyWindows = new Dictionary<int, MessageWindow>();
        private Dictionary<int, MediaSource> NotifyMediaSources = new Dictionary<int, MediaSource>();
        public bool Add(Core.Models.GameLogSetting setting)
        {
            if(setting.WindowNotify)
            {
                if(!NotifyWindows.TryAdd(setting.ListenerID, new MessageWindow()))
                {
                    Core.Log.Error($"添加相同ListenerID{setting.ListenerID}");
                    return false;
                }
            }
            if(setting.SoundNotify)
            {
                if(!NotifyMediaSources.TryAdd(setting.ListenerID, MediaSource.CreateFromUri(new Uri(setting.SoundFile))))
                {
                    Core.Log.Error($"添加相同ListenerID{setting.ListenerID}");
                    return false;
                }
            }
            return true;
        }
    }
}
