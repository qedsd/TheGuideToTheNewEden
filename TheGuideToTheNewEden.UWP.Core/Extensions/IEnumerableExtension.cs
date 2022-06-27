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
    }
}
