using ESI.NET.Models.PlanetaryInteraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Notifications;
using TheGuideToTheNewEden.WinUI.Wins;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal class GameLogMonitorNotifyService
    {
        private static GameLogMonitorNotifyService current;
        public static GameLogMonitorNotifyService Current
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
        private Dictionary<int, GaemLogMsgWindow> NotifyWindows = new Dictionary<int, GaemLogMsgWindow>();
        private Dictionary<int, MediaPlayer> NotifyMediaPlayers = new Dictionary<int, MediaPlayer>();
        public bool Add(Core.Models.GameLogInfo info, Core.Models.GameLogSetting setting, string title)
        {
            if(setting.WindowNotify)
            {
                if(!NotifyWindows.ContainsKey(setting.ListenerID))
                {
                    GaemLogMsgWindow messageWindow = new GaemLogMsgWindow(info.ListenerID, info.ListenerName)
                    {
                        Tag = setting.ListenerID
                    };
                    messageWindow.SetTitle($"{Helpers.ResourcesHelper.GetString("ShellPage_GameLogMonitor")} - {title}");
                    messageWindow.OnHided += MessageWindow_OnHided;
                    messageWindow.OnShowGameButtonClick += MessageWindow_OnShowGameButtonClick;
                    NotifyWindows.Add(setting.ListenerID, messageWindow);
                }
                else
                {
                    Core.Log.Error($"添加相同ListenerID{setting.ListenerID}");
                    return false;
                }
            }
            if(setting.SoundNotify)
            {
                if (!NotifyMediaPlayers.TryAdd(setting.ListenerID, new MediaPlayer()
                {
                    Source = MediaSource.CreateFromUri(new Uri(string.IsNullOrEmpty(setting.SoundFile) ?
                    System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "default.mp3") :
                    setting.SoundFile)),
                }))
                {
                    Core.Log.Error($"添加相同ListenerID{setting.ListenerID}");
                    NotifyWindows.Remove(setting.ListenerID);
                    return false;
                }
            }
            return true;
        }

        private void MessageWindow_OnShowGameButtonClick(GaemLogMsgWindow gaemLogMsgWindow)
        {
            var hwnd = Helpers.WindowHelper.GetGameHwndByCharacterName(gaemLogMsgWindow.ListenerName);
            if (hwnd != IntPtr.Zero)
            {
                Helpers.WindowHelper.SetForegroundWindow_Click(hwnd);
                Stop(gaemLogMsgWindow.ListenerID);
            }
        }

        private void MessageWindow_OnHided(GaemLogMsgWindow messageWindow)
        {
            if(NotifyMediaPlayers.TryGetValue((int)(messageWindow.Tag),out var mediaPlayer))
            {
                mediaPlayer.Pause();
            }
        }

        public void Remove(int id)
        {
            if(NotifyWindows.TryGetValue(id,out var messageWindow))
            {
                messageWindow.Close();
                NotifyWindows.Remove(id);
            }
            if(NotifyMediaPlayers.TryGetValue(id, out var mediaPlayer))
            {
                (mediaPlayer.Source as MediaSource).Dispose();
                mediaPlayer.Dispose();
                NotifyMediaPlayers.Remove(id);
            }
        }

        public void Notify(GameLogItem gameLog, string content)
        {
            if(NotifyWindows.TryGetValue(gameLog.Info.ListenerID, out var messageWindow))
            {
                messageWindow.Show(content);
            }
            if(NotifyMediaPlayers.TryGetValue(gameLog.Info.ListenerID, out var mediaPlayer))
            {
                mediaPlayer.Pause();
                mediaPlayer.Play();
            }
            if(gameLog.Setting.SystemNotify)
            {
                GameLogMonitorToast.SendToast(gameLog.Info.ListenerID, gameLog.Info.ListenerName, content);
            }
        }
        public void Stop(int id)
        {
            if (NotifyWindows.TryGetValue(id, out var messageWindow))
            {
                messageWindow.Hide();
            }
            if (NotifyMediaPlayers.TryGetValue(id, out var mediaPlayer))
            {
                mediaPlayer.Pause();
            }
        }
    }
}
