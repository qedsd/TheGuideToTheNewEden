using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace TheGuideToTheNewEden.UWP.Converters
{
    public sealed class BoolAddOrReduceColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                if (!(bool)value)
                    return new SolidColorBrush(Colors.MediumSeaGreen);
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
