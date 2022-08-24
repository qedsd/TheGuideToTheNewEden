using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public static class WebCrawlerHelper
    {
        /// <summary>
        /// 获取指定标签内容
        /// </summary>
        /// <param name="html"></param>
        /// <param name="tag"></param>
        /// <param name="attr"></param>
        /// <returns></returns>
        public static List<string> GetLabelContent(string html, string tag, string attr)
        {
            //获取某标签简要信息
            Regex re = new Regex(@"(<" + tag + @"[\w\W].+?>)");
            MatchCollection imgreg = re.Matches(html);
            re = new Regex(@"(<" + tag + @">)");
            MatchCollection imgreg2 = re.Matches(html);

            List<string> fullName = new List<string>();
            foreach (var temp in imgreg)
            {
                if (temp.ToString().Contains(attr))
                    fullName.Add(temp.ToString());
            }
            foreach (var temp in imgreg2)
            {
                if (temp.ToString().Contains(attr))
                    fullName.Add(temp.ToString());
            }
            List<string> result = new List<string>();
            foreach (var temp in fullName)
            {
                int startIndex = html.IndexOf(temp);
                string toTheEnd = html.Substring(startIndex, html.Length - startIndex);
                int count_tag_less = 1;
                int count_tag_greater = 0;
                int wholeLength = 1 + tag.Length;
                string toTheEnd_temp = toTheEnd.Substring(wholeLength, toTheEnd.Length - wholeLength);
                for (int i = 0; i < toTheEnd.Length;)
                {
                    int index_temp = 0;
                    int lessMark = 0;
                    int greaterMark = 0;
                    lessMark = toTheEnd_temp.IndexOf("<" + tag);
                    greaterMark = toTheEnd_temp.IndexOf("</" + tag + ">");
                    int plus = 0;
                    //判断谁先找到
                    if (lessMark == -1 && greaterMark == -1)
                    {
                        index_temp = toTheEnd.Length - 1;
                    }
                    else if (lessMark == -1 || greaterMark == -1)
                    {
                        //index_temp = (lessMark == -1 ? lessMark : greaterMark);
                        //index_temp = (lessMark == -1 ? greaterMark : lessMark);
                        if (lessMark == -1)
                        {
                            index_temp = greaterMark;
                            plus = 2;
                            count_tag_greater++;
                        }
                        else
                            index_temp = toTheEnd.Length - 1;
                    }
                    else
                    {
                        if (lessMark < greaterMark)
                        {
                            index_temp = lessMark;
                            count_tag_less++;
                        }
                        else
                        {
                            index_temp = greaterMark;
                            count_tag_greater++;
                            plus = 2;//多一个/一个>
                        }
                    }
                    wholeLength += index_temp + 1 + tag.Length + plus;

                    i = wholeLength;
                    toTheEnd_temp = toTheEnd_temp.Substring(index_temp + 1 + tag.Length + plus, toTheEnd_temp.Length - (index_temp + 1 + tag.Length + plus));
                    if (count_tag_less == count_tag_greater)
                    {
                        result.Add(html.Substring(startIndex, wholeLength));
                        html = html.Remove(startIndex, wholeLength);
                        break;
                    }
                }
            }

            return result;
        }
        /// <summary>
        /// 获取指定标签内容
        /// </summary>
        /// <param name="html"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static List<string> GetLabelContent(string html, string tag)
        {
            //获取某标签简要信息
            Regex re = new Regex(@"(<" + tag + @">)");
            MatchCollection imgreg = re.Matches(html);

            List<string> fullName = new List<string>();
            foreach (var temp in imgreg)
            {
                fullName.Add(temp.ToString());
            }
            List<string> result = new List<string>();
            foreach (var temp in fullName)
            {
                int startIndex = html.IndexOf(temp);
                string toTheEnd = html.Substring(startIndex, html.Length - startIndex);
                int count_tag_less = 1;
                int count_tag_greater = 0;
                int wholeLength = 1 + tag.Length;
                string toTheEnd_temp = toTheEnd.Substring(wholeLength, toTheEnd.Length - wholeLength);
                for (int i = 0; i < toTheEnd.Length;)
                {
                    int index_temp = 0;
                    int lessMark = 0;
                    int greaterMark = 0;
                    lessMark = toTheEnd_temp.IndexOf("<" + tag);
                    greaterMark = toTheEnd_temp.IndexOf("</" + tag + ">");
                    int plus = 0;
                    //判断谁先找到
                    if (lessMark == -1 && greaterMark == -1)
                    {
                        index_temp = toTheEnd.Length - 1;
                    }
                    else if (lessMark == -1 || greaterMark == -1)
                    {
                        //index_temp = (lessMark == -1 ? lessMark : greaterMark);
                        //index_temp = (lessMark == -1 ? greaterMark : lessMark);
                        if (lessMark == -1)
                        {
                            index_temp = greaterMark;
                            plus = 2;
                            count_tag_greater++;
                        }
                        else
                            index_temp = toTheEnd.Length - 1;
                    }
                    else
                    {
                        if (lessMark < greaterMark)
                        {
                            index_temp = lessMark;
                            count_tag_less++;
                        }
                        else
                        {
                            index_temp = greaterMark;
                            count_tag_greater++;
                            plus = 2;//多一个/一个>
                        }
                    }
                    wholeLength += index_temp + 1 + tag.Length + plus;

                    i = wholeLength;
                    toTheEnd_temp = toTheEnd_temp.Substring(index_temp + 1 + tag.Length + plus, toTheEnd_temp.Length - (index_temp + 1 + tag.Length + plus));
                    if (count_tag_less == count_tag_greater)
                    {
                        result.Add(html.Substring(startIndex, wholeLength));
                        html = html.Remove(startIndex, wholeLength);
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>  
        /// 获取字符中指定标签的值  
        /// </summary>  
        /// <param name="str">字符串</param>  
        /// <param name="title">标签</param>  
        /// <param name="attrib">属性名</param>  
        /// <returns>属性</returns>  
        public static string GetTitleContent(string str, string title, string attrib)
        {

            string tmpStr = string.Format("<{0}[^>]*?{1}=(['\"\"]?)(?<url>[^'\"\"\\s>]+)\\1[^>]*>", title, attrib); //获取<title>之间内容  

            Match TitleMatch = Regex.Match(str, tmpStr, RegexOptions.IgnoreCase);

            string result = TitleMatch.Groups["url"].Value;
            return result;
        }

        /// <summary>  
        /// 获取字符中指定标签的值  
        /// </summary>  
        /// <param name="str">字符串</param>  
        /// <param name="title">标签</param>  
        /// <returns>值</returns>  
        public static string GetTitleContent(string str, string title)
        {
            string tmpStr = string.Format("<{0}[^>]*?>(?<Text>[^<]*)</{1}>", title, title); //获取<title>之间内容  

            Match TitleMatch = Regex.Match(str, tmpStr, RegexOptions.IgnoreCase);

            string result = TitleMatch.Groups["Text"].Value;
            return result;
        }
    }
}
