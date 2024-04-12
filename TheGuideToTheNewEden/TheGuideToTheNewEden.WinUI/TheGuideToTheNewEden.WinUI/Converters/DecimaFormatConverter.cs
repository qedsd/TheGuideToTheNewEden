using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    class DecimaFormatConverter : IValueConverter
    {
        public int Decimals { get; set; } = 2;
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            switch(value.GetType().ToString())
            {
                case "System.Double": return Math.Round((double)value, Decimals);
                case "System.Float": return Math.Round((float)value, Decimals);
                case "System.Decimal": return Math.Round((decimal)value, Decimals);
                default: return value;
            }
            
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
            switch (value.GetType().ToString())
            {
                case "System.Double": return Math.Round((double)value, Decimals);
                case "System.Float": return Math.Round((float)value, Decimals);
                case "System.Decimal": return Math.Round((decimal)value, Decimals);
                default: return value;
            }
        }
    }
}
