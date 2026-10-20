using System.IO;
using EVEStandard.Enumerations;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>某星系的工业成本指数（按活动）。</summary>
public sealed class IndustryCostIndex
{
    public long SolarSystemId { get; set; }
    public double Manufacturing { get; set; }
    public double Reaction { get; set; }
    public double Invention { get; set; }
}

/// <summary>
/// 工业计算所需的公开市场数据：
/// <list type="bullet">
///   <item><c>/markets/prices</c> → 调整价（EIV）与平均价（买/建对比）；</item>
///   <item><c>/industry/systems</c> → 各星系成本指数（制造 / 反应 / 发明）。</item>
/// </list>
/// 匿名 ESI，磁盘缓存 <c>Configs/IndustryCache/*.json</c>（TTL 1 小时）。失败时返回上次缓存。
/// </summary>
public sealed class IndustryMarketDataService
{
    private static IndustryMarketDataService? _current;

    public static IndustryMarketDataService Current => _current ??= new IndustryMarketDataService();

    private readonly EVEStandard.EVEStandardAPI _esi;

    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    public IndustryMarketDataService()
    {
        var dataSource = GameServerSelectorService.Value == GameServerType.Tranquility
            ? DataSource.Tranquility
            : DataSource.Serenity;
        _esi = new EVEStandard.EVEStandardAPI(
            "TheGuideToTheNewEden",
            dataSource,
            CompatibilityDate.v2025_12_16,
            TimeSpan.FromSeconds(30));
    }

    private static string Folder => Path.Combine(SettingsService.DataPath, "Configs", "IndustryCache");
    private static string PricesFile => Path.Combine(Folder, "prices.json");
    private static string CostIndexFile => Path.Combine(Folder, "costindices.json");

    /// <summary>CCP 调整价（TypeID → adjusted_price）。失败/无缓存返回空字典。</summary>
    public async Task<Dictionary<int, double>> GetAdjustedPricesAsync(bool forceRefresh = false)
    {
        var cache = forceRefresh ? null : ReadCache<PricesCache>(PricesFile);
        if (cache != null && cache.AdjustedPrices is { Count: > 0 } && IsFresh(cache.UpdateTime))
        {
            return cache.AdjustedPrices;
        }

        try
        {
            var response = await _esi.Market.ListMarketPricesAsync(null);
            var adjusted = new Dictionary<int, double>();
            var average = new Dictionary<int, double>();
            if (response?.Model != null)
            {
                foreach (var price in response.Model)
                {
                    if (price.TypeId <= 0) continue;
                    if (price.AdjustedPrice.HasValue && price.AdjustedPrice.Value > 0)
                    {
                        adjusted[(int)price.TypeId] = price.AdjustedPrice.Value;
                    }
                    if (price.AveragePrice.HasValue && price.AveragePrice.Value > 0)
                    {
                        average[(int)price.TypeId] = price.AveragePrice.Value;
                    }
                }
            }

            if (adjusted.Count > 0)
            {
                WriteCache(PricesFile, new PricesCache
                {
                    UpdateTime = DateTime.UtcNow,
                    AdjustedPrices = adjusted,
                    AveragePrices = average,
                });
                return adjusted;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        return cache?.AdjustedPrices ?? new Dictionary<int, double>();
    }

    /// <summary>市场平均价（TypeID → average_price），用于买/建对比。</summary>
    public async Task<Dictionary<int, double>> GetAveragePricesAsync(bool forceRefresh = false)
    {
        await GetAdjustedPricesAsync(forceRefresh);
        var cache = ReadCache<PricesCache>(PricesFile);
        return cache?.AveragePrices ?? new Dictionary<int, double>();
    }

    /// <summary>各星系成本指数（SolarSystemID → 指数）。失败/无缓存返回空字典。</summary>
    public async Task<Dictionary<long, IndustryCostIndex>> GetCostIndicesAsync(bool forceRefresh = false)
    {
        var cache = forceRefresh ? null : ReadCache<CostIndexCache>(CostIndexFile);
        if (cache != null && cache.Indices is { Count: > 0 } && IsFresh(cache.UpdateTime))
        {
            return cache.Indices;
        }

        try
        {
            var response = await _esi.Industry.ListSolarSystemCostIndiciesAsync(null);
            var map = new Dictionary<long, IndustryCostIndex>();
            if (response?.Model != null)
            {
                foreach (var system in response.Model)
                {
                    if (system.SolarSystemId <= 0 || system.CostIndices == null) continue;
                    var index = new IndustryCostIndex { SolarSystemId = system.SolarSystemId };
                    foreach (var item in system.CostIndices)
                    {
                        if (string.IsNullOrEmpty(item.Activity)) continue;
                        if (item.Activity.Equals("manufacturing", StringComparison.OrdinalIgnoreCase))
                        {
                            index.Manufacturing = item.CostIndex;
                        }
                        else if (item.Activity.Equals("reaction", StringComparison.OrdinalIgnoreCase))
                        {
                            index.Reaction = item.CostIndex;
                        }
                        else if (item.Activity.Equals("invention", StringComparison.OrdinalIgnoreCase))
                        {
                            index.Invention = item.CostIndex;
                        }
                    }
                    map[system.SolarSystemId] = index;
                }
            }

            if (map.Count > 0)
            {
                WriteCache(CostIndexFile, new CostIndexCache { UpdateTime = DateTime.UtcNow, Indices = map });
                return map;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        return cache?.Indices ?? new Dictionary<long, IndustryCostIndex>();
    }

    // ───────────── 缓存 ─────────────

    private static bool IsFresh(DateTime updateTime) => updateTime > DateTime.UtcNow - Ttl;

    private static T? ReadCache<T>(string file) where T : class
    {
        try
        {
            if (!File.Exists(file)) return null;
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(file));
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    private static void WriteCache<T>(string file, T value)
    {
        try
        {
            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(file, JsonConvert.SerializeObject(value));
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private sealed class PricesCache
    {
        public DateTime UpdateTime { get; set; }
        public Dictionary<int, double> AdjustedPrices { get; set; } = new();
        public Dictionary<int, double> AveragePrices { get; set; } = new();
    }

    private sealed class CostIndexCache
    {
        public DateTime UpdateTime { get; set; }
        public Dictionary<long, IndustryCostIndex> Indices { get; set; } = new();
    }
}
