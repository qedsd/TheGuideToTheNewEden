using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services;
using Wpf.Ui.Controls;
using Statistic = TheGuideToTheNewEden.Core.Models.Market.Statistic;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 倒货物品详情窗：左侧全部指标，右侧源/目的市场的卖单、买单与历史图表（LiveCharts）。
/// 同一个窗口实例可反复 <see cref="SetItem"/> 切换物品（对齐 WinUI 版 ScalperItemDetailWindow）。
/// </summary>
public partial class ScalperItemDetailWindow : FluentWindow
{
    private static readonly HttpClient Http = new();

    private readonly LineSeries<DateTimePoint> _srcHighest = NewLine("Highest");
    private readonly LineSeries<DateTimePoint> _srcAverage = NewLine("Average");
    private readonly LineSeries<DateTimePoint> _srcLowest = NewLine("Lowest");
    private readonly LineSeries<DateTimePoint> _srcVolume = NewLine("Volume");
    private readonly LineSeries<DateTimePoint> _dstHighest = NewLine("Highest");
    private readonly LineSeries<DateTimePoint> _dstAverage = NewLine("Average");
    private readonly LineSeries<DateTimePoint> _dstLowest = NewLine("Lowest");
    private readonly LineSeries<DateTimePoint> _dstVolume = NewLine("Volume");

    private readonly Axis _srcXAxis = NewXAxis();
    private readonly Axis _srcYAxis = NewYAxis();
    private readonly Axis _dstXAxis = NewXAxis();
    private readonly Axis _dstYAxis = NewYAxis();

    private List<Statistic>? _sourceStatistics;
    private List<Statistic>? _destinationStatistics;

    public ScalperItemDetailWindow()
    {
        InitializeComponent();

        SourcePriceChart.Series = [_srcHighest, _srcAverage, _srcLowest];
        SourcePriceChart.XAxes = [_srcXAxis];
        SourcePriceChart.YAxes = [_srcYAxis];
        SourceVolumeChart.Series = [_srcVolume];
        SourceVolumeChart.XAxes = [_srcXAxis];
        SourceVolumeChart.YAxes = [_srcYAxis];

        DestinationPriceChart.Series = [_dstHighest, _dstAverage, _dstLowest];
        DestinationPriceChart.XAxes = [_dstXAxis];
        DestinationPriceChart.YAxes = [_dstYAxis];
        DestinationVolumeChart.Series = [_dstVolume];
        DestinationVolumeChart.XAxes = [_dstXAxis];
        DestinationVolumeChart.YAxes = [_dstYAxis];

        ApplyThemeColors();
        ThemeService.ThemeChanged += ApplyThemeColors;
        Closed += (_, _) => ThemeService.ThemeChanged -= ApplyThemeColors;
    }

    public void SetItem(ScalperItem item)
    {
        TitleBar.Title = item.InvType.TypeName;
        Title = item.InvType.TypeName;
        TypeNameText.Text = item.InvType.TypeName;
        TypeIdText.Text = $"ID: {item.InvType.TypeID}";

        SuggestionText.Text = item.Suggestion.ToString("N2");
        BuyPriceText.Text = item.BuyPrice.ToString("N2");
        SellPriceText.Text = item.SellPrice.ToString("N2");
        SourceSalesText.Text = item.SourceSales.ToString("N0");
        DestinationSalesText.Text = item.DestinationSales.ToString("N0");
        TargetSalesText.Text = item.TargetSales.ToString("N0");
        RoiText.Text = item.ROI.ToString("N2");
        NetProfitText.Text = item.NetProfit.ToString("N2");
        TargetNetProfitText.Text = item.TargetNetProfit.ToString("N2");
        HeatValueText.Text = item.HeatValue.ToString("N0");
        PrincipalText.Text = item.Principal.ToString("N2");
        HistoryFluctuationText.Text = item.HistoryPriceFluctuation.ToString("N4");
        NowFluctuationText.Text = item.NowPriceFluctuation.ToString("N4");
        SaturationText.Text = item.Saturation.ToString("N2");
        VolumeText.Text = item.InvType.PackagedVolume.ToString("N2");
        TargetVolumeText.Text = item.TargetVolume.ToString("N2");
        IskPerJumpText.Text = item.IskPerJump.ToString("N2");
        IskPerVolumeText.Text = item.IskPerVolume.ToString("N2");

        SourceSellGrid.ItemsSource = item.SourceSellOrders ?? [];
        SourceBuyGrid.ItemsSource = item.SourceBuyOrders ?? [];
        DestinationSellGrid.ItemsSource = item.DestinationSellOrders ?? [];
        DestinationBuyGrid.ItemsSource = item.DestinationBuyOrders ?? [];

        _sourceStatistics = item.SourceStatistics;
        _destinationStatistics = item.DestinationStatistics;
        RefreshSourceCharts();
        RefreshDestinationCharts();

        _ = LoadIconAsync(item.InvType.TypeID);
    }

