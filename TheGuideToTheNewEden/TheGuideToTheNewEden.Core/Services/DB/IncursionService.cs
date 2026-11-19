using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels.Static;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    /// <summary>
    /// 入侵站点查询服务（数据在 <c>Resources/Database/static.db</c> 的 <c>IncursionSite</c> 表）。
    /// <para>
    /// 只有 15 个站点，维度也简单：类型（集结点/先锋/突击/总部/母舰）+ 关键词。
    /// 排序按"难度递增"——集结点 → 先锋 → 突击 → 总部 → 母舰，符合玩家的实际进度。
    /// </para>
    /// </summary>
    public static class IncursionService
    {
        /// <summary>不限定类型。</summary>
        public const int AnyType = -1;

        /// <summary>按类型与关键词查询站点。</summary>
        /// <param name="type">0 集结点 / 1 先锋 / 2 突击 / 3 总部 / 4 母舰；<see cref="AnyType"/> 不限。</param>
        /// <param name="keyword">中英文名字或正文的模糊匹配。</param>
        public static async Task<List<IncursionSite>> QueryAsync(int type = AnyType, string keyword = null)
        {
            try
            {
                var db = DBService.StaticDb;
                if (db == null)
                {
                    return new List<IncursionSite>();
                }

                var query = db.Queryable<IncursionSite>();
                if (type != AnyType)
                {
                    query = query.Where(p => p.Type == type);
                }

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var k = keyword.Trim();
                    query = query.Where(p =>
                        (p.TitleCN != null && p.TitleCN.Contains(k)) ||
                        (p.TitleEN != null && p.TitleEN.Contains(k)) ||
                        (p.Content != null && p.Content.Contains(k)));
                }

                var list = await query.ToListAsync();
                return list.OrderBy(p => p.Type).ThenBy(p => p.TitleCN).ThenBy(p => p.Id).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<IncursionSite>();
            }
        }

        /// <summary>按主键取单条。取不到返回 null。</summary>
        public static async Task<IncursionSite> GetByIdAsync(int id)
        {
            try
            {
                var db = DBService.StaticDb;
                if (db == null)
                {
                    return null;
                }

                return await db.Queryable<IncursionSite>().Where(p => p.Id == id).FirstAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

        /// <summary>各类型条目数（键固定 0–4，无数据补 0）。</summary>
        public static async Task<Dictionary<int, int>> CountByTypeAsync()
        {
            var result = Enumerable.Range(0, 5).ToDictionary(i => i, _ => 0);
            try
            {
                var db = DBService.StaticDb;
                if (db == null)
                {
                    return result;
                }

                var types = await db.Queryable<IncursionSite>().Select(p => p.Type).ToListAsync();
                foreach (var t in types)
                {
                    if (result.ContainsKey(t))
                    {
                        result[t]++;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            return result;
        }
    }
}
