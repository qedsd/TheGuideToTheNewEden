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
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public bool IsReverse { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo language)
        {
            if (value == null)
            {
                return Visibility.Collapsed;
            }
            if ((bool)value)
            {
                return IsReverse ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                return IsReverse ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo language)
        {
            throw new NotImplementedException();
        }
    }
}
