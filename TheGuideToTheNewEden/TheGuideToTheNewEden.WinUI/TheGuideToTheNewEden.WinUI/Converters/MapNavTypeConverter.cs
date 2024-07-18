using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class MapNavTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int type = (int)value;
            if(type == 0)
            {
                return string.Empty;
            }
            else
            {
                return Helpers.ResourcesHelper.GetString(type == 1 ? "MapNavigation_NavType_Stargate" : "MapNavigation_NavType_CapitalJump");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
