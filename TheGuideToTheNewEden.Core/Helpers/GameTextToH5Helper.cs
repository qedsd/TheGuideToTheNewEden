using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public static class GameTextToH5Helper
    {
        public static string TranFormat(string gameText)
        {
            Regex reg = new Regex(" size=\\\"" + "\\d+" + "\\" + "\"");
            string result = reg.Replace(gameText, "");
            StringBuilder stringBuilder = new StringBuilder(result);
            Regex reg3 = new Regex("color=\\\".{9}\\\"");
            var m = reg3.Match(result);
            Color color;
            string colorStr;
            while (m.Success)
            {
                int cindex = m.Index + 7;
                color = ColorHx16toRGB(result.Substring(cindex, 9));
                int r = color.R % 255;
                int g = color.G % 255;
                int b = color.B % 255;
                color = Color.FromArgb(r, g, b);
                colorStr = ColorRGBtoHx16(color);
                for (int i = 1; i < 9; i++)
                {
                    stringBuilder[cindex + i] = colorStr[i];
                }
                m = m.NextMatch();
            }
            return stringBuilder.ToString();
        }

        public static Color ColorHx16toRGB(string strHxColor)
        {
            try
            {
                if (strHxColor.Length == 0)
                {//如果为空
                    return System.Drawing.Color.FromArgb(0, 0, 0);//设为黑色
                }
                else
                {//转换颜色
                    return System.Drawing.Color.FromArgb(int.Parse(strHxColor.Substring(7, 2), System.Globalization.NumberStyles.AllowHexSpecifier), int.Parse(strHxColor.Substring(1, 2), System.Globalization.NumberStyles.AllowHexSpecifier), int.Parse(strHxColor.Substring(3, 2), System.Globalization.NumberStyles.AllowHexSpecifier), int.Parse(strHxColor.Substring(5, 2), System.Globalization.NumberStyles.AllowHexSpecifier));
                }
            }
            catch
            {//设为黑色
                return System.Drawing.Color.FromArgb(0, 0, 0);
            }
        }

        /// <summary>
        /// [颜色：RGB转成16进制]
        /// </summary>
        /// <param name="R">红 int</param>
        /// <param name="G">绿 int</param>
        /// <param name="B">蓝 int</param>
        /// <returns></returns>
        public static string ColorRGBtoHx16(Color color)
        {
            string R = Convert.ToString(color.R, 16);
            if (R.Length == 1)
                R = "0" + R;
            string G = Convert.ToString(color.G, 16);
            if (G.Length == 1)
                G = "0" + G;
            string B = Convert.ToString(color.B, 16);
            if (B.Length == 1)
                B = "0" + B;
            string A = Convert.ToString(color.A, 16);
            if (A.Length == 1)
                A = "0" + A;
            string HexColor = "#" + R + G + B + A;
            return HexColor;
        }
    }
}
