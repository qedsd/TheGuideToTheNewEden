using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    class NullToVisibilityConverter : IValueConverter
    {
        public bool NullToVisible { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, string language)
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

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
