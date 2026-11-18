using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels.Static;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    /// <summary>
    /// 任务查询服务（数据在 <c>Resources/Database/static.db</c> 的 <c>Mission</c> 表）。
    /// <para>
    /// 2026-11 重做：老表只有 `Level / Title_Zh / Title_En / Content`，只能"按等级列出、点开看一段中文"，
    /// 既不能按分类/势力筛，也没有目标、奖励、船型限制这些结构字段。现在字段对齐 wiki 的
    /// `{{Missiondetails}}` 模板，查询维度与 <see cref="CombatSiteService"/> 保持一致
    /// （分类 / 等级 / 势力 / 关键词），错误一律 <c>Log.Error</c> + 空集合。
    /// </para>
    /// </summary>
    public static class MissionService
    {
        /// <summary>不限定分类。</summary>
        public const int AnyCategory = -1;

        /// <summary>不限定势力。</summary>
        public const int AnyFaction = -1;

        /// <summary>不限定等级。</summary>
        public const int AnyLevel = 0;

        /// <summary>按条件查询任务。</summary>
        /// <param name="category">分类，见 <see cref="MissionCategory"/>；<see cref="AnyCategory"/> 不限。</param>
        /// <param name="level">等级 1–5；<see cref="AnyLevel"/> 不限。</param>
        /// <param name="faction">势力 0–5；<see cref="AnyFaction"/> 不限。</param>
        /// <param name="keyword">中英文标题 / wiki 页名 / 任务目标的模糊匹配。</param>
        public static async Task<List<Mission>> QueryAsync(
            int category = AnyCategory,
            int level = AnyLevel,
            int faction = AnyFaction,
            string keyword = null)
        {
            try
            {
                var db = DBService.StaticDb;
                if (db == null)
                {
                    return new List<Mission>();
                }

                var query = db.Queryable<Mission>();
                if (category != AnyCategory)
                {
                    query = query.Where(p => p.Category == category);
                }

                if (level != AnyLevel)
                {
                    query = query.Where(p => p.Level == level);
                }

                if (faction != AnyFaction)
                {
                    query = query.Where(p => p.Faction == faction);
                }

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var k = keyword.Trim();
                    query = query.Where(p =>
                        (p.TitleCN != null && p.TitleCN.Contains(k)) ||
                        (p.TitleEN != null && p.TitleEN.Contains(k)) ||
                        (p.WikiTitle != null && p.WikiTitle.Contains(k)) ||
                        (p.Objective != null && p.Objective.Contains(k)));
                }

                var list = await query.ToListAsync();
                return list.OrderBy(p => p.Category)
                           .ThenBy(p => p.Level)
                           .ThenBy(p => p.Arc)
                           .ThenBy(p => p.TitleCN)
                           .ThenBy(p => p.Id)
                           .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<Mission>();
            }
        }

        /// <summary>按主键取单条（右侧正文用）。取不到返回 null。</summary>
        public static async Task<Mission> GetByIdAsync(int id)
        {
            try
            {
                var db = DBService.StaticDb;
                if (db == null)
                {
                    return null;
                }

                return await db.Queryable<Mission>().Where(p => p.Id == id).FirstAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

        /// <summary>各分类条目数（键一定包含全部分类，无数据补 0）。</summary>
        public static async Task<Dictionary<int, int>> CountByCategoryAsync()
        {
            var result = Enum.GetValues(typeof(MissionCategory))
                             .Cast<MissionCategory>()
                             .ToDictionary(c => (int)c, _ => 0);
            try
            {
                var db = DBService.StaticDb;
                if (db == null)
                {
                    return result;
                }

                var categories = await db.Queryable<Mission>().Select(p => p.Category).ToListAsync();
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

        /// <summary>表里一共多少条。</summary>
        public static async Task<int> CountAsync()
        {
            try
            {
                var db = DBService.StaticDb;
                if (db == null)
                {
                    return 0;
                }

                return await db.Queryable<Mission>().CountAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return 0;
            }
        }
    }
}
