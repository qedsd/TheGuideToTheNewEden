using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal static class GameLogsSettingService
    {
        internal enum GameLogKey
        {
            EVELogsPath,
            EVELogsChannelDuration,
            MaxShowItems,
        }
        public static string EVELogsPathValue { get; set; }
        public static int EVELogsChannelDurationValue { get; set; }
        public static int MaxShowItems { get; set; }


        public static void Initialize()
        {
            EVELogsPathValue = SettingService.GetValue(GameLogKey.EVELogsPath.ToString());
            if(string.IsNullOrEmpty(EVELogsPathValue))
            {
                EVELogsPathValue = GetDefaultLogsPath();
            }

            if (int.TryParse(SettingService.GetValue(GameLogKey.EVELogsChannelDuration.ToString()), out int duration))
            {
                EVELogsChannelDurationValue = duration;
            }
            else
            {
                EVELogsChannelDurationValue = 7;
            }

            if (int.TryParse(SettingService.GetValue(GameLogKey.MaxShowItems.ToString()), out int maxShowItems))
            {
                MaxShowItems = maxShowItems;
            }
            else
            {
                MaxShowItems = 100;
            }
        }

        public static void SetValue(GameLogKey key, string value)
        {
            switch(key)
            {
                case GameLogKey.EVELogsPath: EVELogsPathValue = value; break;
                case GameLogKey.EVELogsChannelDuration:
                    {
                        if(int.TryParse(value, out int v))
                        {
                            EVELogsChannelDurationValue = v;
                        }
                        else
                        {
                            Core.Log.Error($"Set {key} invalid data type of {value}");
                        }
                        break;
                    }
                case GameLogKey.MaxShowItems:
                    {
                        if (int.TryParse(value, out int v))
                        {
                            MaxShowItems = v;
                        }
                        else
                        {
                            Core.Log.Error($"Set {key} invalid data type of {value}");
                        }
                        break;
                    }
            }
            SettingService.SetValue(key.ToString(), value);
        }

        public static string GetDefaultLogsPath()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "logs");
        }

        /// <summary>
        /// 获取聊天频道日志文件夹路径
        /// </summary>
        /// <returns></returns>
        public static string GetChatlogsPath()
        {
            return System.IO.Path.Combine(EVELogsPathValue, "Chatlogs");
        }
        /// <summary>
        /// 获取游戏日志文件夹路径
        /// </summary>
        /// <returns></returns>
        public static string GetGamelogsPath()
        {
            return System.IO.Path.Combine(EVELogsPathValue, "Gamelogs");
        }
    }
    
}
