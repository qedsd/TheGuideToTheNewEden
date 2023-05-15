using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class IsBuyForegroundConverter : IValueConverter
    {
        private static SolidColorBrush _greenBrush;
        private static SolidColorBrush _redBrush;
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(bool)value)
            {
                if (_greenBrush == null)
                {
                    _greenBrush = new SolidColorBrush(Colors.SeaGreen);
                }
                return _greenBrush;
            }
            else
            {
                if (_redBrush == null)
                {
                    _redBrush = new SolidColorBrush(Colors.OrangeRed);
                }
                return _redBrush;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
