using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 比价结果区（一组：卖出或买入）。内层 Tab 由 <see cref="Tabs"/> 驱动——第一个是"全部"（完整价格矩阵），
/// 其余是"该市场为最优"的物品简表。
/// <para>
/// 矩阵的列在代码里构造（市场是动态的），因此它**没有用星号列**：DataGrid 只要存在 Star 列就会把列压缩进视口，
/// 于是永远不出现横向滚动条（§9 第 84 条）。列头里的"卖 / 买"是求值时拼好的字符串，换语言要重建列。
/// </para>
/// </summary>
public partial class CompareMarketResultView : UserControl
{
    /// <summary>市场列在 Auto 宽度下的最小宽度。</summary>
    private const double MarketColumnMinWidth = 90;

    public CompareMarketResultView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => LanguageService.LanguageChanged += OnLanguageChanged;

    private void OnUnloaded(object sender, RoutedEventArgs e) => LanguageService.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, string language) => ReloadColumns();

    // ==================================================================
    //  依赖属性
    // ==================================================================

    public static readonly DependencyProperty TabsProperty = DependencyProperty.Register(
        nameof(Tabs),
        typeof(IEnumerable),
        typeof(CompareMarketResultView),
        new PropertyMetadata(null));

    /// <summary>结果 Tab 集合（"全部" + 各市场）。</summary>
    public IEnumerable? Tabs
    {
        get => (IEnumerable?)GetValue(TabsProperty);
        set => SetValue(TabsProperty, value);
    }

    public static readonly DependencyProperty MarketNamesProperty = DependencyProperty.Register(
        nameof(MarketNames),
        typeof(IList<string>),
        typeof(CompareMarketResultView),
        new PropertyMetadata(null));

    /// <summary>市场名（与 <c>CompareMarketRow.Cells</c> 同序），矩阵的动态列按它生成。</summary>
    public IList<string>? MarketNames
    {
        get => (IList<string>?)GetValue(MarketNamesProperty);
        set => SetValue(MarketNamesProperty, value);
    }

    public static readonly DependencyProperty IsSellSideProperty = DependencyProperty.Register(
        nameof(IsSellSide),
        typeof(bool),
        typeof(CompareMarketResultView),
        new PropertyMetadata(true));

    /// <summary>
    /// 本控件承载的是"卖出结果"还是"买入结果"。只影响矩阵里那一个"最优市场"列显示哪一侧
    /// （外层 Tab 已经分了卖出 / 买入，把另一侧也摆出来是冗余信息）。
    /// </summary>
    public bool IsSellSide
    {
        get => (bool)GetValue(IsSellSideProperty);
        set => SetValue(IsSellSideProperty, value);
    }

    // ==================================================================
    //  列构造
    // ==================================================================

    /// <summary>矩阵表格每次进入可视树（切 Tab 会重建内容）都要构造列。</summary>
    private void OnMatrixGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            BuildColumns(grid);
        }
    }

    /// <summary>外部（主视图）在"结果已更新 / 市场变化 / 换语言"时调用，重建当前矩阵表格的列。</summary>
    public void ReloadColumns()
    {
        foreach (var grid in FindMatrixGrids(this))
        {
            BuildColumns(grid);
        }
    }

    private void BuildColumns(DataGrid grid)
    {
        var names = MarketNames ?? [];

        grid.Columns.Clear();

        // 固定列全部用固定像素（见类注释：不能出现星号列）
        grid.Columns.Add(CreateColumn(FindString("CompareMarketPage_ColItem"), nameof(CompareMarketRow.Name), 200, number: false));
        grid.Columns.Add(CreateColumn(FindString("CompareMarketPage_ColAmount"), nameof(CompareMarketRow.Amount), 80, format: "N0"));
        grid.Columns.Add(CreateColumn(FindString("CompareMarketPage_ColVolume"), nameof(CompareMarketRow.Volume), 90, format: "N2"));

        // 只显示"本组对应的那一个最优市场"：外层 Tab 已经分了卖出 / 买入，把另一侧的最优市场也摆出来是冗余信息
        grid.Columns.Add(IsSellSide
            ? CreateColumn(FindString("CompareMarketPage_ColBestSell"), nameof(CompareMarketRow.BestSellMarketName), 150, number: false)
            : CreateColumn(FindString("CompareMarketPage_ColBestBuy"), nameof(CompareMarketRow.BestBuyMarketName), 150, number: false));

        grid.Columns.Add(CreateColumn(FindString("CompareMarketPage_ColUnitPrice"), nameof(CompareMarketRow.EffectiveUnitPrice), 110, format: "N2"));
        grid.Columns.Add(CreateColumn(FindString("CompareMarketPage_ColAdvantage"), nameof(CompareMarketRow.AdvantagePercent), 100, converter: "PercentOrDash"));
        grid.Columns.Add(CreateColumn(FindString("CompareMarketPage_ColSubtotal"), nameof(CompareMarketRow.Subtotal), 130, format: "N2"));

        // 每个市场**一列**：只显示"这一行角色对应的那一侧"（卖出组显示到手价、买入组显示成本价），
        // 不再同一市场并列卖价 / 买价两列（用户要求）。
        for (var i = 0; i < names.Count; i++)
        {
            grid.Columns.Add(CreateMarketColumn(names[i] ?? string.Empty, i));
        }
    }

    private DataGridTextColumn CreateColumn(
        string header,
        string path,
        double minWidth,
        bool number = true,
        string? format = null,
        string? converter = null)
    {
        var binding = new Binding(path);
        if (!string.IsNullOrEmpty(format))
        {
            binding.StringFormat = "{0:" + format + "}";
        }

        if (!string.IsNullOrEmpty(converter) && FindResource(converter) is IValueConverter valueConverter)
        {
            binding.Converter = valueConverter;
        }

        return new DataGridTextColumn
        {
            Header = header,
            Binding = binding,
            IsReadOnly = true,
            ElementStyle = FindResource(number ? "ResultNumberCellStyle" : "ResultCellStyle") as Style,
            // 列宽**按内容自适应**（用户要求"全部 Auto"），MinWidth 保证不会窄到看不清。
            // 仍然**不能有星号列**：一旦存在 Star 列，DataGrid 会把列压缩进视口、永不出现横向滚动条（§9 第 84 条）；
            // 全 Auto / Pixel 列时，内容超宽会正常给出横向滚动条。
            Width = DataGridLength.Auto,
            MinWidth = minWidth,
        };
    }

    /// <summary>一个市场的一列（<c>Cells[i].UnitPrice</c> = 按该行角色取到的价），无价显示 —。</summary>
    private DataGridTextColumn CreateMarketColumn(string header, int index)
    {
        var path = $"Cells[{index}].{nameof(CompareMarketCell.UnitPrice)}";
        return new DataGridTextColumn
        {
            Header = new TextBlock
            {
                Text = header,
                ToolTip = header,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
            Binding = new Binding(path) { Converter = FindResource("PriceOrDash") as IValueConverter },
            ElementStyle = FindResource("ResultNumberCellStyle") as Style,
            IsReadOnly = true,
            Width = DataGridLength.Auto,
            MinWidth = MarketColumnMinWidth,
        };
    }

    /// <summary>在可视树里找标记为矩阵的表格（简表是静态列，不需要重建）。</summary>
    private static IEnumerable<DataGrid> FindMatrixGrids(DependencyObject root)
    {
        if (root is DataGrid { Tag: "matrix" } grid)
        {
            yield return grid;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            foreach (var found in FindMatrixGrids(VisualTreeHelper.GetChild(root, i)))
            {
                yield return found;
            }
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
