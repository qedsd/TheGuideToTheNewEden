using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class TargetStringToVisibilityConverter : IValueConverter
    {
        public string TargetValue { get; set; } = "0";

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value.ToString() == TargetValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
