using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Market;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>
/// 估价设置。估价方式沿用倒货的 <see cref="ScalperSetting.PriceType"/> 口径枚举
/// （卖单最低价/前5%均价/实际数量价、买单最高价/前5%均价/实际数量价、历史最高/平均/最低/中位数），
/// 并在口径价的基础上乘以百分比（如"按卖单最低价的 95% 估价"）。
/// 价格来源（市场位置）支持 星域 / 星系 / 建筑（与倒货的位置选择器同模型）。
/// </summary>
public sealed class AppraisalSetting
{
    /// <summary>
    /// 估价的价格来源。默认伏尔戈（吉他所在星域）；
    /// 旧版配置只有 <see cref="RegionId"/> 字段，由 <see cref="AppraisalSettingService.Load"/> 迁移。
    /// </summary>
    public MarketLocation? Location { get; set; }

    /// <summary>旧版估价星域（仅作旧配置迁移，新配置一律写 <see cref="Location"/>）。</summary>
    public int RegionId { get; set; } = MarketOrderService.DefaultMarketRegion;

    /// <summary>卖出估价口径（估"这船东西卖掉能得多少"）。</summary>
    public ScalperSetting.PriceType SellPriceType { get; set; } = ScalperSetting.PriceType.SellTop;

    /// <summary>卖出估价百分比（%）。100 = 按口径原价。</summary>
    public double SellPercent { get; set; } = 100;

    /// <summary>买入估价口径（估"按买单回收能得多少"）。</summary>
    public ScalperSetting.PriceType BuyPriceType { get; set; } = ScalperSetting.PriceType.BuyTop;

    /// <summary>买入估价百分比（%）。</summary>
    public double BuyPercent { get; set; } = 100;

    /// <summary>历史口径的统计天数（History* 估价方式使用）。</summary>
    public int HistoryDay { get; set; } = 7;

    /// <summary>历史口径去极值（去掉一个最值后求均值，与倒货一致）。</summary>
    public bool RemoveExtremum { get; set; } = true;
}

/// <summary>
/// 估价设置的持久化：<c>Configs/AppraisalSetting.json</c>（WPF 版专有，与 WinUI 版的第三方 API 方案无关）。
/// </summary>
public static class AppraisalSettingService
{
    private static readonly string Folder = Path.Combine(SettingsService.DataPath, "Configs");

    private static readonly string FilePath = Path.Combine(Folder, "AppraisalSetting.json");

    /// <summary>读取设置；文件不存在或损坏时返回默认设置。旧版仅有星域的配置迁移为 <see cref="MarketLocation"/>。</summary>
    public static AppraisalSetting Load()
    {
        AppraisalSetting? setting = null;
        if (File.Exists(FilePath))
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    setting = JsonConvert.DeserializeObject<AppraisalSetting>(json);
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        setting ??= new AppraisalSetting();
        MigrateLegacyRegion(setting);
        return setting;
    }

    /// <summary>旧版配置只有 RegionId：迁移成星域位置（不用 MarketLocation(MapRegion) 构造，它对无星系星域会抛异常）。</summary>
    private static void MigrateLegacyRegion(AppraisalSetting setting)
    {
        if (setting.Location is not null || setting.RegionId <= 0)
        {
            return;
        }

        var region = Core.Services.DB.MapRegionService.Query(setting.RegionId);
        if (region is not null)
        {
            setting.Location = new MarketLocation
            {
                Type = MarketLocationType.Region,
                Id = region.RegionID,
                MarketObj = region,
                Name = region.RegionName,
                RegionId = region.RegionID,
                SolarSystemId = Core.Services.DB.MapSolarSystemService.QueryByRegionID(region.RegionID).FirstOrDefault()?.SolarSystemID ?? 0,
            };
        }
    }

    public static void Save(AppraisalSetting setting)
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
}
