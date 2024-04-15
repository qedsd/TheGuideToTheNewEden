using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class StatusOrderDescConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if(!(bool)value)//被压单
            {
                return Helpers.ResourcesHelper.GetString("CharacterOrderPage_Difference_Status_Backward");
            }
            else
            {
                return Helpers.ResourcesHelper.GetString("CharacterOrderPage_Difference_Status_Normal");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
