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
    class NullToVisibilityConverter : IValueConverter
    {
        public bool NullToVisible { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo language)
        {
            if (value == null)
            {
                return NullToVisible ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                return NullToVisible ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo language)
        {
            throw new NotImplementedException();
        }
    }
}
