using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    /// <summary>
    /// 聊天日志文件名包含的信息
    /// Alliance_20211218_1234_112121221
    /// 频道名_日期_频道文件唯一id_监听角色id
    /// 注：同一个频道，频道名与语言相关、唯一id可能不同
    /// </summary>
    public class ChatlogFileInfo
    {
        /// <summary>
        /// 频道名称
        /// 与游戏语言相关
        /// 如Alliance、联盟
        /// </summary>
        public string ChannelName { get; set; }

        public DateTime Date {  get; set; }
        /// <summary>
        /// 同一个频道id有可能不同
        /// </summary>
        public string ChannelID { get; set; }
        /// <summary>
        /// 角色id
        /// 聊天文件里面只包含角色名(Listener)，可从文件名里面获取id
        /// </summary>
        public string ListenerID { get; set; }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; }

        public static ChatlogFileInfo Create(string fileFullPath)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(fileFullPath);
            var array = fileName.Split('_');
            if (array.Length >= 4)
            {
                if (array.Length > 4)
                {
                    //可能存在频道名称带_的情况，需要从后往回找
                    string a4 = array[array.Length - 1];
                    string a3 = array[array.Length - 2];
                    string a2 = array[array.Length - 3];
                    string a1 = fileName.Substring(0, fileName.Length - a4.Length - a3.Length - a2.Length - 3);
                    array = new string[] { a1, a2, a3, a4 };
                }
                if (array[1].Length == 8)
                {
                    if (int.TryParse(array[1].Substring(0, 4), out var y))
                    {
                        if (int.TryParse(array[1].Substring(4, 2), out var m))
                        {
                            if (int.TryParse(array[1].Substring(6, 2), out var d))
                            {
                                //测试用
                                //int index = array[3].IndexOf(" ");
                                //if(index > 0)
                                //{
                                //    array[3] = array[3].Substring(0, index);
                                //}
                                return new ChatlogFileInfo()
                                {
                                    ChannelName = array[0],
                                    Date = new DateTime(y, m, d),
                                    ChannelID = array[2],
                                    ListenerID = array[3],
                                    FilePath = fileFullPath
                                };
                            }
                        }
                    }
                }
            }
            Log.Error($"无效的日志文件名:{fileFullPath}");
            return null;
        }
    }
}
