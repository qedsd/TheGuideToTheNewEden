using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    internal class DEDLevelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if((int)value == 0)
            {
                return Helpers.ResourcesHelper.GetString("DEDPage_UnkownLevel");
            }
            else
            {
                return $"{(int)value}/10";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
