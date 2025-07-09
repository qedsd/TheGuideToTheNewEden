using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TheGuideToTheNewEden.WPF.Helpers
{
    internal static class ResourcesHelper
    {
        public static string GetString(string key,bool returnKey = true)
        {
            if (Application.Current.Resources.Contains(key))
            {
                return Application.Current.Resources[key].ToString();
            }
            else
            {
                if(returnKey)
                {
                    return key;
                }
                else
                {
                    return String.Empty;
                }
            }
        }
        public static object Get(string key)
        {
            if (Application.Current.Resources.Contains(key))
            {
                return Application.Current.Resources[key];
            }
            else
            {
                return null;
            }
        }
    }
}
