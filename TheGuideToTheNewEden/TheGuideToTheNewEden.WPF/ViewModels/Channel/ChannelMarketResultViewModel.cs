using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.ViewModels.Channel;

/// <summary>
/// 频道查价结果窗内容：多物品 = 汇总（卖/买 × 5%/最优）+ 物品卡片列表；
/// 单物品 = 物品卡片 + 近三个月价格曲线（LiveCharts，配色随主题切换重刷）。
/// 取价走 <see cref="MarketOrderService"/>（星域订单 + 历史，带 TTL 缓存）。
/// </summary>
public sealed class ChannelMarketResultViewModel : INotifyPropertyChanged
{
    private readonly LineSeries<DateTimePoint> _highestSeries = new() { Name = "Highest", GeometrySize = 0 };
    private readonly LineSeries<DateTimePoint> _averageSeries = new() { Name = "Average", GeometrySize = 0 };
    private readonly LineSeries<DateTimePoint> _lowestSeries = new() { Name = "Lowest", GeometrySize = 0 };
    private readonly Axis _xAxis;
    private readonly Axis _yAxis;

    private IEnumerable<MarketChatContent> _contents = [];

    public ObservableCollection<ChannelMarketResult> Results { get; } = [];

    private ChannelMarketResult? _result;

    /// <summary>单物品结果。</summary>
    public ChannelMarketResult? Result
    {
        get => _result;
        private set
        {
            if (Set(ref _result, value))
            {
                ApplyStatistics();
            }
        }
    }

    private int _itemCount;

    public int ItemCount
    {
        get => _itemCount;
        private set => Set(ref _itemCount, value);
    }

    private bool _multiItem;

    public bool MultiItem
    {
        get => _multiItem;
        private set => Set(ref _multiItem, value);
    }

    public double Sell5P { get; private set; }
    public double Buy5P { get; private set; }
    public double SellTop { get; private set; }
    public double BuyTop { get; private set; }

    public ISeries[] PriceSeries { get; }

    public Axis[] XAxes { get; }

    public Axis[] YAxes { get; }

    public ChannelMarketResultViewModel()
    {
        _highestSeries.Fill = null;
        _averageSeries.Fill = null;
        _lowestSeries.Fill = null;
        PriceSeries = [_highestSeries, _averageSeries, _lowestSeries];
        _xAxis = new DateTimeAxis(TimeSpan.FromDays(1), d => d.ToString("yyyy.MM.dd")) { TextSize = 12 };
        _yAxis = new Axis { Labeler = v => v.ToString("N2"), TextSize = 12 };
        XAxes = [_xAxis];
        YAxes = [_yAxis];
        ApplyThemeColors();
        ThemeService.ThemeChanged += ApplyThemeColors;
    }

    /// <summary>查询并填充结果（对齐 WinUI 版 ChannelMarketWinViewModel.UpdateContent）。</summary>
    public async Task UpdateContentAsync(IEnumerable<MarketChatContent> marketChatContents, int regionId)
    {
        _contents = marketChatContents;
        _lastRegionId = regionId;
        ItemCount = _contents.Sum(p => p.Items.Count);
        MultiItem = ItemCount > 1;
        Results.Clear();
        Result = null;
        PageNotifyService.ShowWaiting(FindString("ChannelMarketPage_Running"));
        try
        {
            if (MultiItem)
            {
                foreach (var content in _contents)
                {
                    foreach (var item in content.Items)
                    {
                        var orders = await MarketOrderService.Current.GetRegionOrdersAsync(item.TypeID, regionId);
                        var buyOrders = orders?.Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price).ToList();
                        var sellOrders = orders?.Where(p => !p.IsBuyOrder).OrderBy(p => p.Price).ToList();
                        var statistics = await MarketOrderService.Current.GetHistoryAsync(item.TypeID, regionId);
                        Results.Add(new ChannelMarketResult(item, sellOrders, buyOrders, statistics));
                    }
                }

                Sell5P = Results.Sum(p => p.Sell5P);
                Buy5P = Results.Sum(p => p.Buy5P);
                SellTop = Results.Sum(p => p.SellTop);
                BuyTop = Results.Sum(p => p.BuyTop);
                OnPropertyChanged(nameof(Sell5P));
                OnPropertyChanged(nameof(Buy5P));
                OnPropertyChanged(nameof(SellTop));
                OnPropertyChanged(nameof(BuyTop));
            }
            else
            {
                var item = _contents.First().Items[0];
                var orders = await MarketOrderService.Current.GetRegionOrdersAsync(item.TypeID, regionId);
                var buyOrders = orders?.Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price).ToList();
                var sellOrders = orders?.Where(p => !p.IsBuyOrder).OrderBy(p => p.Price).ToList();
                var statistics = await MarketOrderService.Current.GetHistoryAsync(item.TypeID, regionId);
                Result = new ChannelMarketResult(item, sellOrders, buyOrders, statistics);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
        finally
        {
            PageNotifyService.HideWaiting();
        }
    }

    /// <summary>重新查询（同一批内容与星域）。</summary>
    public Task RefreshAsync() => UpdateContentAsync(_contents, _lastRegionId);

    private int _lastRegionId = MarketOrderService.DefaultMarketRegion;

    private void ApplyStatistics()
    {
        var rows = (Result?.StatisticsForShow ?? []).OrderBy(p => p.Date).ToList();
        _highestSeries.Values = rows.Select(p => new DateTimePoint(p.Date, p.Highest)).ToArray();
        _averageSeries.Values = rows.Select(p => new DateTimePoint(p.Date, p.Average)).ToArray();
        _lowestSeries.Values = rows.Select(p => new DateTimePoint(p.Date, p.Lowest)).ToArray();
    }

    /// <summary>图表配色取自主题资源；主题切换后重新调用（LiveCharts 无法用 DynamicResource）。</summary>
    public void ApplyThemeColors()
    {
        if (MakePaint("SystemFillColorCriticalBrush", 2) is { } highest)
        {
            _highestSeries.Stroke = highest;
        }

        if (MakePaint("SystemAccentColorPrimaryBrush", 2) is { } average)
        {
            _averageSeries.Stroke = average;
        }

        if (MakePaint("SystemFillColorSuccessBrush", 2) is { } lowest)
        {
            _lowestSeries.Stroke = lowest;
        }

        if (MakePaint("TextFillColorSecondaryBrush", 1) is { } labelPaint)
        {
            _xAxis.LabelsPaint = labelPaint;
            _yAxis.LabelsPaint = labelPaint;
        }

        if (MakePaint("DividerStrokeColorDefaultBrush", 1) is { } separatorPaint)
        {
            _xAxis.SeparatorsPaint = separatorPaint;
            _yAxis.SeparatorsPaint = separatorPaint;
        }
    }

    private static SolidColorPaint? MakePaint(string resourceKey, double thickness)
    {
        if (Application.Current?.TryFindResource(resourceKey) is not System.Windows.Media.SolidColorBrush brush)
        {
            return null;
        }

        var c = brush.Color;
        return new SolidColorPaint(new SKColor(c.R, c.G, c.B, c.A), (float)thickness);
    }

    // ---------- INotifyPropertyChanged ----------

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
