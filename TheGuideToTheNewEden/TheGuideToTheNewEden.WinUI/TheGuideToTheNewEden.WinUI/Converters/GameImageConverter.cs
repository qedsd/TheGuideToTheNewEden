using ESI.NET.Models.Character;
using Microsoft.UI.Xaml.Data;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Vanara.PInvoke.Kernel32;

namespace TheGuideToTheNewEden.WinUI.Converters
{
    public sealed class GameImageConverter : IValueConverter
    {
        private const string TranquilityURL = "https://images.evetech.net";
        private const string Serenity = "https://image.evepc.163.com";

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
            if (id <= 0)
                return null;
            switch (Services.GameServerSelectorService.Value)
            {
                case Core.Enums.GameServerType.Tranquility: return GetTranquilityImage(id, type, size);
                case Core.Enums.GameServerType.Serenity: return $"{Serenity}/{type}/{id}_{size}.{GetImageFormat(type)}";
                default: return string.Empty;
            }
        }
        private static string GetTranquilityImage(int id, ImgType type, int size = 128)
        {
            switch (type)
            {
                case ImgType.Alliance: return $"{TranquilityURL}/alliances/{id}/logo?size={size}";
                case ImgType.Corporation: return $"{TranquilityURL}/corporations/{id}/logo?size={size}";
                case ImgType.Character: return $"{TranquilityURL}/characters/{id}/portrait?size={size}";
                case ImgType.Type:
                    {
                        int rootMarketGroup = Core.Services.DB.InvMarketGroupService.QueryRootGroupOfType(id);
                        bool isRenderType = rootMarketGroup == 4 | rootMarketGroup == 157;//舰船or无人机
                        if (isRenderType)
                        {
                            return $"{TranquilityURL}/types/{id}/render?size={size}";
                        }
                        else
                        {
                            return $"{TranquilityURL}/types/{id}/icon?size={size}";
                        }
                    }
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
