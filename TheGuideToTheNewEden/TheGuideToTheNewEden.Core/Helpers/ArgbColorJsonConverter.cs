using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TheGuideToTheNewEden.Core.Helpers
{
    /// <summary>
    /// <see cref="Color"/> 序列化为 <c>#AARRGGBB</c> 字符串。
    /// <para>
    /// 为什么要自己写：Newtonsoft 默认序列化 <see cref="Color"/> 会输出 <c>{A,R,G,B,Name,IsKnownColor…}</c>
    /// 一大坨，且**丢掉透明度**（半透明色往返后变成不透明）。
    /// 另外注意不要用 <see cref="System.Drawing.ColorConverter"/>：它是 <c>TypeConverter</c>，
    /// 不是 <c>Newtonsoft.Json.JsonConverter</c>，写进 <c>[JsonConverter]</c> 会在序列化时抛
    /// <c>InvalidCastException</c>；而异常发生在 Save 内部，表现为"改了设置保存不了 / 整个配置文件都不再更新"。
    /// </para>
    /// </summary>
    public sealed class ArgbColorJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Color);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var color = value is Color c ? c : Color.Empty;
            writer.WriteValue(color.A == 255
                ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
                : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return Color.Empty;
            }

            // 兼容两种历史写法：#RRGGBB / #AARRGGBB 字符串，以及早期序列化出的对象 {A,R,G,B}
            if (reader.TokenType == JsonToken.String)
            {
                return Parse(reader.Value as string);
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                var obj = JObject.Load(reader);
                return Color.FromArgb(
                    Get(obj, "A", 255),
                    Get(obj, "R", 0),
                    Get(obj, "G", 0),
                    Get(obj, "B", 0));
            }

            return Color.Empty;
        }

        private static int Get(JObject obj, string name, int fallback)
            => obj.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token) && token.Type == JTokenType.Integer
                ? token.Value<int>()
                : fallback;

        private static Color Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Color.Empty;
            }

            var value = text.Trim().TrimStart('#');
            if (value.Length == 3)
            {
                value = string.Concat(value.Select(ch => new string(ch, 2)));
            }

            if (value.Length != 6 && value.Length != 8)
            {
                return Color.Empty;
            }

            uint parsed;
            if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed))
            {
                return Color.Empty;
            }

            return value.Length == 6
                ? Color.FromArgb(255, (byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed)
                : Color.FromArgb((byte)(parsed >> 24), (byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed);
        }
    }
}
