using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    public class OrderTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if((bool)value)//IsBuyOrder
            {
                return Helpers.ResourcesHelper.GetString("CharacterOrderPage_OrderType_Buy");
            }
            else
            {
                return Helpers.ResourcesHelper.GetString("CharacterOrderPage_OrderType_Sell");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
