using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace TheGuideToTheNewEden.UWP.Converters
{
    /// <summary>
    /// Converts a null value to a <see cref="bool"/> value.
    /// </summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public bool IsReverse { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if((bool)value)
            {
                return IsReverse?Visibility.Collapsed:Visibility.Visible;
            }
            else
            {
                return IsReverse ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
