using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Wins;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.Streaming.Adaptive;

namespace TheGuideToTheNewEden.WinUI.Services
{
    /// <summary>
    ///管理预警通知
    /// </summary>
    internal class WarningService
    {
        private static WarningService current;
        public static WarningService Current
        {
            get
            {
                if(current == null)
                {
                    current = new WarningService();
                }
                return current; 
            }
        }
        /// <summary>
        /// 一个角色绑定一个通知窗口
        /// </summary>
        private Dictionary<string, IntelWindow> WarningWindows = new Dictionary<string, IntelWindow>();
        /// <summary>
        /// 一个角色绑定一个
        /// </summary>
        private Dictionary<string, SoundNotifyItem> SoundNotifyItems = new Dictionary<string, SoundNotifyItem>();
        public bool Add(Core.Models.ChannelIntel.ChannelIntelSetting setting, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            if(setting.OverlapNotify)//初始化小窗
            {
                if (WarningWindows.ContainsKey(setting.Listener))
                {
                    Core.Log.Error("存在相同角色名称WarningWindow");
                    return false;
                }
                else
                {
                    IntelWindow intelWindow = new IntelWindow(setting, intelMap);
                    WarningWindows.Add(setting.Listener, intelWindow);
                    if(setting.OverlapType == 0)
                    {
                        intelWindow.Show();
                    }
                }
            }
            if(setting.MakeSound)
            {
                if (SoundNotifyItems.ContainsKey(setting.Listener))
                {
                    Core.Log.Error("存在相同角色名称SoundNotifyItem");
                    return false;
                }
                else
                {
                    SoundNotifyItems.Add(setting.Listener, new SoundNotifyItem(setting.Listener));
                }
            }
            return true;
        }
        public bool Remove(string listener)
        {
            if(!string.IsNullOrEmpty(listener))
            {
                if(WarningWindows.TryGetValue(listener, out var window))
                {
                    window.Dispose();
                }
                WarningWindows.Remove(listener);
                if (SoundNotifyItems.TryGetValue(listener, out var item))
                {
                    item.Dispose();
                }
                SoundNotifyItems.Remove(listener);
            }
            return true;
        }
        public IntelWindow GetIntelWindow(string listener)
        {
            if(WarningWindows.TryGetValue(listener, out var value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }
        private object _notifyLocker = new object();
        public bool Notify(string listener, Core.Models.ChannelIntel.ChannelIntelSoundSetting soundSetting, bool sendToast, string chanel, EarlyWarningContent content)
        {
            try
            {
                lock(_notifyLocker)
                {
                    if (WarningWindows.TryGetValue(listener, out var value))
                    {
                        value.Show();
                        value.Intel(content);
                    }
                    if (SoundNotifyItems.TryGetValue(listener, out var soundNotifyItem))
                    {
                        soundNotifyItem.PlaySound(soundSetting);
                    }
                    if (sendToast)
                    {
                        Notifications.IntelToast.SendToast(listener, chanel, content);
                    }
                }
                return true;
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                return false;
            }
        }
        public void ClearWindow()
        {
            foreach(var item in Current.WarningWindows.Values)
            {
                item.Dispose();
            }
            Current.WarningWindows.Clear();
        }
        public void UpdateWindowUI(string listener)
        {
            if (Current.WarningWindows.TryGetValue(listener, out var value))
            {
                value.UpdateUI();
            }
        }
        public void UpdateWindowHome(string listener, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            if (Current.WarningWindows.TryGetValue(listener, out var value))
            {
                value.UpdateHome(intelMap);
            }
        }
        public bool RestoreWindowPos(string listener)
        {
            if (Current.WarningWindows.TryGetValue(listener, out var value))
            {
                value.RestoreWindowPos();
                return true;
            }
            else
            {
                return false;
            }
        }
        public void StopSound(string listener)
        {
            if (!string.IsNullOrEmpty(listener) && SoundNotifyItems.TryGetValue(listener, out var soundNotifyItem))
            {
                soundNotifyItem.StopSound();
            }
        }
        public void Dispose()
        {
            ClearWindow();
            foreach(var player in SoundNotifyItems.Values)
            {
                player.Dispose();
            }
            SoundNotifyItems.Clear();
        }
    }

    class SoundNotifyItem
    {
        private string defaultMediaFile;
        private string DefaultMediaFile
        {
            get
            {
                if (defaultMediaFile == null)
                {
                    defaultMediaFile = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "default.mp3");
                }
                return defaultMediaFile;
            }
        }
        public string Listener { get; set; }
        private Dictionary<string, MediaPlayer> MediaPlayers = new Dictionary<string, MediaPlayer>();
        public SoundNotifyItem(string listener)
        {
            Listener = listener;
        }
        public void PlaySound(Core.Models.ChannelIntel.ChannelIntelSoundSetting soundSetting)
        {
            string soundFile = string.IsNullOrEmpty(soundSetting.FilePath) ? DefaultMediaFile : soundSetting.FilePath;
            MediaPlayer mediaPlayer;
            if (!MediaPlayers.TryGetValue(soundFile, out mediaPlayer))
            {
                mediaPlayer = new MediaPlayer()
                {
                    Source = MediaSource.CreateFromUri(new Uri(soundFile))
                };
                MediaPlayers.Add(soundFile, mediaPlayer);
            }
            mediaPlayer.Volume = (soundSetting?.Volume ?? 100) / 100.0;
            mediaPlayer.IsLoopingEnabled = soundSetting?.Loop ?? false;
            mediaPlayer.Pause();
            mediaPlayer.Position = TimeSpan.Zero;
            mediaPlayer.Play();
        }
        public void StopSound()
        {
            foreach(var player in MediaPlayers.Values)
            {
                player.Pause();
                player.Position = TimeSpan.Zero;
            }
        }
        public void Dispose()
        {
            foreach (var player in MediaPlayers.Values)
            {
                player.Pause();
                player.Dispose();
            }
        }
    }
}
