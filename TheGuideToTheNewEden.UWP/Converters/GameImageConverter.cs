using Newtonsoft.Json.Linq;
using System;
using Windows.UI.Xaml.Data;

namespace TheGuideToTheNewEden.UWP.Converters
{
    public sealed class GameImageConverter : IValueConverter
    {
        public int Size { get; set; } = 128;
        public ImgType Type { get; set; } = ImgType.Type;
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if(value == null || (int)value == 0)
            {
                return null;
            }
            switch (Services.GameServerSelectorService.GameServerType)
            {
                case Core.Enums.GameServerType.Tranquility: return $"https://imageserver.eveonline.com/{Type}/{value}_{Size}.jpg";
                default: return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    public enum ImgType
    {
        Type,
        Character,
        Corporation,
        Alliance
    }
}
