using Microsoft.UI.Xaml;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal static class LocalDbSelectorService
    {
        private const string Key = "LocalDbPath";
        public static string Value { get; set; }


        public static void Initialize()
        {
            Value = LoadFromSettings();
        }

        public static async Task SetAsync(string value)
        {
            Value = value;
            await SaveToSettingsAsync(value);
        }

        private static string LoadFromSettings()
        {
            return SettingService.GetValue(Key);
        }

        private static async Task SaveToSettingsAsync(string value)
        {
            await SettingService.SetValueAsync(Key, value);
        }

        /// <summary>
        /// 获取所有本地数据库
        /// </summary>
        /// <returns></returns>
        public static List<string> GetAll()
        {
            string localDbFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Database", "Local");
            var files = System.IO.Directory.GetFiles(localDbFolder);
            if (files.NotNullOrEmpty())
            {
                return files.Where(p => p.EndsWith(".db", StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else
            {
                return null;
            }
        }
    }
}
