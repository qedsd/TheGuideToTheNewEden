using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters
{
    public sealed class NullToZeroConverter : IValueConverter
    {
        public bool IsInverted { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo language)
        {
            if (value == null)
            {
                return IsInverted ? value.ToString() : "0";
            }
            else
            {
                return IsInverted ? "0" : value.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo language)
        {
            throw new NotImplementedException();
        }
    }
}
