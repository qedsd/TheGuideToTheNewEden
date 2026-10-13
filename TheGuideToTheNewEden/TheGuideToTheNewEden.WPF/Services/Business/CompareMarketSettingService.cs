using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>比价清单里的一件物品（持久化用）。</summary>
public sealed class CompareMarketItemSetting
{
    public int TypeId { get; set; }

    public double Amount { get; set; } = 1;

    public CompareItemGroup Group { get; set; }
}

/// <summary>
/// 比价设置：市场列表（可多个）、物品清单（含数量与分组）、两侧价格口径与成交方式、卖出税费、分地阈值、利差口径。
/// <para>
/// 价格口径沿用倒货/估价的 <see cref="ScalperSetting.PriceType"/>，但比价**只支持订单类 6 项**
/// （比价是"找地点"，用现价即可；历史口径会让取数量变 4 倍且显著变慢）。读到历史枚举值时回落默认。
/// </para>
/// </summary>
public sealed class CompareMarketSetting
{
    /// <summary>最多可添加的市场数（防误操作与取数时长失控）。</summary>
    public const int MaxMarkets = 12;

    /// <summary>市场列表（星域 / 星系 / 建筑，至少 1 个才有意义）。</summary>
    public List<MarketLocation> Markets { get; set; } = [];

    /// <summary>物品清单（同一 TypeId 只出现一次）。</summary>
    public List<CompareMarketItemSetting> Items { get; set; } = [];

    /// <summary>卖单侧价格口径（该市场现有卖单的参考价）。</summary>
    public ScalperSetting.PriceType SellPriceType { get; set; } = ScalperSetting.PriceType.SellTop5;

    /// <summary>买单侧价格口径（该市场现有买单的参考价）。</summary>
    public ScalperSetting.PriceType BuyPriceType { get; set; } = ScalperSetting.PriceType.BuyTop5;

    /// <summary>卖出方式（默认挂卖单）。</summary>
    public CompareTradeMode SellMode { get; set; } = CompareTradeMode.MakeOrder;

    /// <summary>买入方式（默认从卖单买入）。</summary>
    public CompareTradeMode BuyMode { get; set; } = CompareTradeMode.TakeOrder;

    /// <summary>卖出税费 %（销售税 + 经纪费合计；0 = 不计税，只作用于卖出侧）。</summary>
    public double SellTaxPercent { get; set; }

}

/// <summary>
/// 比价设置的持久化：<c>Configs/CompareMarketSetting.json</c>（WPF 专有，不影响 WinUI 与 Core）。
/// </summary>
public static class CompareMarketSettingService
{
    private static readonly string Folder = Path.Combine(SettingsService.DataPath, "Configs");

    private static readonly string FilePath = Path.Combine(Folder, "CompareMarketSetting.json");

    /// <summary>读取设置；文件不存在或损坏时返回默认设置，并做防御性修补（见 <see cref="Sanitize"/>）。</summary>
    public static CompareMarketSetting Load()
    {
        CompareMarketSetting? setting = null;
        if (File.Exists(FilePath))
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    setting = JsonConvert.DeserializeObject<CompareMarketSetting>(json);
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        setting ??= new CompareMarketSetting();
        Sanitize(setting);
        return setting;
    }

    public static void Save(CompareMarketSetting setting)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(setting));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>
    /// 修补：市场去重/丢弃未选择项/补 SolarSystemId/夹上限；清单去重、数量合法化；
    /// 历史口径回落订单默认；百分比夹到 0..100。
    /// </summary>
    private static void Sanitize(CompareMarketSetting setting)
    {
        var markets = new List<MarketLocation>();
        var seen = new HashSet<string>();
        foreach (var market in setting.Markets ?? [])
        {
            if (market is null || market.Id <= 0 || market.Type is not (MarketLocationType.Region or MarketLocationType.SolarSystem or MarketLocationType.Structure))
            {
                continue;
            }

            // 同一个市场只保留一次（Type + Id）
            if (!seen.Add($"{market.Type}:{market.Id}"))
            {
                continue;
            }

            // 旧配置/手工编辑可能缺 SolarSystemId（后续按星系过滤与跳数都依赖它）
            if (market.SolarSystemId <= 0)
            {
                market.SolarSystemId = ResolveSolarSystemId(market) ?? 0;
            }

            markets.Add(market);
            if (markets.Count >= CompareMarketSetting.MaxMarkets)
            {
                break;
            }
        }

        setting.Markets = markets;

        var items = new List<CompareMarketItemSetting>();
        var seenItems = new HashSet<string>();
        foreach (var item in setting.Items ?? [])
        {
            // 同一物品允许同时出现在"卖出组"与"买入组"（两组是独立清单），因此去重键必须带上分组，
            // 否则重启加载时会把买入组里的同一物品丢掉。
            if (item is null || item.TypeId <= 0 || !seenItems.Add($"{item.TypeId}:{(int)item.Group}"))
            {
                continue;
            }

            if (item.Amount <= 0)
            {
                item.Amount = 1;
            }

            items.Add(item);
        }

        setting.Items = items;

        // 比价不提供历史口径，且两侧口径各自只作用于对应订单侧：
        // 手改 JSON、旧版本写入的其它值（历史口径、买卖错侧）一律回落默认
        if (!IsSellSidePriceType(setting.SellPriceType))
        {
            setting.SellPriceType = ScalperSetting.PriceType.SellTop5;
        }

        if (!IsBuySidePriceType(setting.BuyPriceType))
        {
            setting.BuyPriceType = ScalperSetting.PriceType.BuyTop5;
        }

        setting.SellTaxPercent = Math.Clamp(setting.SellTaxPercent, 0, 100);
    }

    /// <summary>卖单侧口径（只作用于该市场的卖单：前 5% 均价 / 最低价 / 按数量吃单均价）。</summary>
    public static bool IsSellSidePriceType(ScalperSetting.PriceType type)
        => type is ScalperSetting.PriceType.SellTop
            or ScalperSetting.PriceType.SellTop5
            or ScalperSetting.PriceType.SellAvailable;

    /// <summary>买单侧口径（只作用于该市场的买单：前 5% 均价 / 最高价 / 按数量吃单均价）。</summary>
    public static bool IsBuySidePriceType(ScalperSetting.PriceType type)
        => type is ScalperSetting.PriceType.BuyTop
            or ScalperSetting.PriceType.BuyTop5
            or ScalperSetting.PriceType.BuyAvailable;

    /// <summary>按数量逐档吃单的口径（价格随数量变化，数量变更后需要重新比价）。</summary>
    public static bool IsAmountDependentPriceType(ScalperSetting.PriceType type)
        => type is ScalperSetting.PriceType.SellAvailable or ScalperSetting.PriceType.BuyAvailable;

    /// <summary>补齐市场位置所在星系（星域取首个星系、建筑取结构所在星系；不用 MarketLocation(MapRegion) 构造——它对无星系星域会抛异常）。</summary>
    private static int? ResolveSolarSystemId(MarketLocation location) => location.Type switch
    {
        MarketLocationType.SolarSystem => (int)location.Id,
        MarketLocationType.Region => Core.Services.DB.MapSolarSystemService.QueryByRegionID((int)location.Id).FirstOrDefault()?.SolarSystemID,
        MarketLocationType.Structure => StructureService.GetStructure(location.Id)?.SolarSystemId,
        _ => null,
    };
}
