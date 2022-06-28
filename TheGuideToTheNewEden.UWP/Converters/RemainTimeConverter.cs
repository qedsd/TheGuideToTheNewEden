using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace TheGuideToTheNewEden.UWP.Converters
{
    public sealed class RemainTimeConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if(value == null)
            {
                return null;
            }
            else
            {
                if((DateTime)value == DateTime.MinValue)
                {
                    return null;
                }
                else
                {
                    return (((DateTime)value).ToLocalTime() - DateTime.Now).ToString();
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
