using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    class ISKNormalizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string str = value.ToString();
            var array = str.Split(".");
            int length = array[0].Length;
            double number = (double)value;
            switch (length)
            {
                case 4:
                case 5:
                case 6: return (Math.Round(number / 1000, 2) + "k");
                case 7:
                case 8:
                case 9: return (Math.Round(number / 1000000, 2) + "m");
                case 10:
                case 11:
                case 12: return (Math.Round(number / 1000000000, 2) + "b");
                case 13:
                case 14:
                case 15: return (Math.Round(number / 1000000000000, 2) + "t");
                case 16:
                case 17:
                case 18: return (Math.Round(number / 1000000000000000, 2) + "aa");
                default: return number.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
