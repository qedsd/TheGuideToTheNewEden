using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters
{
    public sealed class StringToVisibilityConverter : IValueConverter
    {
        public bool IsNullOrEmptyToCollapsed { get; set; } = true;

        public object Convert(object value, Type targetType, object parameter, CultureInfo language)
        {
            if (string.IsNullOrEmpty(value as string))
            {
                return IsNullOrEmptyToCollapsed ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                return IsNullOrEmptyToCollapsed ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo language)
        {
            throw new NotImplementedException();
        }
    }
}
