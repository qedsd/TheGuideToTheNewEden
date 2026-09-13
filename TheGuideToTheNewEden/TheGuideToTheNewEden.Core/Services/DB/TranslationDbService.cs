using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.Translation;

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

        #region 术语表抽取（供 AI 翻译注入与后校验）

        /// <summary>
        /// 抽取全量中英术语对（物品 / 星域 / 星系 / 空间站）：
        /// 主库取 <c>Id + 名称</c>（物品另取 MarketGroupID 用于排序），本地化库取 <c>Id + 名称</c>，
        /// 双方按 Id 配对，**丢弃中英相同的"未翻译"行**。
        /// <para>实测规模：types 约 5.2 万 + 星系 8500 + 空间站 5200 + 星域 114，整表读入约 0.5–1 秒。</para>
        /// </summary>
        /// <param name="progress">进度回调（已完成类别数 / 总类别数）。</param>
        public static List<GlossaryEntry> ExtractGlossary(Action<int, int>? progress = null, CancellationToken cancellationToken = default)
        {
            var entries = new List<GlossaryEntry>();
            if (!IsAvailable)
            {
                return entries;
            }

            const int totalSteps = 4;
            var step = 0;
            try
            {
                ExtractInvTypes(entries, cancellationToken);
                progress?.Invoke(++step, totalSteps);

                ExtractNamed(entries, DBService.MainDb.Queryable<MapRegionBase>().Select(p => new NamedRow { Id = p.RegionID, Name = p.RegionName }).ToList(),
                    DBService.LocalDb.Queryable<MapRegionBase>().Select(p => new NamedRow { Id = p.RegionID, Name = p.RegionName }).ToList(),
                    DataBaseItemType.MapRegion);
                progress?.Invoke(++step, totalSteps);

                ExtractNamed(entries, DBService.MainDb.Queryable<MapSolarSystemBase>().Select(p => new NamedRow { Id = p.SolarSystemID, Name = p.SolarSystemName }).ToList(),
                    DBService.LocalDb.Queryable<MapSolarSystemBase>().Select(p => new NamedRow { Id = p.SolarSystemID, Name = p.SolarSystemName }).ToList(),
                    DataBaseItemType.MapSolarSystem);
                progress?.Invoke(++step, totalSteps);

                ExtractNamed(entries, DBService.MainDb.Queryable<StaStationBase>().Select(p => new NamedRow { Id = p.StationID, Name = p.StationName }).ToList(),
                    DBService.LocalDb.Queryable<StaStationBase>().Select(p => new NamedRow { Id = p.StationID, Name = p.StationName }).ToList(),
                    DataBaseItemType.StaStation);
                progress?.Invoke(++step, totalSteps);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            return entries;
        }

        private static void ExtractInvTypes(List<GlossaryEntry> entries, CancellationToken cancellationToken)
        {
            var mainRows = DBService.MainDb.Queryable<InvType>()
                .Select(p => new InvTypeRow { Id = p.TypeID, Name = p.TypeName, MarketGroupId = p.MarketGroupID })
                .ToList();
            cancellationToken.ThrowIfCancellationRequested();

            var localNames = ToNameMap(DBService.LocalDb.Queryable<InvTypeBase>()
                .Select(p => new NamedRow { Id = p.TypeID, Name = p.TypeName })
                .ToList());

            foreach (var row in mainRows)
            {
                if (!TryPair(row.Id, row.Name, localNames, out var chinese))
                {
                    continue;
                }

                entries.Add(new GlossaryEntry
                {
                    Id = row.Id,
                    Kind = DataBaseItemType.InvType,
                    English = row.Name.Trim(),
                    Chinese = chinese,
                    IsMarketItem = row.MarketGroupId != null,
                });
            }
        }

        private static void ExtractNamed(List<GlossaryEntry> entries, List<NamedRow> mainRows, List<NamedRow> localRows, DataBaseItemType kind)
        {
            var localNames = ToNameMap(localRows);
            foreach (var row in mainRows)
            {
                if (!TryPair(row.Id, row.Name, localNames, out var chinese))
                {
                    continue;
                }

                entries.Add(new GlossaryEntry
                {
                    Id = row.Id,
                    Kind = kind,
                    English = row.Name.Trim(),
                    Chinese = chinese,
                });
            }
        }

        private static Dictionary<int, string> ToNameMap(List<NamedRow> rows)
        {
            var map = new Dictionary<int, string>(rows.Count);
            foreach (var row in rows)
            {
                if (!string.IsNullOrWhiteSpace(row.Name))
                {
                    map[row.Id] = row.Name.Trim();
                }
            }

            return map;
        }

        /// <summary>配对成功条件：两侧都非空且不相等（相等说明本地化库那一行没翻译）。</summary>
        private static bool TryPair(int id, string english, Dictionary<int, string> localNames, out string chinese)
        {
            chinese = null;
            if (string.IsNullOrWhiteSpace(english) || !localNames.TryGetValue(id, out var local) || string.IsNullOrWhiteSpace(local))
            {
                return false;
            }

            if (string.Equals(english.Trim(), local, StringComparison.Ordinal))
            {
                return false;
            }

            chinese = local;
            return true;
        }

        /// <summary>SqlSugar 投影用的轻量行（避免把 Description 这些大字段读进来）。</summary>
        private sealed class NamedRow
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class InvTypeRow
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public int? MarketGroupId { get; set; }
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
