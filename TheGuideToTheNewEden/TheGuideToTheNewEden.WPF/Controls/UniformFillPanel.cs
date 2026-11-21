using System;
using System.Windows;
using System.Windows.Controls;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>
/// 阶段 370：等宽自动换行面板。
/// <para>
/// 卡片先按 <see cref="MinItemWidth"/> 决定一行能放几列（不超过 <see cref="MaxItemWidth"/> 允许的列数），
/// 同一行的卡片再平分可用宽度（扣掉间距），所以每一行都刚好填满可用区域；放不下就换行。
/// </para>
/// </summary>
public sealed class UniformFillPanel : Panel
{
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(UniformFillPanel),
        new FrameworkPropertyMetadata(150d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaxItemWidthProperty = DependencyProperty.Register(
        nameof(MaxItemWidth), typeof(double), typeof(UniformFillPanel),
        new FrameworkPropertyMetadata(360d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemSpacingProperty = DependencyProperty.Register(
        nameof(ItemSpacing), typeof(double), typeof(UniformFillPanel),
        new FrameworkPropertyMetadata(7d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register(
        nameof(RowSpacing), typeof(double), typeof(UniformFillPanel),
        new FrameworkPropertyMetadata(7d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>单张卡片的最小宽度（决定一行最多几列）。</summary>
    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    /// <summary>单张卡片的最大宽度（可用宽度很大时也不会被拉到离谱）。</summary>
    public double MaxItemWidth
    {
        get => (double)GetValue(MaxItemWidthProperty);
        set => SetValue(MaxItemWidthProperty, value);
    }

    public double ItemSpacing
    {
        get => (double)GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = InternalChildren.Count;
        if (count == 0)
        {
            return new Size(0, 0);
        }

        var width = double.IsInfinity(availableSize.Width) ? MinItemWidth * count : availableSize.Width;
        var (columns, itemWidth) = ResolveColumns(width, count);

        double totalHeight = 0;
        var index = 0;
        while (index < count)
        {
            var rowCount = Math.Min(columns, count - index);
            var rowWidth = RowItemWidth(rowCount, width, columns, itemWidth);
            double rowHeight = 0;
            for (var i = 0; i < rowCount; i++)
            {
                var child = InternalChildren[index + i];
                child.Measure(new Size(rowWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            }

            totalHeight += rowHeight;
            index += rowCount;
            if (index < count)
            {
                totalHeight += RowSpacing;
            }
        }

        return new Size(width, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = InternalChildren.Count;
        if (count == 0)
        {
            return finalSize;
        }

        var (columns, itemWidth) = ResolveColumns(finalSize.Width, count);
        var index = 0;
        var y = 0d;
        while (index < count)
        {
            var rowCount = Math.Min(columns, count - index);
            var rowWidth = RowItemWidth(rowCount, finalSize.Width, columns, itemWidth);
            double rowHeight = 0;
            for (var i = 0; i < rowCount; i++)
            {
                var child = InternalChildren[index + i];
                child.Measure(new Size(rowWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            }

            for (var i = 0; i < rowCount; i++)
            {
                var child = InternalChildren[index + i];
                var x = i * (rowWidth + ItemSpacing);
                // 同一行统一高度：矮卡片拉伸到本行最高的那张，保证一行看起来齐平。
                child.Arrange(new Rect(x, y, rowWidth, rowHeight));
            }

            y += rowHeight + RowSpacing;
            index += rowCount;
        }

        return finalSize;
    }

    /// <summary>
    /// 单张卡片占满一行时（例如最后一行只剩一张），这张卡直接铺满整行可用宽度。
    /// </summary>
    private double RowItemWidth(int rowCount, double availableWidth, int columns, double itemWidth)
    {
        if (rowCount != 1 || columns <= 1)
        {
            return itemWidth;
        }

        return Math.Max(1d, availableWidth);
    }

    /// <summary>
    /// 按可用宽度与卡片数量算列数：先按最小宽度排，列数不超过卡片数（一张卡就占满一行），
    /// 再保证每列不超过 <see cref="MaxItemWidth"/>。
    /// </summary>
    private (int Columns, double ItemWidth) ResolveColumns(double availableWidth, int count)
    {
        var min = Math.Max(1d, MinItemWidth);
        var max = Math.Max(min, MaxItemWidth);
        var spacing = Math.Max(0d, ItemSpacing);
        if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth) || availableWidth <= 0)
        {
            return (1, min);
        }

        var columns = (int)Math.Max(1, Math.Floor((availableWidth + spacing) / (min + spacing)));
        columns = Math.Min(columns, Math.Max(1, count));

        var itemWidth = (availableWidth - (columns - 1) * spacing) / columns;
        // 列宽超过上限就多加一列（直到用完卡片数），让单行仍然填满可用宽度。
        while (itemWidth > max && columns < count)
        {
            columns++;
            itemWidth = (availableWidth - (columns - 1) * spacing) / columns;
        }

        if (itemWidth > max)
        {
            itemWidth = max;
        }

        return (columns, Math.Max(1, itemWidth));
    }
}
