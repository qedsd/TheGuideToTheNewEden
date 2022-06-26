using System;
using Windows.UI.Xaml.Data;

namespace TheGuideToTheNewEden.UWP.Converters
{
    public sealed class CharacterIdToAvatarConverter : IValueConverter
    {
        public int Size { get; set; } = 128;
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            switch (Services.GameServerSelectorService.GameServerType)
            {
                case Core.Enums.GameServerType.Tranquility: return $"https://imageserver.eveonline.com/Character/{value}_{Size}.jpg";
                default: return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
