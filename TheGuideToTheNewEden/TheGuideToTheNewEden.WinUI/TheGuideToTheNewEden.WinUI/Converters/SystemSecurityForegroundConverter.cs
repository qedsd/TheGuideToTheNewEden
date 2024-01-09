using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class SystemSecurityForegroundConverter : IValueConverter
    {
        public static SolidColorBrush Convert(double value)
        {
            SolidColorBrush solidColorBrush;
            switch (SystemSecurityFormatConverter.Convert((double)value))
            {
                case 1: solidColorBrush = Helpers.ResourcesHelper.Get("SystemSecurityForeground1") as SolidColorBrush;break;
                case 0.9: solidColorBrush = Helpers.ResourcesHelper.Get("SystemSecurityForeground09") as SolidColorBrush; break;
                case 0.8: solidColorBrush = Helpers.ResourcesHelper.Get("SystemSecurityForeground08") as SolidColorBrush; break;
                case 0.7: solidColorBrush = Helpers.ResourcesHelper.Get("SystemSecurityForeground07") as SolidColorBrush; break;
                case 0.6: solidColorBrush = Helpers.ResourcesHelper.Get("SystemSecurityForeground06") as SolidColorBrush; break;
                case 0.5: solidColorBrush = Helpers.ResourcesHelper.Get("SystemSecurityForeground05") as SolidColorBrush; break;
                case 0.4: solidColorBrush = Helpers.ResourcesHelper.Get("SystemSecurityForeground04") as SolidColorBrush; break;
                case 0.3: solidColorBrush = Helpers.ResourcesHelper.Get("SystemSecurityForeground03") as SolidColorBrush; break;
                case 0.2: solidColorBrush = Helpers.ResourcesHelper.Get("SystemSecurityForeground02") as SolidColorBrush; break;
                case 0.1: solidColorBrush = Helpers.ResourcesHelper.Get("SystemSecurityForeground01") as SolidColorBrush; break;
                default: solidColorBrush = Helpers.ResourcesHelper.Get("SystemSecurityForeground00") as SolidColorBrush; break;
            }
            return solidColorBrush;
        }
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Convert((double)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
