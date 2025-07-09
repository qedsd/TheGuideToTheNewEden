using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters
{
    internal class ZeroToVisibilityConverter : IValueConverter
    {
        public bool ZeroToVisible { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo language)
        {
            if (decimal.Parse(value.ToString()) == 0)
            {
                return ZeroToVisible ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                return ZeroToVisible ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo language)
        {
            throw new NotImplementedException();
        }
    }
}
