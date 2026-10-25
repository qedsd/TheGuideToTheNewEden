using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>殖民地产能折线图：提取器衰减或工厂理论满产，共用同一绘图控件。</summary>
public partial class PlanetaryCapacityChart : UserControl
{
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series),
        typeof(IReadOnlyList<PlanetaryLiveCapacityPoint>),
        typeof(PlanetaryCapacityChart),
        new PropertyMetadata(null, (d, _) => ((PlanetaryCapacityChart)d).Render()));

    public IReadOnlyList<PlanetaryLiveCapacityPoint>? Series
    {
        get => (IReadOnlyList<PlanetaryLiveCapacityPoint>?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public PlanetaryCapacityChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
    }

    private void Render()
    {
        Plot.Children.Clear();
        var series = Series;
        if (series is null
            || series.Count < 2
            || ActualWidth <= 20
            || ActualHeight <= 20)
        {
            return;
        }

        var width = ActualWidth;
        var height = ActualHeight;
        var left = 40.0;
        var right = 12.0;
        var top = 12.0;
        var bottom = 24.0;
        var plotWidth = Math.Max(1, width - left - right);
        var plotHeight = Math.Max(1, height - top - bottom);
        var max = Math.Max(series.Max(p => p.Value), 1);
        var min = Math.Min(series.Min(p => p.Value), 0);
        var range = Math.Max(max - min, 1);
        var accent = TryFindResource("SystemAccentColorPrimaryBrush") as Brush ?? Brushes.DodgerBlue;
        var gridBrush = TryFindResource("ControlFillColorSecondaryBrush") as Brush ?? Brushes.LightGray;
        var textBrush = TryFindResource("TextFillColorTertiaryBrush") as Brush ?? Brushes.Gray;

        for (var i = 0; i <= 3; i++)
        {
            var y = top + plotHeight * i / 3.0;
            Plot.Children.Add(new Line
            {
                X1 = left,
                X2 = left + plotWidth,
                Y1 = y,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 0.7,
            });
            var value = max - range * i / 3.0;
            var label = new TextBlock
            {
                Text = FormatValue(value),
                FontSize = 9,
                Foreground = textBrush,
            };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, y - 7);
            Plot.Children.Add(label);
        }

        var points = new PointCollection();
        for (var i = 0; i < series.Count; i++)
        {
            var x = left + plotWidth * i / (series.Count - 1.0);
            var y = top + plotHeight * (1 - (series[i].Value - min) / range);
            points.Add(new Point(x, y));
        }
        Plot.Children.Add(new Polyline
        {
            Points = points,
            Stroke = accent,
            StrokeThickness = 1.6,
            StrokeLineJoin = PenLineJoin.Round,
        });

        var first = new TextBlock
        {
            Text = series[0].Label,
            FontSize = 9,
            Foreground = textBrush,
        };
        Canvas.SetLeft(first, left);
        Canvas.SetTop(first, top + plotHeight + 4);
        Plot.Children.Add(first);

        var last = new TextBlock
        {
            Text = series[series.Count - 1].Label,
            FontSize = 9,
            Foreground = textBrush,
        };
        last.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(last, left + plotWidth - last.DesiredSize.Width);
        Canvas.SetTop(last, top + plotHeight + 4);
        Plot.Children.Add(last);
    }

    private static string FormatValue(double value)
    {
        if (value >= 1_000_000) return $"{value / 1_000_000:0.#}M";
        if (value >= 1_000) return $"{value / 1_000:0.#}k";
        return value.ToString("0.#");
    }
}
