using Newtonsoft.Json;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace TheGuideToTheNewEden.UWP.Helpers
{
    public static class ObjectExtension
    {
        /// <summary>
        /// 深度克隆
        /// 通过json序列化对象实现
        /// 出错时返回类型默认值
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">原始版本对象</param>
        /// <returns>深度克隆后的对象</returns>
        public static T DepthClone<T>(this object obj)
        {
            if (ReferenceEquals(obj, null))
            {
                return default(T);
            }
            try
            {
                var deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
                return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj), deserializeSettings);
            }
            catch (JsonException)
            {
                return default(T);
            }
        }

        /// <summary>
        /// 深度克隆
        /// 通过xml序列化对象实现，要求对象可序列化[Serializable]
        /// 出错时返回null
        /// </summary>
        /// <param name="obj">原始版本对象</param>
        /// <returns>深度克隆后的对象</returns>
        public static object DepthClone(this object obj)
        {
            if (ReferenceEquals(obj, null))
            {
                return null;
            }
            object clone = new object();
            using (Stream stream = new MemoryStream())
            {
                IFormatter formatter = new BinaryFormatter();
                try
                {
                    formatter.Serialize(stream, obj);
                    stream.Seek(0, SeekOrigin.Begin);
                    clone = formatter.Deserialize(stream);
                }
                catch (SerializationException)
                {
                    return null;
                }
            }
            return clone;
        }
    }
}
