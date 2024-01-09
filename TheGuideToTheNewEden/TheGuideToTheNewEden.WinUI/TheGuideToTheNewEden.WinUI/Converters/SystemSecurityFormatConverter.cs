using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class SystemSecurityFormatConverter : IValueConverter
    {
        public static double Convert(double value)
        {
            return Math.Round(value,1);
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
