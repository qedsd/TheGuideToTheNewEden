using Microsoft.UI.Xaml.Data;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    public sealed class GameImageConverter : IValueConverter
    {
        public int Size { get; set; } = 128;
        public ImgType Type { get; set; } = ImgType.Type;
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || (int)value == 0)
            {
                return null;
            }
            return GetImageUri((int)value, Type, Size);
        }

        public static string GetImageUri(int id, ImgType type, int size = 128)
        {

            switch (Services.GameServerSelectorService.Value)
            {
                case Core.Enums.GameServerType.Tranquility: return $"https://imageserver.eveonline.com/{type}/{id}_{size}.{GetImageFormat(type)}";
                case Core.Enums.GameServerType.Serenity: return $"https://image.evepc.163.com/{type}/{id}_{size}.{GetImageFormat(type)}";
                default: return string.Empty;
            }
        }
        private static string GetImageFormat(ImgType type)
        {
            switch(type) 
            {
                case ImgType.Character: return "jpg";
                default:return "png";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
        public enum ImgType
        {
            Type,
            Character,
            Corporation,
            Alliance
        }
    }
}
