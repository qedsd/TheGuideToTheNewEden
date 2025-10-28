using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class DayOfWeekToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if(int.TryParse(value.ToString(),out int v) && v >= 0 && v< 7)
            {
                return Helpers.ResourcesHelper.GetString($"WormholePage_ZKB_DayOfWeek{v + 1}");
            }
            else
            {
                return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
