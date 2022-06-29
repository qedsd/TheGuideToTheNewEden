using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace TheGuideToTheNewEden.UWP.Converters
{
    /// <summary>
    /// Converts a null value to a <see cref="bool"/> value.
    /// </summary>
    public sealed class DoubleAddOrReduceColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                if ((double)value > 0)
                    return new SolidColorBrush(Colors.MediumSeaGreen);
                else if ((double)value == 0)
                    return new SolidColorBrush(Colors.Black);
                else
                    return new SolidColorBrush(Colors.OrangeRed);
            }
            catch (Exception)
            {
                return new SolidColorBrush(Colors.Black);
            };
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