    private async Task LoadIconAsync(int typeId)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync($"https://images.evetech.net/types/{typeId}/icon?size=64").ConfigureAwait(false);
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            TypeImage.Source = bitmap;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private void OnSourceRangeChanged(object sender, SelectionChangedEventArgs e) => RefreshSourceCharts();

    private void OnDestinationRangeChanged(object sender, SelectionChangedEventArgs e) => RefreshDestinationCharts();

    private void RefreshSourceCharts()
    {
        ApplyStatistics(_sourceStatistics, SourceRangeBox.SelectedIndex, _srcHighest, _srcAverage, _srcLowest, _srcVolume);
    }

    private void RefreshDestinationCharts()
    {
        ApplyStatistics(_destinationStatistics, DestinationRangeBox.SelectedIndex, _dstHighest, _dstAverage, _dstLowest, _dstVolume);
    }

    private static void ApplyStatistics(
        List<Statistic>? statistics,
        int rangeIndex,
        LineSeries<DateTimePoint> highest,
        LineSeries<DateTimePoint> average,
        LineSeries<DateTimePoint> lowest,
        LineSeries<DateTimePoint> volume)
    {
        if (statistics is null)
        {
            highest.Values = [];
            average.Values = [];
            lowest.Values = [];
            volume.Values = [];
            return;
        }

        var now = DateTime.Now;
        IEnumerable<Statistic> filtered = rangeIndex switch
        {
            0 => statistics.Where(p => p.Date > now.AddMonths(-1)),
            1 => statistics.Where(p => p.Date > now.AddMonths(-3)),
            2 => statistics.Where(p => p.Date > now.AddMonths(-6)),
            3 => statistics.Where(p => p.Date > now.AddMonths(-12)),
            _ => statistics,
        };

        var rows = filtered.OrderBy(p => p.Date).ToList();
        highest.Values = rows.Select(p => new DateTimePoint(p.Date, p.Highest)).ToArray();
        average.Values = rows.Select(p => new DateTimePoint(p.Date, p.Average)).ToArray();
        lowest.Values = rows.Select(p => new DateTimePoint(p.Date, p.Lowest)).ToArray();
        volume.Values = rows.Select(p => new DateTimePoint(p.Date, p.Volume)).ToArray();
    }

    private void ApplyThemeColors()
    {
        if (MakePaint("SystemFillColorCriticalBrush", 2) is { } highest)
        {
            _srcHighest.Stroke = highest;
            _dstHighest.Stroke = highest;
        }

        if (MakePaint("SystemAccentColorPrimaryBrush", 2) is { } average)
        {
            _srcAverage.Stroke = average;
            _dstAverage.Stroke = average;
        }

        if (MakePaint("SystemFillColorSuccessBrush", 2) is { } lowest)
        {
            _srcLowest.Stroke = lowest;
            _dstLowest.Stroke = lowest;
        }

        if (MakePaint("TextFillColorSecondaryBrush", 2) is { } volume)
        {
            _srcVolume.Stroke = volume;
            _dstVolume.Stroke = volume;
        }

        if (MakePaint("TextFillColorSecondaryBrush", 1) is { } labelPaint)
        {
            _srcXAxis.LabelsPaint = labelPaint;
            _srcYAxis.LabelsPaint = labelPaint;
            _dstXAxis.LabelsPaint = labelPaint;
            _dstYAxis.LabelsPaint = labelPaint;
        }

        if (MakePaint("DividerStrokeColorDefaultBrush", 1) is { } separatorPaint)
        {
            _srcXAxis.SeparatorsPaint = separatorPaint;
            _srcYAxis.SeparatorsPaint = separatorPaint;
            _dstXAxis.SeparatorsPaint = separatorPaint;
            _dstYAxis.SeparatorsPaint = separatorPaint;
        }
    }

    private static SolidColorPaint? MakePaint(string resourceKey, double thickness)
    {
        if (Application.Current?.TryFindResource(resourceKey) is not SolidColorBrush brush)
        {
            return null;
        }

        var c = brush.Color;
        return new SolidColorPaint(new SKColor(c.R, c.G, c.B, c.A), (float)thickness);
    }

    private static LineSeries<DateTimePoint> NewLine(string name) => new()
    {
        Name = name,
        GeometrySize = 0,
        Fill = null,
    };

    private static Axis NewXAxis() => new DateTimeAxis(TimeSpan.FromDays(1), d => d.ToString("yyyy.MM.dd")) { TextSize = 12 };

    private static Axis NewYAxis() => new() { Labeler = v => v.ToString("N2"), TextSize = 12 };
}
