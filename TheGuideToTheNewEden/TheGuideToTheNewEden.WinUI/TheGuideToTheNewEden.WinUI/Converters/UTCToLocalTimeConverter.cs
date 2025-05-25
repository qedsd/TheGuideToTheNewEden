using Microsoft.UI.Xaml.Data;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    public class UTCToLocalTimeConverter : IValueConverter
    {
        public static string Convert(DateTimeOffset dateTime)
        {
            return $"{Helpers.ResourcesHelper.GetString("General_LocalTime")} {(dateTime).ToLocalTime()}";
        }
        public static string Convert(DateTime dateTime)
        {
            return $"{Helpers.ResourcesHelper.GetString("General_LocalTime")} {(dateTime).ToLocalTime()}";
        }
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if(value.GetType() == typeof(DateTime))
            {
                return Convert((DateTime)value);
            }
            else if(value.GetType() == typeof(DateTimeOffset))
            {
                return Convert((DateTimeOffset)value);
            }
            else
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
