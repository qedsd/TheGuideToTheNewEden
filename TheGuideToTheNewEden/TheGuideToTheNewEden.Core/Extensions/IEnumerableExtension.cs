using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace TheGuideToTheNewEden.Core.Extensions
{
    public static class IEnumerableExtension
    {
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> ls)
        {
            if (ls is null)
            {
                return default;
            }
            HashSet<T> sets = new HashSet<T>();
            foreach(var p in ls)
            {
                sets.Add(p);
            }
            return sets;
        }

        public static string ToSeqString<T>(this IEnumerable<T> ls, string separator)
        {
            if (ls.NotNullOrEmpty())
            {
                StringBuilder stringBuilder = new StringBuilder();
                foreach (var p in ls)
                {
                    stringBuilder.Append(p);
                    stringBuilder.Append(separator);
                }
                stringBuilder.Remove(stringBuilder.Length - separator.Length, separator.Length);
                return stringBuilder.ToString();
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 既不为null也不为空
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ls"></param>
        /// <returns></returns>
        public static bool NotNullOrEmpty<T>(this IEnumerable<T> ls)
        {
            return ls != null && ls.Any();
        }

        /// <summary>
        ///     集合转为ObservableCollection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> source)
        {
            if (source == null) return null;
            return new ObservableCollection<T>(source);
        }
    }
}
