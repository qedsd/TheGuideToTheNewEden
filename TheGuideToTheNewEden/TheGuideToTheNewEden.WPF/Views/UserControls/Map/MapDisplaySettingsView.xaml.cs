using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Models.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>
/// 星图显示参数实时调参面板（顶栏「显示设置」弹窗内容）：
/// 按分组生成滑杆（节点 / 徽标与光晕 / 文字 / 连线与热力），拖动即写回 <see cref="MapDisplayConfig"/>
/// 并回调页面使画布实时重绘；保存按 500ms 防抖落盘，「恢复默认」一键还原出厂值。
/// 行布局：参数名（150）｜滑杆（伸展）｜当前值（56 右对齐）；文字用 WPF-UI 主题笔刷，随深浅主题。
/// 参数文案走本地化键 <c>MapDisplay_*</c>（缺键时显示键名，见 FindString 回退）。
/// </summary>
public partial class MapDisplaySettingsView : UserControl
{
    /// <summary>单个可调参数描述：本地化键、滑杆范围、读写器。</summary>
    private sealed record Param(string Key, double Min, double Max, Func<MapDisplayConfig, double> Get, Action<MapDisplayConfig, double> Set);

    /// <summary>参数行：携带参数与数值文本引用，滑杆 ValueChanged 时就地更新数值。</summary>
    private sealed class ParamRow : Grid
    {
        public Param Param = null!;
        public TextBlock ValueText = null!;
    }

    private static readonly (string GroupKey, Param[] Items)[] Schema =
    [
        ("MapDisplay_Group_Node",
        [
            new Param("MapDisplay_NodeRadiusBase", 0.8, 4, c => c.NodeRadiusBase, (c, v) => c.NodeRadiusBase = v),
            new Param("MapDisplay_NodeRadiusPower", 0.2, 1, c => c.NodeRadiusPower, (c, v) => c.NodeRadiusPower = v),
            new Param("MapDisplay_NodeRadiusMin", 0.5, 3, c => c.NodeRadiusMin, (c, v) => c.NodeRadiusMin = v),
            new Param("MapDisplay_NodeRadiusMax", 8, 60, c => c.NodeRadiusMax, (c, v) => c.NodeRadiusMax = v),
            new Param("MapDisplay_KernelZoom", 0, 100, c => c.KernelZoom, (c, v) => c.KernelZoom = v),
            new Param("MapDisplay_KernelScale", 0, 1, c => c.KernelScale, (c, v) => c.KernelScale = v),
        ]),
        ("MapDisplay_Group_Logo",
        [
            new Param("MapDisplay_LogoGate", 2, 10, c => c.LogoGate, (c, v) => c.LogoGate = v),
            new Param("MapDisplay_LogoScale", 1, 4, c => c.LogoScale, (c, v) => c.LogoScale = v),
            new Param("MapDisplay_LogoRingFactor", 1, 1.6, c => c.LogoRingFactor, (c, v) => c.LogoRingFactor = v),
            new Param("MapDisplay_LogoGlowFactor", 1, 3, c => c.LogoGlowFactor, (c, v) => c.LogoGlowFactor = v),
            new Param("MapDisplay_GlowScaleBase", 1, 3, c => c.GlowScaleBase, (c, v) => c.GlowScaleBase = v),
            new Param("MapDisplay_GlowScaleSlope", 0, 1, c => c.GlowScaleSlope, (c, v) => c.GlowScaleSlope = v),
            new Param("MapDisplay_GlowScaleMax", 1.5, 6, c => c.GlowScaleMax, (c, v) => c.GlowScaleMax = v),
            new Param("MapDisplay_GlowAlphaSlope", 10, 150, c => c.GlowAlphaSlope, (c, v) => c.GlowAlphaSlope = v),
            new Param("MapDisplay_GlowAlphaMax", 50, 255, c => c.GlowAlphaMax, (c, v) => c.GlowAlphaMax = v),
        ]),
        ("MapDisplay_Group_Text",
        [
            new Param("MapDisplay_NameSizeSlope", 0.3, 2, c => c.NameSizeSlope, (c, v) => c.NameSizeSlope = v),
            new Param("MapDisplay_NameSizeMin", 6, 16, c => c.NameSizeMin, (c, v) => c.NameSizeMin = v),
            new Param("MapDisplay_NameSizeMax", 10, 40, c => c.NameSizeMax, (c, v) => c.NameSizeMax = v),
            new Param("MapDisplay_SecSizeSlope", 0.2, 1.2, c => c.SecSizeSlope, (c, v) => c.SecSizeSlope = v),
            new Param("MapDisplay_SecSizeMin", 4, 10, c => c.SecSizeMin, (c, v) => c.SecSizeMin = v),
            new Param("MapDisplay_SecSizeMax", 6, 20, c => c.SecSizeMax, (c, v) => c.SecSizeMax = v),
            new Param("MapDisplay_NameFadeStart", 1, 15, c => c.NameFadeStart, (c, v) => c.NameFadeStart = v),
            new Param("MapDisplay_NameFadeEnd", 2, 20, c => c.NameFadeEnd, (c, v) => c.NameFadeEnd = v),
            new Param("MapDisplay_NameAlphaBase", 0, 200, c => c.NameAlphaBase, (c, v) => c.NameAlphaBase = v),
            new Param("MapDisplay_NameAlphaSlope", 0, 40, c => c.NameAlphaSlope, (c, v) => c.NameAlphaSlope = v),
            new Param("MapDisplay_NameAlphaMin", 0, 255, c => c.NameAlphaMin, (c, v) => c.NameAlphaMin = v),
            new Param("MapDisplay_NameAlphaMax", 80, 255, c => c.NameAlphaMax, (c, v) => c.NameAlphaMax = v),
            new Param("MapDisplay_NameDotGap", 0.8, 3, c => c.NameDotGap, (c, v) => c.NameDotGap = v),
            new Param("MapDisplay_TextPad", 0, 10, c => c.TextPad, (c, v) => c.TextPad = v),
            new Param("MapDisplay_SecGap", 0, 12, c => c.SecGap, (c, v) => c.SecGap = v),
            new Param("MapDisplay_SecAlignFactor", 0.1, 0.5, c => c.SecAlignFactor, (c, v) => c.SecAlignFactor = v),
            new Param("MapDisplay_ValueLineSpacing", 0.8, 3, c => c.ValueLineSpacing, (c, v) => c.ValueLineSpacing = v),
        ]),
        ("MapDisplay_Group_Heat",
        [
            new Param("MapDisplay_LinkFadeStart", 1, 10, c => c.LinkFadeStart, (c, v) => c.LinkFadeStart = v),
            new Param("MapDisplay_LinkWidthBase", 0.2, 2, c => c.LinkWidthBase, (c, v) => c.LinkWidthBase = v),
            new Param("MapDisplay_LinkWidthPower", 0, 1, c => c.LinkWidthPower, (c, v) => c.LinkWidthPower = v),
            new Param("MapDisplay_HeatAlphaBase", 0, 120, c => c.HeatAlphaBase, (c, v) => c.HeatAlphaBase = v),
            new Param("MapDisplay_HeatAlphaSlope", 50, 255, c => c.HeatAlphaSlope, (c, v) => c.HeatAlphaSlope = v),
            new Param("MapDisplay_HeatAlphaMax", 60, 255, c => c.HeatAlphaMax, (c, v) => c.HeatAlphaMax = v),
            new Param("MapDisplay_SovBlobAlphaBase", 0, 120, c => c.SovBlobAlphaBase, (c, v) => c.SovBlobAlphaBase = v),
            new Param("MapDisplay_SovBlobAlphaSlope", 50, 255, c => c.SovBlobAlphaSlope, (c, v) => c.SovBlobAlphaSlope = v),
            new Param("MapDisplay_SovBlobAlphaMax", 60, 255, c => c.SovBlobAlphaMax, (c, v) => c.SovBlobAlphaMax = v),
            new Param("MapDisplay_SovAlphaDamp", 0.1, 1, c => c.SovAlphaDamp, (c, v) => c.SovAlphaDamp = v),
        ]),
    ];

