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
        private static WarningService Current
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
        
        public static void NotifyWindow(string listener, EarlyWarningContent content)
        {
            if(Current.WarningWindows.TryGetValue(listener,out var value))
            {
                value.Intel(content);
            }
        }
        public static IntelWindow AddNotifyWindow(Core.Models.EarlyWarningSetting setting, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            if (Current.WarningWindows.ContainsKey(setting.Listener))
            {
                return null;
            }
            else
            {
                IntelWindow intelWindow = new IntelWindow(setting, intelMap);
                Current.WarningWindows.Add(setting.Listener, intelWindow);
                return intelWindow;
            }
        }
        public static void ShowWindow(string listener)
        {
            if (Current.WarningWindows.TryGetValue(listener, out var value))
            {
                value.Show();
            }
        }
        public static void RemoveWindow(string listener)
        {
            if (listener != null && Current.WarningWindows.TryGetValue(listener, out var value))
            {
                value.Dispose();
                Current.WarningWindows.Remove(listener);
            }
        }
        public static void ClearWindow()
        {
            foreach(var item in Current.WarningWindows.Values)
            {
                item.Dispose();
            }
            Current.WarningWindows.Clear();
        }
        public static void UpdateWindowUI(string listener)
        {
            if (Current.WarningWindows.TryGetValue(listener, out var value))
            {
                value.UpdateUI();
            }
        }
        public static void UpdateWindowHome(string listener, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            if (Current.WarningWindows.TryGetValue(listener, out var value))
            {
                value.UpdateHome(intelMap);
            }
        }
        public static bool RestoreWindowPos(string listener)
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

        public static void NotifyToast(string listener, string chanel, EarlyWarningContent content)
        {
            Notifications.IntelToast.SendToast(listener, chanel, content);
        }

        private Dictionary<string, MediaSource> MediaSourceDic = new Dictionary<string, MediaSource>();
        private MediaSource DefaultMediaSource;
        private MediaPlayer mediaPlayer;
        private MediaPlayer MediaPlayer
        {
            get
            {
                if(mediaPlayer == null)
                {
                    mediaPlayer = new MediaPlayer();
                }
                return mediaPlayer;
            }
        }
        public static void NotifySound(string filepath)
        {
            Current.MediaPlayer.Pause();
            if (!string.IsNullOrEmpty(filepath))
            {
                if (Current.MediaSourceDic.TryGetValue(filepath,out var mediaSourc))
                {
                    Current.MediaPlayer.Source = mediaSourc;
                }
                else
                {
                    var m = MediaSource.CreateFromUri(new Uri(filepath));
                    if(m != null)
                    {
                        Current.MediaPlayer.Source = m;
                        Current.MediaSourceDic.Add(filepath,m);
                    }
                }
                Current.MediaPlayer.Play();
            }
            else
            {
                if(Current.DefaultMediaSource == null)
                {
                    Current.DefaultMediaSource = MediaSource.CreateFromUri(new Uri(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "default.mp3")));
                }
                if (Current.DefaultMediaSource != null)
                {
                    Current.MediaPlayer.Source = Current.DefaultMediaSource;
                    Current.MediaPlayer.Play();
                }
            }
        }

        public static void Dispose()
        {
            ClearWindow();
            foreach(var m in Current.MediaSourceDic.Values)
            {
                m.Dispose();
            }
            Current.MediaSourceDic.Clear();
            Current.DefaultMediaSource?.Dispose();
            Current.DefaultMediaSource = null;
            if(Current.mediaPlayer != null)
            {
                Current.mediaPlayer.Dispose();
            }
            Current.mediaPlayer = null;
        }
    }
}
