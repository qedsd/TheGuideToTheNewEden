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
                switch(type)
                {
                    case 1: return Helpers.ResourcesHelper.GetString("MapNavigation_NavType_Stargate");
                    case 2: return Helpers.ResourcesHelper.GetString("MapNavigation_NavType_CapitalJump");
                    case 3: return Helpers.ResourcesHelper.GetString("MapNavigation_NavType_JumpBridge");
                    default:return type.ToString();
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
