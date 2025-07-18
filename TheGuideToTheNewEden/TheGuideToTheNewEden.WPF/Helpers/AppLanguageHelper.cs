using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TheGuideToTheNewEden.WPF.Helpers
{
    internal static class AppLanguageHelper
    {
        public static void SetLanguage(Core.Services.LanguageSelectorService.AppLanguage language)
        {
            switch (language)
            {
                case Core.Services.LanguageSelectorService.AppLanguage.Chinese:
                    {
                        var res = Application.Current.Resources.MergedDictionaries.FirstOrDefault(p => p.Source.ToString() == "/Resources/Languages/en-US.xaml");
                        if (res != null)
                        {
                            Application.Current.Resources.MergedDictionaries.Remove(res);
                            ResourceDictionary newRes = new ResourceDictionary();
                            newRes.Source = new Uri("/Resources/Languages/zh-CN.xaml", UriKind.Relative);
                            Application.Current.Resources.MergedDictionaries.Add(newRes);
                        }
                    }
                    break;
                case Core.Services.LanguageSelectorService.AppLanguage.English:
                    {
                        var res = Application.Current.Resources.MergedDictionaries.FirstOrDefault(p => p.Source.ToString() == "/Resources/Languages/zh-CN.xaml");
                        if (res != null)
                        {
                            Application.Current.Resources.MergedDictionaries.Remove(res);
                            ResourceDictionary newRes = new ResourceDictionary();
                            newRes.Source = new Uri("/Resources/Languages/en-US.xaml", UriKind.Relative);
                            Application.Current.Resources.MergedDictionaries.Add(newRes);
                        }
                    }
                    break;
            }
        }
    }
}
