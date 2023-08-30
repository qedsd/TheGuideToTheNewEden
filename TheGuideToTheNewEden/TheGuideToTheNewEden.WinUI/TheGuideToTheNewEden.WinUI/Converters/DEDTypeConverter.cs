using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class DEDTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            switch((int)value)
            {
                case 0: return Helpers.ResourcesHelper.GetString("DEDPage_Drone");
                case 1: return Helpers.ResourcesHelper.GetString("DEDPage_Serpentis");
                case 2: return Helpers.ResourcesHelper.GetString("DEDPage_BloodRaider");
                case 3: return Helpers.ResourcesHelper.GetString("DEDPage_Sansha");
                case 4: return Helpers.ResourcesHelper.GetString("DEDPage_Angel");
                case 5: return Helpers.ResourcesHelper.GetString("DEDPage_Guristas");
                default:return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
