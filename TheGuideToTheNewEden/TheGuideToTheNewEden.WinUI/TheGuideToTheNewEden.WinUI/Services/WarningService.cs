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
        /// 一个角色绑定一个MediaSource
        /// </summary>
        private Dictionary<string, MediaPlayer> MediaPlayers = new Dictionary<string, MediaPlayer>();
        /// <summary>
        /// 一个音源绑定一个MediaSource
        /// </summary>
        private Dictionary<string, MediaSource> MediaSources = new Dictionary<string, MediaSource>();
        private MediaSource defaultMediaSource;
        private MediaSource DefaultMediaSource
        {
            get
            {
                if(defaultMediaSource == null)
                {
                    defaultMediaSource = MediaSource.CreateFromUri(new Uri(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "default.mp3")));
                }
                return defaultMediaSource;
            }
        }
        public bool Add(Core.Models.EarlyWarningSetting setting, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            if(setting.OverlapType != 2)//初始化小窗
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
                if (MediaPlayers.ContainsKey(setting.Listener))
                {
                    Core.Log.Error("存在相同角色名称MediaPlayer");
                    return false;
                }
                else
                {
                    MediaPlayer mediaPlayer = new MediaPlayer()
                    {
                        Source = DefaultMediaSource
                    };
                    MediaPlayers.Add(setting.Listener, mediaPlayer);
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
                if (MediaPlayers.TryGetValue(listener, out var mediaPlayer))
                {
                    mediaPlayer.Dispose();
                }
                MediaPlayers.Remove(listener);
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
        public bool Notify(string listener,string soundFile, bool sendToast, string chanel, EarlyWarningContent content)
        {
            try
            {
                if (WarningWindows.TryGetValue(listener, out var value))
                {
                    value.Show();
                    value.Intel(content);
                }
                if (MediaPlayers.TryGetValue(listener, out var mediaPlayer))
                {
                    if (string.IsNullOrEmpty(soundFile))
                    {
                        mediaPlayer.Source = DefaultMediaSource;
                    }
                    else
                    {
                        if (MediaSources.TryGetValue(soundFile, out var mediaSource))
                        {
                            mediaPlayer.Source = mediaSource;
                        }
                        else
                        {
                            var m = MediaSource.CreateFromUri(new Uri(soundFile));
                            if (m != null)
                            {
                                MediaSources.Add(soundFile, m);
                                mediaPlayer.Source = m;
                            }
                            else
                            {
                                Core.Log.Error($"创建{soundFile}MediaSource失败");
                                mediaPlayer.Source = mediaSource;
                            }
                        }
                    }
                    mediaPlayer.Pause();
                    mediaPlayer.Position = TimeSpan.Zero;
                    mediaPlayer.Play();
                }
                if(sendToast)
                {
                    Notifications.IntelToast.SendToast(listener, chanel, content);
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

        public void Dispose()
        {
            ClearWindow();
            foreach(var player in MediaPlayers.Values)
            {
                player.Pause();
                player.Dispose();
            }
            MediaPlayers.Clear();
            foreach (var m in MediaSources.Values)
            {
                m.Dispose();
            }
            MediaSources.Clear();
            DefaultMediaSource?.Dispose();
            defaultMediaSource = null;
        }
    }
}
