using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TheGuideToTheNewEden.Core.Services.Settings.ThemeSelectorService;
using Syncfusion.SfSkinManager;
using System.Windows;

namespace TheGuideToTheNewEden.WPF.Helpers
{
    internal static class AppThemeHelper
    {
        public static void SetTheme(ElementTheme theme)
        {
            switch (theme)
            {
                case ElementTheme.Light:
                    {
                        var res = Application.Current.Resources.MergedDictionaries.FirstOrDefault(p => p.Source.ToString() == "/Resources/Styles/Colors_Dark.xaml");
                        if (res != null)
                        {
                            Application.Current.Resources.MergedDictionaries.Remove(res);
                            ResourceDictionary newRes = new ResourceDictionary();
                            newRes.Source = new Uri("/Resources/Styles/Colors_Light.xaml", UriKind.Relative);
                            Application.Current.Resources.MergedDictionaries.Add(newRes);
                        }
                        Application.Current.ThemeMode = ThemeMode.Light;
                    }
                    break;
                case ElementTheme.Dark:
                    {
                        var res = Application.Current.Resources.MergedDictionaries.FirstOrDefault(p => p.Source.ToString() == "/Resources/Styles/Colors_Light.xaml");
                        if (res != null)
                        {
                            Application.Current.Resources.MergedDictionaries.Remove(res);
                            ResourceDictionary newRes = new ResourceDictionary();
                            newRes.Source = new Uri("/Resources/Styles/Colors_Dark.xaml", UriKind.Relative);
                            Application.Current.Resources.MergedDictionaries.Add(newRes);
                        }
                        Application.Current.ThemeMode = ThemeMode.Dark;
                    }
                    break;
            }
        }
    }
}
