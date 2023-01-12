using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    public sealed class StringToVisibilityConverter : IValueConverter
    {
        public bool IsNullOrEmptyToCollapsed { get; set; } = true;

        public object Convert(object value, Type targetType, object parameter, string language)
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

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
