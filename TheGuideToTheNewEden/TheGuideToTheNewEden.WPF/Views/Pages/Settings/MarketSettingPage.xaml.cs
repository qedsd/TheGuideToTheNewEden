using System.IO;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>市场设置：缓存有效期、线程数、是否跳过结构、清理缓存。</summary>
public partial class MarketSettingPage : Page
{
    public MarketSettingPage()
    {
        InitializeComponent();

        OrderDurationBox.Value = MarketOrderSettingService.OrderDurationValue;
        OrderDurationBox.ValueChanged += (_, _) =>
            MarketOrderSettingService.OrderDurationValue = (int)OrderDurationBox.Value;

        HistoryDurationBox.Value = MarketOrderSettingService.HistoryDurationValue;
        HistoryDurationBox.ValueChanged += (_, _) =>
            MarketOrderSettingService.HistoryDurationValue = (int)HistoryDurationBox.Value;

        MaxThreadBox.Value = MarketOrderSettingService.ThreadValue;
        MaxThreadBox.ValueChanged += (_, _) =>
            MarketOrderSettingService.ThreadValue = (int)MaxThreadBox.Value;

        ScalperSkipToggle.IsChecked = MarketOrderSettingService.ScalperSikpStructureValue;
        ScalperSkipToggle.Checked += (_, _) => MarketOrderSettingService.ScalperSikpStructureValue = true;
        ScalperSkipToggle.Unchecked += (_, _) => MarketOrderSettingService.ScalperSikpStructureValue = false;

        MarketSkipToggle.IsChecked = MarketOrderSettingService.MarketSikpStructureValue;
        MarketSkipToggle.Checked += (_, _) => MarketOrderSettingService.MarketSikpStructureValue = true;
        MarketSkipToggle.Unchecked += (_, _) => MarketOrderSettingService.MarketSikpStructureValue = false;

        ClearCacheButton.Click += (_, _) => ClearCache();
    }

    private void ClearCache()
    {
        var folders = new[]
        {
            MarketOrderSettingService.StructureOrderFolder,
            MarketOrderSettingService.RegionOrderFolder,
            MarketOrderSettingService.HistoryOrderFolder,
        };

        foreach (var folder in folders)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        MessageBox.Show(
            Application.Current.TryFindResource("MarketSettingPage_ClearCache_Done") as string ?? "已清除缓存订单信息",
            Application.Current.TryFindResource("SettingPage_Market") as string ?? "市场",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}