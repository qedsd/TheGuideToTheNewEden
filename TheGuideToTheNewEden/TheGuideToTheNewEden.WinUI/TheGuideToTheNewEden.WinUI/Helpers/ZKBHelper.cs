using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZKB.NET.Models.Killmails;
using ZKB.NET;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    public static class ZKBHelper
    {
        private static int _maxItems = 200;
        private static float _targetItemsF = 50;
        private static int _targetItems = 50;
        /// <summary>
        /// 给ZKB加上每页最大数量
        /// </summary>
        /// <param name="paramModifiers"></param>
        /// <param name="page"></param>
        /// <param name="typeModifiers"></param>
        /// <returns></returns>
        public static async Task<List<ZKillmaill>> GetKillmaillAsync(List<ParamModifierData> paramModifiers, int page = 1, params TypeModifier[] typeModifiers)
        {
            int zkbPage = (int)Math.Ceiling((page * _targetItemsF) / _maxItems);
            var pageParam = paramModifiers.FirstOrDefault(p => p.Modifier == ParamModifier.Page);
            if(!string.IsNullOrEmpty(pageParam.Param))
            {
                paramModifiers.Remove(pageParam);
            }
            paramModifiers.Add(new ParamModifierData(ParamModifier.Page, zkbPage.ToString()));
            var totalKillmaills = await ZKB.NET.ZKB.GetKillmaillAsync(paramModifiers.ToArray(), typeModifiers);
            return totalKillmaills.Skip((page - 1) * _targetItems).Take(_targetItems).ToList();
        }
    }
}
