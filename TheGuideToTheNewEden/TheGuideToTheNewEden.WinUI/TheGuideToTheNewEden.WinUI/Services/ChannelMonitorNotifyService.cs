using ESI.NET.Models.PlanetaryInteraction;
using Microsoft.Windows.AppNotifications;
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
    internal class ChannelMonitorNotifyService
    {
        private static ChannelMonitorNotifyService current;
        public static ChannelMonitorNotifyService Current
        {
            get
            {
                if (current == null)
                {
                    current = new ChannelMonitorNotifyService();
                }
                return current;
            }
        }
        private Dictionary<string, GaemLogMsgWindow> NotifyWindows = new Dictionary<string, GaemLogMsgWindow>();
        private Dictionary<string, MediaPlayer> NotifyMediaPlayers = new Dictionary<string, MediaPlayer>();
        public bool Add(Core.Models.ChannelMonitorItem info)
        {
            if (info.Setting.WindowNotify)
            {
                if (!NotifyWindows.ContainsKey(info.Name))
                {
                    GaemLogMsgWindow messageWindow = new GaemLogMsgWindow(info.Name, info.Name);
                    messageWindow.SetTitle($"{Helpers.ResourcesHelper.GetString("ShellPage_ChannelMonitor")} - {info.Name}");
                    messageWindow.OnHided += MessageWindow_OnHided;
                    messageWindow.OnShowGameButtonClick += MessageWindow_OnShowGameButtonClick;
                    NotifyWindows.Add(info.Name, messageWindow);
                }
                else
                {
                    Core.Log.Error($"添加相同Name{info.Name}");
                    return false;
                }
            }
            if (info.Setting.SoundNotify)
            {
                if (!NotifyMediaPlayers.TryAdd(info.Name, new MediaPlayer()
                {
                    Source = MediaSource.CreateFromUri(new Uri(string.IsNullOrEmpty(info.Setting.SoundFile) ?
                    System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "default.mp3") :
                    info.Setting.SoundFile)),
                    IsLoopingEnabled = info.Setting.RepeatSound
                }))
                {
                    Core.Log.Error($"添加相同Name {info.Name}");
                    NotifyWindows.Remove(info.Setting.Name);
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
            }
            Stop(gaemLogMsgWindow.Tag.ToString());
        }

        private void MessageWindow_OnHided(GaemLogMsgWindow messageWindow)
        {
            if (NotifyMediaPlayers.TryGetValue((string)messageWindow.Tag, out var mediaPlayer))
            {
                mediaPlayer.Pause();
                mediaPlayer.Position = TimeSpan.Zero;
            }
            _ = AppNotificationManager.Default.RemoveByGroupAsync(messageWindow.Tag.ToString());
        }

        public void Remove(string name)
        {
            if (NotifyWindows.TryGetValue(name, out var messageWindow))
            {
                messageWindow.Close();
                NotifyWindows.Remove(name);
            }
            if (NotifyMediaPlayers.TryGetValue(name, out var mediaPlayer))
            {
                (mediaPlayer.Source as MediaSource).Dispose();
                mediaPlayer.Dispose();
                NotifyMediaPlayers.Remove(name);
            }
        }

        public void Notify(Core.Models.ChannelMonitorItem info, string content)
        {
            Helpers.WindowHelper.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (NotifyWindows.TryGetValue(info.Name, out var messageWindow))
                {
                    messageWindow.Show(content);
                }
                if (NotifyMediaPlayers.TryGetValue(info.Name, out var mediaPlayer))
                {
                    mediaPlayer.Pause();
                    mediaPlayer.Position = TimeSpan.Zero;
                    mediaPlayer.Play();
                }
                if (info.Setting.SystemNotify)
                {
                    ChannelMonitorToast.SendToast(info.Name, content);
                }
            });
        }
        public void Stop(string name)
        {
            if (NotifyWindows.TryGetValue(name, out var messageWindow))
            {
                messageWindow.Hide();
            }
            if (NotifyMediaPlayers.TryGetValue(name, out var mediaPlayer))
            {
                mediaPlayer.Pause();
                mediaPlayer.Position = TimeSpan.Zero;
            }
            _ = AppNotificationManager.Default.RemoveByGroupAsync(name);
        }
    }
}
