using System;
using System.Collections.Generic;
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
            if (ReferenceEquals(ls, null))
            {
                return null;
            }
            else
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
        }
    }
}
