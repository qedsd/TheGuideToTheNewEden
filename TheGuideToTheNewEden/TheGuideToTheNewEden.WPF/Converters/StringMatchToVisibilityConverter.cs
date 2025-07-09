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
    public sealed class StringMatchToVisibilityConverter : IValueConverter
    {
        public bool TrueToVisible { get; set; } = true;
        public object Convert(object value, Type targetType, object parameter, CultureInfo language)
        {
            if(value.ToString() == parameter.ToString())
            {
                return TrueToVisible ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                return TrueToVisible ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo language)
        {
            throw new NotImplementedException();
        }
    }
}
