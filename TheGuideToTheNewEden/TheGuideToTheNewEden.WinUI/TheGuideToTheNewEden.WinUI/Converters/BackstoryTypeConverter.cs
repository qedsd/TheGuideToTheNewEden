using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class BackstoryTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            switch ((int)value)
            {
                case 0: return Helpers.ResourcesHelper.GetString("Backstory_Type_Race");
                case 1: return Helpers.ResourcesHelper.GetString("Backstory_Type_History");
                case 2: return Helpers.ResourcesHelper.GetString("Backstory_Type_Technology");
                case 3: return Helpers.ResourcesHelper.GetString("Backstory_Type_Organization");
                default: return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