    private readonly MapDisplayConfig _config;
    private readonly Action _applyCallback;
    private readonly Action _saveCallback;
    private readonly DispatcherTimer _saveTimer;   // 拖动滑杆高频触发，防抖后落盘
    private bool _updating;                        // 程序化赋值（初始回填/恢复默认）时跳过 ValueChanged

    public MapDisplaySettingsView(MapDisplayConfig config, Action applyCallback, Action saveCallback)
    {
        InitializeComponent();
        _config = config;
        _applyCallback = applyCallback;
        _saveCallback = saveCallback;
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _saveCallback();
        };
        BuildTabs();
    }

    private void BuildTabs()
    {
        GroupTabs.Items.Clear();
        foreach (var (groupKey, items) in Schema)
        {
            // 每组一个 Tab；标签列固定宽 → 各行滑杆左缘/长度对齐，数值列右对齐
            var panel = new StackPanel { Margin = new Thickness(4, 8, 4, 0) };
            foreach (var param in items)
            {
                panel.Children.Add(CreateRow(param));
            }

            GroupTabs.Items.Add(new TabItem
            {
                Header = FindString(groupKey),
                Content = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = panel,
                },
            });
        }
    }

    private ParamRow CreateRow(Param param)
    {
        var row = new ParamRow { Margin = new Thickness(0, 5, 0, 5) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });   // 标签统一列宽：滑杆起点/长度全表一致
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });

        var label = new TextBlock
        {
            Text = FindString(param.Key),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = param.Key,
        };
        label.SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        Grid.SetColumn(label, 0);

        var slider = new Slider
        {
            Minimum = param.Min,
            Maximum = param.Max,
            Value = Math.Clamp(param.Get(_config), param.Min, param.Max),
            SmallChange = (param.Max - param.Min) / 50,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Tag = param,
        };
        slider.ValueChanged += Slider_ValueChanged;
        Grid.SetColumn(slider, 1);

        var valueText = new TextBlock
        {
            Text = param.Get(_config).ToString("0.##"),
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
        };
        valueText.SetResourceReference(ForegroundProperty, "TextFillColorSecondaryBrush");
        Grid.SetColumn(valueText, 2);

        row.Children.Add(label);
        row.Children.Add(slider);
        row.Children.Add(valueText);
        row.Param = param;
        row.ValueText = valueText;
        return row;
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || _config is null || sender is not Slider { Tag: Param param })
        {
            return;
        }

        param.Set(_config, e.NewValue);
        if (param.Get(_config) is var current && sender is Slider slider && slider.Parent is ParamRow row)
        {
            row.ValueText.Text = current.ToString("0.##");
        }

        _applyCallback();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new MapDisplayConfig();
        _updating = true;
        try
        {
            foreach (var (_, items) in Schema)
            {
                foreach (var param in items)
                {
                    param.Set(_config, param.Get(defaults));
                }
            }

            BuildTabs();   // 重建行即按新值回填滑杆与数值
        }
        finally
        {
            _updating = false;
        }

        _applyCallback();
        _saveCallback();
    }

    /// <summary>取 WPF-UI 主题笔刷（随深浅主题），缺键时给中性回退色。</summary>
    private static Brush ThemeBrush(string key, Color fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}
