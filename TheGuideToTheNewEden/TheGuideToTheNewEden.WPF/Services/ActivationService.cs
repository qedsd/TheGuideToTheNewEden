using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WPF.Helpers;
using static Vanara.PInvoke.LANGID;

namespace TheGuideToTheNewEden.WPF.Services
{
    internal static class ActivationService
    {
        public static void Init()
        {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(GetSyncfusionLicense());
            AppThemeHelper.SetTheme(Core.Services.Settings.ThemeSelectorService.Theme);
            AppLanguageHelper.SetLanguage(Core.Services.LanguageSelectorService.Value);
        }
        private static string GetSyncfusionLicense()
        {
            string file = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "SyncfusionLicense.txt");
            if (System.IO.File.Exists(file))
            {
                return System.IO.File.ReadAllText(file);
            }
            else
            {
                //TODO:release
                return "";
            }
        }
    }
}
