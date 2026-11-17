using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    /// <summary>
    /// 战斗站点（死亡远征）查询服务。
    /// <para>
    /// 数据在 <c>Resources/Database/static.db</c> 的 <c>CombatSite</c> 表（与 <c>DED</c> 同库）。
    /// 相比只按势力取数的 <see cref="DEDService"/>，这里多了**分类 / 评级 / 难度档 / 关键词**四个维度——
    /// 补进 126 条战斗异常与 34 条化学实验室以后，光靠"势力"已经筛不动了。
    /// </para>
    /// <para>
    /// 取数约定与项目其它服务一致：全部 <c>try/catch</c> + <c>Log.Error</c>，失败返回空集合而不是抛异常
    /// （页面与 AI 工具拿到 <c>null</c> 会误判成"库里没有"，用空集合语义更清楚）。
    /// 排序放在内存里做：全表才 380 条，比让 SqlSugar 生成多段 <c>ORDER BY</c> 更可控。
    /// </para>
    /// </summary>
    public static class CombatSiteService
    {
        /// <summary>不限定分类时的取值（与 <see cref="CombatSiteCategory"/> 不冲突）。</summary>
        public const int AnyCategory = -1;

        /// <summary>不限定势力时的取值（与 0–5 的势力索引不冲突）。</summary>
        public const int AnyFaction = -1;

        /// <summary>
        /// 按条件查询站点。
        /// </summary>
        /// <param name="category">分类，见 <see cref="CombatSiteCategory"/>；<see cref="AnyCategory"/> 表示不限。</param>
        /// <param name="faction">势力 0–5；<see cref="AnyFaction"/> 表示不限。</param>
        /// <param name="rating">DED 评级 1–10；0 表示"只看没有评级的"；-1 表示不限。</param>
        /// <param name="tier">异常难度档 1–10；0 表示不限。</param>
        /// <param name="keyword">中英文标题 / wiki 页名 / 升级目标的模糊匹配；空表示不限。</param>
        public static async Task<List<CombatSite>> QueryAsync(
            int category = AnyCategory,
            int faction = AnyFaction,
            int rating = -1,
            int tier = 0,
            string keyword = null)
        {
            try
            {
                var db = DBService.StaticDb;
                if (db == null)
                {
                    return new List<CombatSite>();
                }

                var query = db.Queryable<CombatSite>();
                if (category != AnyCategory)
                {
                    query = query.Where(p => p.Category == category);
                }

                if (faction != AnyFaction)
                {
                    query = query.Where(p => p.Faction == faction);
                }

                if (rating >= 0)
                {
                    query = query.Where(p => p.Rating == rating);
                }

                if (tier > 0)
                {
                    query = query.Where(p => p.Tier == tier);
                }

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var k = keyword.Trim();
                    query = query.Where(p =>
                        (p.TitleCN != null && p.TitleCN.Contains(k)) ||
                        (p.TitleEN != null && p.TitleEN.Contains(k)) ||
                        (p.WikiTitle != null && p.WikiTitle.Contains(k)) ||
                        (p.Escalation != null && p.Escalation.Contains(k)));
                }

                var list = await query.ToListAsync();
                return list.OrderBy(p => p.Category)
                           .ThenBy(p => p.Faction)
                           .ThenBy(p => p.Rating)
                           .ThenBy(p => p.Tier)
                           .ThenBy(p => p.Level)
                           .ThenBy(p => p.Id)
                           .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<CombatSite>();
            }
        }

        /// <summary>按主键取单条（右侧正文用）。取不到返回 null。</summary>
        public static async Task<CombatSite> GetByIdAsync(int id)
        {
            try
            {
                var db = DBService.StaticDb;
                if (db == null)
                {
                    return null;
                }

                return await db.Queryable<CombatSite>().Where(p => p.Id == id).FirstAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

        /// <summary>
        /// 各分类的条目数（分类下拉框与导航徽标用）。
        /// 返回的字典一定包含全部 8 个分类的键，没有数据的补 0，调用方不必判空。
        /// </summary>
        public static async Task<Dictionary<int, int>> CountByCategoryAsync()
        {
            var result = Enum.GetValues(typeof(CombatSiteCategory))
                             .Cast<CombatSiteCategory>()
                             .ToDictionary(c => (int)c, _ => 0);
            try
            {
                var db = DBService.StaticDb;
                if (db == null)
                {
                    return result;
                }

                var categories = await db.Queryable<CombatSite>().Select(p => p.Category).ToListAsync();
                foreach (var c in categories)
                {
                    if (result.ContainsKey(c))
                    {
                        result[c]++;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            return result;
        }

        /// <summary>表里一共有多少条（探针与"共 N 项"用）。</summary>
        public static async Task<int> CountAsync()
        {
            try
            {
                var db = DBService.StaticDb;
                if (db == null)
                {
                    return 0;
                }

                return await db.Queryable<CombatSite>().CountAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return 0;
            }
        }
    }
}
