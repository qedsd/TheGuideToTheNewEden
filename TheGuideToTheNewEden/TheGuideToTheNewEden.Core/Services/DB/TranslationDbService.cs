using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    /// <summary>
    /// 本地数据库翻译：把 EVE 专有名词（物品 / 星域 / 星系 / 空间站）在
    /// 「主数据库（英文 SDE）」与「本地化数据库（中文 zh.db）」之间互译。
    /// <para>
    /// 纯离线、只读，不发任何网络请求——翻译功能的第一个数据源（取代旧的有道 API 方案）。
    /// 两侧数据库的同一张表以 <c>Id</c> 一一对应，因此只需按名称在<b>原文语言</b>的库里模糊匹配，
    /// 再用这批 ID 去<b>译文语言</b>的库里批量取对照行（每类名词固定 2 次查询，不做逐条查询）。
    /// </para>
    /// </summary>
    public static class TranslationDbService
    {
        /// <summary>中文语言代码（沿用旧在线翻译接口的取值，便于将来混用多数据源）。</summary>
        public const string Chinese = "zh-CHS";

        /// <summary>英文语言代码。</summary>
        public const string English = "en";

        /// <summary>单类名词的匹配上限：避免输入一两个字符就把整库拉回来。</summary>
        public const int DefaultLimitPerKind = 100;

        /// <summary>两侧数据库是否都可用（主库 + 本地化库）。</summary>
        public static bool IsAvailable => DBService.MainDbReady && DBService.LocalDbReady;

        /// <summary>
        /// 按名称模糊搜索名词，并给出另一语言的译名与描述。
        /// 结果按「完全匹配优先、其次原文名称升序」排序；某条在译文库里查不到时
        /// <see cref="TranslationItem.Translation"/> 为 null（调用方据此提示"无译文"）。
        /// </summary>
        /// <param name="text">要翻译的名词（中/英均可）。</param>
        /// <param name="sourceIsChinese">原文语言是否为中文：true 从本地化库查，false 从主库查。</param>
        /// <param name="limitPerKind">每类名词（物品 / 星域 / 星系 / 空间站）的匹配上限。</param>
        public static List<TranslationItem> Search(string text, bool sourceIsChinese, int limitPerKind = DefaultLimitPerKind)
        {
            var results = new List<TranslationItem>();
            if (string.IsNullOrWhiteSpace(text) || !IsAvailable)
            {
                return results;
            }

            var keyword = text.Trim();
            limitPerKind = Math.Max(1, limitPerKind);
            try
            {
                results.AddRange(SearchInvTypes(keyword, sourceIsChinese, limitPerKind));
                results.AddRange(SearchRegions(keyword, sourceIsChinese, limitPerKind));
                results.AddRange(SearchSolarSystems(keyword, sourceIsChinese, limitPerKind));
                results.AddRange(SearchStations(keyword, sourceIsChinese, limitPerKind));
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            // 完全匹配排最前（OrderByDescending 是稳定排序，各类名词内部的名称顺序保持不变）
            return results
                .OrderByDescending(p => string.Equals(p.Query, keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        #region 每类名词：原文库模糊搜索 + 译文库批量取对照行

        private static List<TranslationItem> SearchInvTypes(string keyword, bool sourceIsChinese, int limit)
        {
            var results = new List<TranslationItem>();
            if (sourceIsChinese)
            {
                var sources = DBService.LocalDb.Queryable<InvTypeBase>()
                    .Where(p => p.TypeName.Contains(keyword)).Take(limit).ToList();
                if (sources.Count == 0)
                {
                    return results;
                }

                var ids = sources.Select(p => p.TypeID).Distinct().ToList();
                var targets = DBService.MainDb.Queryable<InvTypeBase>()
                    .Where(p => ids.Contains(p.TypeID)).ToList();
                foreach (var source in sources)
                {
                    var target = targets.FirstOrDefault(p => p.TypeID == source.TypeID);
                    results.Add(Create(source.TypeID, DataBaseItemType.InvType,
                        source.TypeName, target?.TypeName,
                        source.Description, target?.Description,
                        Chinese, English));
                }
            }
            else
            {
                var sources = DBService.MainDb.Queryable<InvTypeBase>()
                    .Where(p => p.TypeName.Contains(keyword)).Take(limit).ToList();
                if (sources.Count == 0)
                {
                    return results;
                }

                var ids = sources.Select(p => p.TypeID).Distinct().ToList();
                var targets = DBService.LocalDb.Queryable<InvTypeBase>()
                    .Where(p => ids.Contains(p.TypeID)).ToList();
                foreach (var source in sources)
                {
                    var target = targets.FirstOrDefault(p => p.TypeID == source.TypeID);
                    results.Add(Create(source.TypeID, DataBaseItemType.InvType,
                        source.TypeName, target?.TypeName,
                        source.Description, target?.Description,
                        English, Chinese));
                }
            }

            return OrderByName(results);
        }

        private static List<TranslationItem> SearchRegions(string keyword, bool sourceIsChinese, int limit)
        {
            var results = new List<TranslationItem>();
            if (sourceIsChinese)
            {
                var sources = DBService.LocalDb.Queryable<MapRegionBase>()
                    .Where(p => p.RegionName.Contains(keyword)).Take(limit).ToList();
                if (sources.Count == 0)
                {
                    return results;
                }

                var ids = sources.Select(p => p.RegionID).Distinct().ToList();
                var targets = DBService.MainDb.Queryable<MapRegionBase>()
                    .Where(p => ids.Contains(p.RegionID)).ToList();
                foreach (var source in sources)
                {
                    var target = targets.FirstOrDefault(p => p.RegionID == source.RegionID);
                    results.Add(Create(source.RegionID, DataBaseItemType.MapRegion,
                        source.RegionName, target?.RegionName, null, null, Chinese, English));
                }
            }
            else
            {
                var sources = DBService.MainDb.Queryable<MapRegionBase>()
                    .Where(p => p.RegionName.Contains(keyword)).Take(limit).ToList();
                if (sources.Count == 0)
                {
                    return results;
                }

                var ids = sources.Select(p => p.RegionID).Distinct().ToList();
                var targets = DBService.LocalDb.Queryable<MapRegionBase>()
                    .Where(p => ids.Contains(p.RegionID)).ToList();
                foreach (var source in sources)
                {
                    var target = targets.FirstOrDefault(p => p.RegionID == source.RegionID);
                    results.Add(Create(source.RegionID, DataBaseItemType.MapRegion,
                        source.RegionName, target?.RegionName, null, null, English, Chinese));
                }
            }

            return OrderByName(results);
        }

        private static List<TranslationItem> SearchSolarSystems(string keyword, bool sourceIsChinese, int limit)
        {
            var results = new List<TranslationItem>();
            if (sourceIsChinese)
            {
                var sources = DBService.LocalDb.Queryable<MapSolarSystemBase>()
                    .Where(p => p.SolarSystemName.Contains(keyword)).Take(limit).ToList();
                if (sources.Count == 0)
                {
                    return results;
                }

                var ids = sources.Select(p => p.SolarSystemID).Distinct().ToList();
                var targets = DBService.MainDb.Queryable<MapSolarSystemBase>()
                    .Where(p => ids.Contains(p.SolarSystemID)).ToList();
                foreach (var source in sources)
                {
                    var target = targets.FirstOrDefault(p => p.SolarSystemID == source.SolarSystemID);
                    results.Add(Create(source.SolarSystemID, DataBaseItemType.MapSolarSystem,
                        source.SolarSystemName, target?.SolarSystemName, null, null, Chinese, English));
                }
            }
            else
            {
                var sources = DBService.MainDb.Queryable<MapSolarSystemBase>()
                    .Where(p => p.SolarSystemName.Contains(keyword)).Take(limit).ToList();
                if (sources.Count == 0)
                {
                    return results;
                }

                var ids = sources.Select(p => p.SolarSystemID).Distinct().ToList();
                var targets = DBService.LocalDb.Queryable<MapSolarSystemBase>()
                    .Where(p => ids.Contains(p.SolarSystemID)).ToList();
                foreach (var source in sources)
                {
                    var target = targets.FirstOrDefault(p => p.SolarSystemID == source.SolarSystemID);
                    results.Add(Create(source.SolarSystemID, DataBaseItemType.MapSolarSystem,
                        source.SolarSystemName, target?.SolarSystemName, null, null, English, Chinese));
                }
            }

            return OrderByName(results);
        }

        private static List<TranslationItem> SearchStations(string keyword, bool sourceIsChinese, int limit)
        {
            var results = new List<TranslationItem>();
            if (sourceIsChinese)
            {
                var sources = DBService.LocalDb.Queryable<StaStationBase>()
                    .Where(p => p.StationName.Contains(keyword)).Take(limit).ToList();
                if (sources.Count == 0)
                {
                    return results;
                }

                var ids = sources.Select(p => p.StationID).Distinct().ToList();
                var targets = DBService.MainDb.Queryable<StaStationBase>()
                    .Where(p => ids.Contains(p.StationID)).ToList();
                foreach (var source in sources)
                {
                    var target = targets.FirstOrDefault(p => p.StationID == source.StationID);
                    results.Add(Create(source.StationID, DataBaseItemType.StaStation,
                        source.StationName, target?.StationName, null, null, Chinese, English));
                }
            }
            else
            {
                var sources = DBService.MainDb.Queryable<StaStationBase>()
                    .Where(p => p.StationName.Contains(keyword)).Take(limit).ToList();
                if (sources.Count == 0)
                {
                    return results;
                }

                var ids = sources.Select(p => p.StationID).Distinct().ToList();
                var targets = DBService.LocalDb.Queryable<StaStationBase>()
                    .Where(p => ids.Contains(p.StationID)).ToList();
                foreach (var source in sources)
                {
                    var target = targets.FirstOrDefault(p => p.StationID == source.StationID);
                    results.Add(Create(source.StationID, DataBaseItemType.StaStation,
                        source.StationName, target?.StationName, null, null, English, Chinese));
                }
            }

            return OrderByName(results);
        }

        #endregion

        private static List<TranslationItem> OrderByName(List<TranslationItem> items)
            => items.OrderBy(p => p.Query, StringComparer.OrdinalIgnoreCase).ToList();

        private static TranslationItem Create(
            int id,
            DataBaseItemType itemType,
            string query,
            string translation,
            string queryDescription,
            string translationDescription,
            string from,
            string to)
        {
            return new TranslationItem
            {
                ID = id,
                DataBaseItemType = itemType,
                Query = query,
                Translation = translation,
                QueryDescription = queryDescription,
                TranslationDescription = translationDescription,
                From = from,
                To = to,
                IsFromDataBase = true,
            };
        }
    }
}
