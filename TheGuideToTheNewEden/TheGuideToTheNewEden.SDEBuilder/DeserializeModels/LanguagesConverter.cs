using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    /// <summary>
    /// SDE 里 name/description 绝大多数是 {de,en,...} 对象，但少数记录（如 npcCharacters）
    /// 是纯字符串。这里统一兼容两种形态：字符串时填到所有语言字段。
    /// </summary>
    public class LanguagesConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Languages);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }
            if (reader.TokenType == JsonToken.String)
            {
                var text = (string)reader.Value;
                return new Languages
                {
                    De = text,
                    En = text,
                    Es = text,
                    Fr = text,
                    Ja = text,
                    Ko = text,
                    Ru = text,
                    Zh = text
                };
            }

            var obj = JObject.Load(reader);
            var languages = new Languages();
            foreach (var property in obj.Properties())
            {
                var value = property.Value.Type == JTokenType.Null ? null : property.Value.ToString();
                switch (property.Name.ToLowerInvariant())
                {
                    case "de": languages.De = value; break;
                    case "en": languages.En = value; break;
                    case "es": languages.Es = value; break;
                    case "fr": languages.Fr = value; break;
                    case "ja": languages.Ja = value; break;
                    case "ko": languages.Ko = value; break;
                    case "ru": languages.Ru = value; break;
                    case "zh": languages.Zh = value; break;
                }
            }
            return languages;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
