using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Extensions
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
        /// 复制target的同名属性、自动到自身
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="target"></param>
        public static void CopyFrom<T>(this object obj, T target, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var targetType = obj.GetType();
            var sourceType = typeof(T);

            // 复制属性
            foreach (var sourceProperty in sourceType.GetProperties(bindingFlags))
            {
                // 跳过索引器属性
                if (sourceProperty.GetIndexParameters().Length > 0)
                    continue;

                var targetProperty = targetType.GetProperty(sourceProperty.Name, bindingFlags);

                if (targetProperty != null &&
                    targetProperty.CanWrite &&
                    IsCompatibleType(targetProperty.PropertyType, sourceProperty.PropertyType))
                {
                    try
                    {
                        var value = sourceProperty.GetValue(target, null);
                        targetProperty.SetValue(obj, value, null);
                    }
                    catch (Exception ex)
                    {
                        // 可以选择记录日志或忽略特定错误
                        Debug.WriteLine($"Error copying property {sourceProperty.Name}: {ex.Message}");
                    }
                }
            }

            // 复制字段
            foreach (var sourceField in sourceType.GetFields(bindingFlags))
            {
                var targetField = targetType.GetField(sourceField.Name, bindingFlags);

                if (targetField != null &&
                    IsCompatibleType(targetField.FieldType, sourceField.FieldType))
                {
                    try
                    {
                        var value = sourceField.GetValue(target);
                        targetField.SetValue(obj, value);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error copying field {sourceField.Name}: {ex.Message}");
                    }
                }
            }
        }

        private static bool IsCompatibleType(Type targetType, Type sourceType)
        {
            return targetType == sourceType ||
                   targetType.IsAssignableFrom(sourceType) ||
                   (targetType.IsGenericType &&
                    targetType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                    targetType.GetGenericArguments()[0] == sourceType);
        }
    }
}
