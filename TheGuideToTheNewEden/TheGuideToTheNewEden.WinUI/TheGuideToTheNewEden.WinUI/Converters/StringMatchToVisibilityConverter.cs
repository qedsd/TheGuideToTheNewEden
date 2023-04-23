using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    public sealed class StringMatchToVisibilityConverter : IValueConverter
    {
        public bool TrueToVisible { get; set; } = true;
        public object Convert(object value, Type targetType, object parameter, string language)
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

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
