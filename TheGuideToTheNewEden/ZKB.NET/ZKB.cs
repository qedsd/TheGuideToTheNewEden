using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET
{
    public static partial class ZKB
    {
        /// <summary>
        /// 字符串起始字母小写
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static string ToStartLower(string str)
        {
            return char.ToLower(str[0]) + str.Substring(1);
        }
    }
}
