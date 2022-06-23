using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TheGuideToTheNewEden.WinUI.Utils
{
    internal static class XmlUtil
    {
        /// <summary>
        /// XML序列化某一类型到指定的文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="obj"></param>
        public static void SerializeToFile<T>(T obj, string filePath)
        {
            XmlSerializer serializer = new XmlSerializer(obj.GetType());
            string content = string.Empty;
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, obj);
                content = writer.ToString();
            }
            if(!Directory.Exists(Path.GetDirectoryName(filePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }
            using (StreamWriter stream_writer = new StreamWriter(filePath))
            {
                stream_writer.Write(content);
            }
        }

        /// <summary>
        /// 从某一XML文件反序列化到某一类型
        /// </summary>
        /// <param name="filePath">待反序列化的XML文件名称</param>
        /// <returns></returns>
        public static T DeserializeFromFile<T>(string filePath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StreamReader reader = new StreamReader(filePath))
            {
                T ret = (T)serializer.Deserialize(reader);
                return ret;
            }
        }
    }
}
