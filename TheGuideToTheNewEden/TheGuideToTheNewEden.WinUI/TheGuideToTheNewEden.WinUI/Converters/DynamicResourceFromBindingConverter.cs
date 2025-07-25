using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    public class DynamicResourceFromBindingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string culture)
        {
            if (value == null) return null;

            string resourceKey = value.ToString();
            if (Application.Current.Resources.TryGetValue(resourceKey, out var result))
            {
                return result;
            }
            else
            {
                return resourceKey;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string culture)
        {
            throw new NotSupportedException();
        }
    }
}
