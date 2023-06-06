using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    class TimeSpanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            TimeSpan timeSpan = (TimeSpan)value;
            return $"{timeSpan.Days}{Helpers.ResourcesHelper.GetString("General_Day")}{timeSpan.Hours}{Helpers.ResourcesHelper.GetString("General_Hour")}{timeSpan.Minutes}{Helpers.ResourcesHelper.GetString("General_Minute")}{timeSpan.Seconds}{Helpers.ResourcesHelper.GetString("General_Second")}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
