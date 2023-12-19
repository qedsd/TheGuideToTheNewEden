using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal static class GameLogsSettingService
    {
        public const string EVELogsPathKey = "EVELogsPath";
        public static string EVELogsPathValue { get; set; }

        public const string EVELogsChannelDurationKey = "EVELogsChannelDuration";
        public static int EVELogsChannelDurationValue { get; set; }


        public static void Initialize()
        {
            EVELogsPathValue = SettingService.GetValue(EVELogsPathKey);
            if(string.IsNullOrEmpty(EVELogsPathValue))
            {
                EVELogsPathValue = GetDefaultLogsPath();
            }
            if (int.TryParse(SettingService.GetValue(EVELogsChannelDurationKey), out int duration))
            {
                EVELogsChannelDurationValue = duration;
            }
            else
            {
                EVELogsChannelDurationValue = 7;
            }
        }

        public static void SetValue(string key, string value)
        {
            switch(key)
            {
                case EVELogsPathKey: EVELogsPathValue = value; break;
                case EVELogsChannelDurationKey:
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
            }
            SettingService.SetValue(key, value);
        }

        public static string GetDefaultLogsPath()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "logs");
        }
    }
}
