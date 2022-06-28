using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace TheGuideToTheNewEden.UWP.Converters
{
    public sealed class IntToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 分界点
        /// </summary>
        public int Separation { get; set; } = 0;
        /// <summary>
        /// false 大于Separation时返回Visible
        /// true时 大于Separation时返回Collapsed
        /// </summary>
        public bool IsInverted { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if(value == null)
            {
                return Visibility.Collapsed;
            }
            if ((int)value > Separation)
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
