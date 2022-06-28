using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace TheGuideToTheNewEden.UWP.Converters
{
    /// <summary>
    /// Converts a null value to a <see cref="bool"/> value.
    /// </summary>
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public bool IsInverted { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null)
            {
                return IsInverted ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                return IsInverted ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
