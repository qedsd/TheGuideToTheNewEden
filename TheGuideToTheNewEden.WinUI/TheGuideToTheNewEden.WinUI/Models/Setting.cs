using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TheGuideToTheNewEden.WinUI.Enums;

namespace TheGuideToTheNewEden.WinUI.Models
{
    public class Setting
    {
        public static Setting Instance { get; private set; } = new Setting();
        /// <summary>
        /// 亮 暗 自动
        /// </summary>
        public ThemeModeEnum Theme { get; set; } = ThemeModeEnum.Auto;
        /// <summary>
        /// true 程序自定义亮暗
        /// false 跟随系统
        /// </summary>
        [XmlIgnore]
        public bool IsRequestedTheme
        {
            get => Theme != ThemeModeEnum.Auto;
        }
        public LanguageEnum UILanguage { get; set; } = LanguageEnum.Chinese;
        public LanguageEnum DBLanguage { get; set; } = LanguageEnum.Chinese;

        public string UILanguageStr
        {
            get
            {
                switch(UILanguage)
                {
                    case LanguageEnum.Chinese:return "zh-CN";
                    case LanguageEnum.English: return "en-US";
                    default:return "zh-CN";
                }
            }
        }

        private static string savedPath;
        private static string SavedPath
        {
            get
            {
                if(savedPath == null)
                {
                    savedPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs","setting.xml");
                }
                return savedPath;
            }
        }
        public static void Save()
        {
            Utils.XmlUtil.SerializeToFile(Instance, SavedPath);
        }
        public static void Load()
        {
            if(System.IO.File.Exists(SavedPath))
            {
                try
                {
                    Instance = Utils.XmlUtil.DeserializeFromFile<Setting>(SavedPath);
                }
                catch(Exception)
                {
                    Instance = new Setting();
                }
            }
            else
            {
                Instance = new Setting();
            }
        }
    }
}
