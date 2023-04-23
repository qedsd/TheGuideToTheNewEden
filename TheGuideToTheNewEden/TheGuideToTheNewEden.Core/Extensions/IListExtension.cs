using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Extensions
{
    public static class IListExtension
    {
        public static List<T> ToList<T>(this IList<object> ls) where T: class
        {
            List<T> items = new List<T>();
            foreach(var item in ls)
            {
                items.Add(item as T);
            }
            return items;
        }
    }
}
