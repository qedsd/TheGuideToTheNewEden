using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    public static class ChatLogHelper
    {
        /// <summary>
        /// 查询本地位置
        /// </summary>
        /// <param name="chatContent"></param>
        /// <param name="nameDbs"></param>
        /// <returns>查询失败时返回-1，成功返回星系id</returns>
        public static async Task<int> TryGetCharacterLocationAsync(IntelChatContent chatContent, List<string> nameDbs)
        {
            if (Core.EVEHelpers.ChatSpeakerHelper.IsEVESystem(chatContent.SpeakerName))
            {
                if (Core.EVEHelpers.ChatSystemContentFormatHelper.IsLocalChanged(chatContent.Content))
                {
                    var array = chatContent.Content.Split(new char[] { ':', '：' });
                    if (array.Length > 0)
                    {
                        var name = array.Last().Trim().Replace("*", "");
                        foreach (var db in nameDbs)
                        {
                            int id = await MapSolarSystemNameService.QueryIdAsync(db == nameDbs.FirstOrDefault() ? Core.Config.DBPath : db, name);
                            if (id != -1)
                            {
                                return id;
                            }
                        }
                    }
                }
            }
            return -1;
        }
    }
}
