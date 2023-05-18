using Microsoft.UI;
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
    internal class MailSubjectForegroundConverter : IValueConverter
    {
        private static SolidColorBrush _greenBrush;
        public static SolidColorBrush GetNormal()
        {
            if (Application.Current.Resources.TryGetValue("ForegroundBrush", out var color))
            {
                return color as SolidColorBrush;
            }
            else
            {
                throw new NotImplementedException("无效资源名");
            }
        }
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if((bool)value)
            {
                if (Application.Current.Resources.TryGetValue("ForegroundBrush", out var color))
                {
                    return color;
                }
                else
                {
                    throw new ArgumentException("无效资源名");
                }
            }
            else
            {
                if (_greenBrush == null)
                {
                    _greenBrush = new SolidColorBrush(Colors.SeaGreen);
                }
                return _greenBrush;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
