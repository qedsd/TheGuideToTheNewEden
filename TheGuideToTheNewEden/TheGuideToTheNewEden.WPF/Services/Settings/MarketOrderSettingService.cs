using System.IO;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>市场相关设置（订单/历史有效期、线程数、是否跳过结构）。</summary>
public static class MarketOrderSettingService
{
    private const string OrderDurationKey = "OrderDuration";
    private const string HistoryDurationKey = "HistoryDuration";
    private const string ThreadKey = "Thread";
    private const string ScalperSikpStructureKey = "ScalperSikpStructure";
    private const string MarketSikpStructureKey = "MarketSikpStructure";

    public static int OrderDurationValue
    {
        get => SettingsService.GetInt(OrderDurationKey, 60);
        set => SettingsService.SetInt(OrderDurationKey, value);
    }

    public static int HistoryDurationValue
    {
        get => SettingsService.GetInt(HistoryDurationKey, 60);
        set => SettingsService.SetInt(HistoryDurationKey, value);
    }

    public static int ThreadValue
    {
        get => SettingsService.GetInt(ThreadKey, 4);
        set => SettingsService.SetInt(ThreadKey, value);
    }

    public static bool ScalperSikpStructureValue
    {
        get => SettingsService.GetBool(ScalperSikpStructureKey, true);
        set => SettingsService.SetBool(ScalperSikpStructureKey, value);
    }

    public static bool MarketSikpStructureValue
    {
        get => SettingsService.GetBool(MarketSikpStructureKey, true);
        set => SettingsService.SetBool(MarketSikpStructureKey, value);
    }

    public static readonly string StructureOrderFolder = Path.Combine(SettingsService.DataPath, "Configs", "StructureOrders");
    public static readonly string RegionOrderFolder = Path.Combine(SettingsService.DataPath, "Configs", "RegionOrders");
    public static readonly string HistoryOrderFolder = Path.Combine(SettingsService.DataPath, "Configs", "HistoryOrders");
}