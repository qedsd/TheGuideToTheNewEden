using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Helpers.Interop;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using TheGuideToTheNewEden.WPF.Services.GamePreview.Overlay;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 预览窗口左上角的"角色名 + 信息叠加层"叠加窗。
/// <para>
/// <b>为什么必须是独立窗口</b>：DWM 缩略图由系统在窗口自身内容之上合成，
/// 画在预览窗口里的任何 WPF 元素都会被缩略图盖住（实测：样式 1 的名称条与样式 0 的角标都看不见）。
/// 因此角色名只能放在一个独立的、位于预览窗口之上的小窗口里。
/// </para>
/// <para>
/// 该窗口：无边框、背景透明、不抢焦点（<c>ShowActivated=False</c> + <c>WS_EX_NOACTIVATE</c>）、
/// 不进 Alt+Tab 与任务栏，并且带 <c>WS_EX_TRANSPARENT</c> —— 鼠标点击**穿透**到下面的预览窗口，
/// 所以拖动与滚轮缩放不受影响。位置尺寸由 <see cref="GamePreviewWindow"/> 在每次刷新时用物理像素同步。
/// </para>
/// <para>
/// <b>阶段 382</b>：信息行不再是一叠自由文本，而是"胶囊 HUD"——
/// 带 <see cref="PreviewOverlayChip"/> 的行按 预警 → 状态 → 数值 三个 zone 排进同一块底板，
/// 放不下的数值胶囊折叠成 <c>+N</c>；没有 Chip 的行（日志监控命中等低频内容）仍按旧文本行显示。
/// 底板宽度受 <see cref="SetMaxContentWidth"/> 限制，永远夹在预览窗内。
/// </para>
/// <para>
/// 这里刻意用普通 <see cref="Window"/> 而不是 <c>ui:FluentWindow</c>：
/// <c>AllowsTransparency</c> 与 FluentWindow 冲突（后者会把 WindowStyle 重置为单边框，
/// 显示时抛 InvalidOperationException，见 REFACTORING 阶段 43 的实测记录）。
/// 这个窗口本身完全透明、无标题栏，不需要 WPF-UI 的外观。
/// </para>
/// </summary>
public sealed partial class PreviewNameOverlayWindow : Window
{

    /// <summary>信息行字号（由"预警文字"自己的设置换算，不再从角色名字号派生；也是整块 HUD 的基准）。</summary>
    public static readonly DependencyProperty InfoFontSizeProperty = DependencyProperty.Register(
        nameof(InfoFontSize),
        typeof(double),
        typeof(PreviewNameOverlayWindow),
        new PropertyMetadata(12d));

    /// <summary>信息行字体（与角色名叠加各自独立设置）。</summary>
    public static readonly DependencyProperty InfoFontFamilyProperty = DependencyProperty.Register(
        nameof(InfoFontFamily),
        typeof(FontFamily),
        typeof(PreviewNameOverlayWindow),
        new PropertyMetadata(new FontFamily("Microsoft YaHei UI")));

    /// <summary>
    /// 悬停详情用的 ToolTip（阶段 356）。
    /// <para>
    /// 叠加窗是鼠标穿透的、自己收不到 hover：由下面的预览窗检测光标后调 <see cref="SetDetails"/> 打开。
    /// 用**独立 ToolTip 浮层**而不是往叠加窗里加一块面板——信息行下面的区域要留给以后的功能，
    /// 不能被"详情"长期占位；浮层也不改变叠加窗尺寸，悬停/收起不会引发布局跳动。
    /// </para>
    /// </summary>
    private readonly ToolTip _detailTip;

    private readonly TextBlock _detailText;

    /// <summary>最近一次从 hub 收到的全部行（用于颜色/字号变化后重建胶囊）。</summary>
    private readonly List<PreviewOverlayItem> _items = [];


    /// <summary>胶囊文字的唯一字号（与日志监控小窗一致：标签 / 数值 / 说明同字号，靠字重区分）。</summary>
    private double _chipFontSize = 11;

    /// <summary>数值区允许的最大行数（1–3，来自多开全局设置）。</summary>
    private int _maxValueRows = 2;

    public PreviewNameOverlayWindow()
    {
        InitializeComponent();

        _detailText = new TextBlock
        {
            MaxWidth = 460,
            TextWrapping = TextWrapping.Wrap,
            // 必须显式左对齐：长内容折行时若走两端对齐，行内的分隔空格会被拉开，
            // 表现为"频道名 / 发言人 / 内容"间隔很远（用户反馈）。
            TextAlignment = TextAlignment.Left,
        };

        _detailTip = new ToolTip
        {
            Content = _detailText,
            // 挂在信息行上、出现在行下方：光标仍停在行上，不会因为浮层盖住光标而反复开关
            PlacementTarget = InfoList,
            Placement = PlacementMode.Relative,
            StaysOpen = true,
            UseLayoutRounding = true,
        };

        UpdateChipFontMetrics(InfoFontSize);
    }

    /// <summary>左上角显示的角色名；传空则隐藏名字（只留信息行，用于"带标题栏"样式）。</summary>
    public string DisplayName
    {
        get => NameText.Text;
        set => NameText.Text = value ?? string.Empty;
    }

    /// <summary>名字单独控制显隐：带标题栏样式的窗口里名字已写在标题栏，这里只显示信息行。</summary>
    public void SetNameVisible(bool visible)
    {
        var next = visible ? Visibility.Visible : Visibility.Collapsed;
        if (Badge.Visibility != next)
        {
            Badge.Visibility = next;
        }
    }

    public double InfoFontSize
    {
        get => (double)GetValue(InfoFontSizeProperty);
        set => SetValue(InfoFontSizeProperty, value);
    }

    public FontFamily InfoFontFamily
    {
        get => (FontFamily)GetValue(InfoFontFamilyProperty);
        set => SetValue(InfoFontFamilyProperty, value);
    }

    /// <summary>
    /// 叠加层内容的可用宽度（DIP）。传 0 表示不限制（探针与旧路径）。
    /// <para>
    /// 预览窗在每次刷新时按"客户区宽 − 12"传入；底板、文本行与角色名都会被它夹住，
    /// 任何 provider 都不能再把叠加窗撑宽。
    /// </para>
    /// </summary>
    public void SetMaxContentWidth(double width)
    {
        var next = width > 0 ? width : double.PositiveInfinity;
        if (AreClose(RootPanel.MaxWidth, next))
        {
            return;
        }

        RootPanel.MaxWidth = next;
        RebuildChips();
    }

    /// <summary>数值区最多显示几行（1–3）。改变时立即重排（折叠结果可能变化）。</summary>
    public void SetMaxValueRows(int rows)
    {
        var next = Math.Clamp(rows, 1, 3);
        if (next == _maxValueRows)
        {
            return;
        }

        _maxValueRows = next;
        RebuildChips();
    }

    /// <summary>
    /// 套用信息行：空集合 = 整块隐藏。调用方随后应自行 <c>UpdateLayout()</c> 并重摆窗口
    /// （<c>SizeToContent</c> 改变了宽高，必须重新按物理像素 <see cref="SetBounds"/>）。
    /// </summary>
    public void SetInfoItems(IReadOnlyList<PreviewOverlayItem>? items)
    {
        _items.Clear();
        if (items is { Count: > 0 })
        {
            _items.AddRange(items);
        }

        // 不带 Chip 的行（日志监控命中等）仍走旧文本行模板
        var textRows = _items.Where(p => p.Chip is null).ToList();
        if (textRows.Count > 0)
        {
            InfoList.ItemsSource = textRows;
            InfoList.Visibility = Visibility.Visible;
        }
        else
        {
            InfoList.ItemsSource = null;
            InfoList.Visibility = Visibility.Collapsed;
        }

        RebuildChips();
    }

    /// <summary>当前是否有信息行（决定"带标题栏样式"下叠加窗是否需要显示）。</summary>
    public bool HasInfoItems
        => InfoList.Visibility == Visibility.Visible || Plate.Visibility == Visibility.Visible;

    /// <summary>当前是否展开了悬停详情。</summary>
    public bool HasDetails => _detailTip.IsOpen;

    /// <summary>测试 / 排查用：悬停详情的文字元素（可直接渲染出来核对排版）。</summary>
    public FrameworkElement DetailsVisual => _detailText;

    /// <summary>
    /// 悬停详情：传 null / 空串即收起。由 <see cref="GamePreviewWindow"/> 在光标进入叠加区域时调用
    /// （叠加窗本身是鼠标穿透的，收不到鼠标消息）。
    /// </summary>
    public void SetDetails(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (_detailTip.IsOpen)
            {
                _detailTip.IsOpen = false;
            }

            return;
        }

        if (!string.Equals(_detailText.Text, text, StringComparison.Ordinal))
        {
            _detailText.Text = text;
        }

        // 用的是标准 ToolTip 外观（WPF-UI 主题）；只把字体/字号跟预警行对齐，
        // 颜色交给主题（深色主题下是深底浅字），不套用户给信息行自选的半透明底色。
        _detailText.FontFamily = InfoFontFamily;
        _detailText.FontSize = Math.Clamp(InfoFontSize, 12d, 18d);

        if (!_detailTip.IsOpen)
        {
            // 出现在信息行正下方，左对齐
            _detailTip.PlacementTarget = Plate.Visibility == Visibility.Visible ? Plate : InfoList;
            _detailTip.Placement = PlacementMode.Relative;
            _detailTip.HorizontalOffset = 0;
            _detailTip.VerticalOffset = (Plate.Visibility == Visibility.Visible ? Plate.ActualHeight : InfoList.ActualHeight) + 6;
            _detailTip.IsOpen = true;
        }
    }

    /// <summary>角色名文字色（测试 / 排查用）。</summary>
    public Brush NameForeground => NameText.Foreground;

    /// <summary>
    /// 测试 / 排查用：当前可见的胶囊数量（按 zone 统计；Overflow 表示出现了 <c>+N</c>）。
    /// </summary>
    public (int Intel, int State, int Value, int Overflow) GetChipCounts()
    {
        static int Visible(WrapPanel panel)
            => panel.Children.OfType<FrameworkElement>().Count(p => p.Visibility == Visibility.Visible && !Equals(p.Tag, "overflow"));

        static int Overflow(WrapPanel panel)
            => panel.Children.OfType<FrameworkElement>().Count(p => Equals(p.Tag, "overflow"));

        return (Visible(IntelPanel), Visible(StatePanel), Visible(ValuePanel), Overflow(IntelPanel) + Overflow(StatePanel) + Overflow(ValuePanel));
    }

    /// <summary>测试 / 排查用：HUD 底板的实际宽度 / 高度（DIP）。</summary>
    public (double Width, double Height) GetPlateSize() => (Plate.ActualWidth, Plate.ActualHeight);


    /// <summary>测试 / 排查用：各枚预警胶囊的透明度（降级预警应为 0.55）。</summary>
    public IReadOnlyList<double> GetIntelChipOpacities()
        => IntelPanel.Children.OfType<FrameworkElement>().Select(p => p.Opacity).ToList();

    /// <summary>测试 / 排查用：某一段里第一枚可见胶囊的文字颜色。</summary>
    public Brush? GetChipForeground(PreviewOverlayChipKind kind)
    {
        var panel = kind switch
        {
            PreviewOverlayChipKind.Intel => IntelPanel,
            PreviewOverlayChipKind.State => StatePanel,
            _ => ValuePanel,
        };

        var first = panel.Children.OfType<Border>().FirstOrDefault(p => p.Visibility == Visibility.Visible);
        return first is null ? null : FirstTextBlock(first)?.Foreground;
    }

    /// <summary>测试 / 排查用：按第一段文字（分类短名 / 预警标签）找胶囊的文字颜色。</summary>
    public Brush? GetChipForegroundByLabel(string label)
    {
        foreach (var panel in new[] { IntelPanel, StatePanel, ValuePanel })
        {
            foreach (var chip in panel.Children.OfType<Border>())
            {
                if (chip.Visibility != Visibility.Visible)
                {
                    continue;
                }

                var text = FirstTextBlock(chip);
                if (text is not null && string.Equals(text.Text, label, StringComparison.Ordinal))
                {
                    return text.Foreground;
                }
            }
        }

        return null;
    }

    /// <summary>测试 / 排查用：第一枚数值胶囊的外观快照（圆角 / 边框 / 底色 / 文字字号）。</summary>
    public (double Radius, double BorderThickness, Color? Background, IReadOnlyList<double> FontSizes)? GetChipStyleSnapshot()
    {
        var chip = ValuePanel.Children.OfType<Border>().FirstOrDefault(p => p.Visibility == Visibility.Visible);
        if (chip is null)
        {
            return null;
        }

        var sizes = new List<double>();
        CollectFontSizes(chip, sizes);
        return (chip.CornerRadius.TopLeft, chip.BorderThickness.Left, (chip.Background as SolidColorBrush)?.Color, sizes);
    }

    private static void CollectFontSizes(DependencyObject root, List<double> sizes)
    {
        if (root is TextBlock block)
        {
            sizes.Add(block.FontSize);
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            CollectFontSizes(VisualTreeHelper.GetChild(root, i), sizes);
        }
    }

    private static TextBlock? FirstTextBlock(DependencyObject root)
    {
        if (root is TextBlock block)
        {
            return block;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FirstTextBlock(VisualTreeHelper.GetChild(root, i));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 套用外观设置：角色名的字体/字号/文字色/背景色，以及叠加层的字体/字号。
    /// <para>
    /// 阶段 385：叠加层的**文字色 / 背景色不再开放设置**——胶囊颜色由分类 tone 决定
    /// （红=受损、绿=受益、紫=电子战；预警按 最近 / 最新 / 降级 取 红 / 黄 / 灰），
    /// 这里只保留字体与字号（字号也是整块 HUD 的基准）。
    /// </para>
    /// </summary>
    public void ApplyAppearance(
        string? fontFamily,
        double fontSize,
        System.Drawing.Color foregroundColor,
        System.Drawing.Color backgroundColor,
        string? infoFontFamily,
        double infoFontSize)
    {
        if (!string.IsNullOrWhiteSpace(fontFamily) && !string.Equals(NameText.FontFamily.Source, fontFamily, StringComparison.OrdinalIgnoreCase))
        {
            // 字体名可能来自别处（未安装/来自 WinUI 版的配置），解析失败就用默认字体，不抛异常
            try
            {
                NameText.FontFamily = new FontFamily(fontFamily);
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        NameText.FontSize = Math.Max(1, fontSize);
        var foreground = PreviewColorHelper.ToBrush(foregroundColor);
        var background = PreviewColorHelper.ToBrush(backgroundColor);
        NameText.Foreground = foreground;
        Badge.Background = background;

        // 叠加层：字体 / 字号（颜色由分类 tone 决定，不再有单独设置）
        InfoFontFamily = ResolveFontFamily(infoFontFamily) ?? InfoFontFamily;
        InfoFontSize = Math.Max(1, infoFontSize);

        var changed = UpdateChipFontMetrics(InfoFontSize);
        if (changed)
        {
            // 字号变了：胶囊的字号是建的时候算好的，需要重建（尺寸会变，SizeToContent 跟着更新）
            RebuildChips();
        }

        UpdateLayout();
    }


    /// <summary>
    /// 按基准字号算胶囊字号；与日志监控小窗的胶囊口径一致：**所有文字同一个字号**，
    /// 靠字重（标签 / 主值 SemiBold、说明 Normal）和颜色区分，不再做三档字号。
    /// 返回是否发生变化。
    /// </summary>
    private bool UpdateChipFontMetrics(double baseSize)
    {
        var size = Math.Clamp(baseSize, 12d, 40d);
        // 默认 30（跟随缩放时 480 窗约 15）→ 11.7 DIP，与日志监控小窗的 11 px 基本一致
        var chip = Math.Max(11d, size * 0.78);
        var changed = Math.Abs(chip - _chipFontSize) > 0.01;
        _chipFontSize = chip;
        return changed;
    }

    // ---------- 胶囊构建 ----------

    private void RebuildChips()
    {
        IntelPanel.Children.Clear();
        StatePanel.Children.Clear();
        ValuePanel.Children.Clear();

        // 折叠要靠真实排版位置判断行数：先让三个 zone 回到可见状态（上一轮可能被折叠成 Collapsed）
        IntelPanel.Visibility = Visibility.Visible;
        StatePanel.Visibility = Visibility.Visible;
        ValuePanel.Visibility = Visibility.Visible;
        Plate.Visibility = Visibility.Visible;

        var chips = _items.Where(p => p.Chip is not null).ToList();
        if (chips.Count == 0)
        {
            UpdateZoneLayout();
            Plate.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var item in chips.Where(p => p.Chip!.Kind == PreviewOverlayChipKind.Intel).OrderBy(p => p.Order))
        {
            IntelPanel.Children.Add(CreateChip(item));
        }

        foreach (var item in chips.Where(p => p.Chip!.Kind == PreviewOverlayChipKind.State).OrderBy(p => p.Order))
        {
            StatePanel.Children.Add(CreateChip(item));
        }

        foreach (var item in chips.Where(p => IsValueKind(p.Chip!.Kind)).OrderBy(p => p.Order))
        {
            ValuePanel.Children.Add(CreateChip(item));
        }

        // 各 zone 的行数预算：预警 / 状态固定 1 行，数值 1–3 行
        Fold(IntelPanel, 1);
        Fold(StatePanel, 1);
        Fold(ValuePanel, _maxValueRows);

        UpdateZoneLayout();
        Plate.Visibility = HasChips ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool HasChips
        => IntelPanel.Children.Count > 0 || StatePanel.Children.Count > 0 || ValuePanel.Children.Count > 0;

    private static bool IsValueKind(PreviewOverlayChipKind kind)
        => kind is PreviewOverlayChipKind.Value or PreviewOverlayChipKind.Damage or PreviewOverlayChipKind.Rep;

    /// <summary>
    /// 建一枚胶囊：外观与日志监控小窗的战斗胶囊一致（圆角 10、无边框、8×2、同字号），
    /// 颜色恢复为**分类 tone**（阶段 385）：红=受损、绿=受益、紫=电子战、黄=提示、白=中性。
    /// 预警胶囊没有战斗分类，按 severity（最近 / 最新 / 降级）映射成 红 / 黄 / 灰。
    /// </summary>
    private Border CreateChip(PreviewOverlayItem item)
    {
        var chip = item.Chip!;
        var isDamage = chip.Kind == PreviewOverlayChipKind.Damage;
        var isRep = chip.Kind == PreviewOverlayChipKind.Rep;
        var isIntel = chip.Kind == PreviewOverlayChipKind.Intel;

        var tone = isIntel ? ToneOfSeverity(item.Severity) : chip.Tone;
        var foreground = ToneBrush(tone);

        var border = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(0),
            Background = ToneSurfaceBrush(tone),
            Margin = new Thickness(0, 0, 4, 4),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Opacity = chip.Dimmed ? 0.55 : 1.0,
            // 单枚胶囊永远不允许比可用宽还宽（极端情况：超长英文标签 + 大字号 + 窄窗）。
            // 真的放不下时宁可裁掉，也不能把叠加窗撑到预览窗外面。
            MaxWidth = PanelWidth(),
            ClipToBounds = true,
        };

        if (isDamage)
        {
            // 折叠时伤害永不优先被砍（见 Fold）
            border.Tag = "pinned";
        }

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 2, 8, 2),
        };

        if (!string.IsNullOrEmpty(chip.Label))
        {
            content.Children.Add(ChipText(chip.Label, FontWeights.SemiBold, isIntel ? 0.85 : 1.0, foreground));
        }

        if (!string.IsNullOrEmpty(chip.Value))
        {
            content.Children.Add(ChipText(chip.Value, FontWeights.SemiBold, 1.0, foreground, 4));
        }

        var metaText = ComposeMeta(chip);
        if (!string.IsNullOrEmpty(metaText))
        {
            content.Children.Add(ChipText(metaText, FontWeights.Normal, 0.85, foreground, 4));
        }

        var layerBrush = isRep ? BuildLayerBrush(chip.LayerShare) : null;
        if (layerBrush is null)
        {
            border.Child = content;
        }
        else
        {
            // 遥修盾/甲/构微条贴在胶囊底边（数据可视化，不是装饰）
            var grid = new Grid();
            grid.Children.Add(content);
            grid.Children.Add(new Border
            {
                Height = 2,
                Background = layerBrush,
                CornerRadius = new CornerRadius(1),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(3, 0, 3, 2),
                IsHitTestVisible = false,
            });
            border.Child = grid;
        }

        return border;
    }

    /// <summary>胶囊里的一段文字：统一字号，靠字重 / 透明度区分（与日志监控小窗一致）。</summary>
    private TextBlock ChipText(string text, FontWeight weight, double opacity, Brush foreground, double leftMargin = 0)
        => new()
        {
            Text = text,
            FontFamily = InfoFontFamily,
            FontSize = _chipFontSize,
            FontWeight = weight,
            Foreground = foreground,
            Opacity = opacity,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(leftMargin, 0, 0, 0),
            LineHeight = Math.Round(_chipFontSize * 1.2, 2),
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        };

    /// <summary>单位 + 副信息：预警用「跳 · 2min」，战斗用「HP 42/s」。</summary>
    private static string ComposeMeta(PreviewOverlayChip chip)
    {
        if (string.IsNullOrEmpty(chip.Meta))
        {
            return chip.Unit;
        }

        if (string.IsNullOrEmpty(chip.Unit))
        {
            return chip.Meta;
        }

        return chip.Kind == PreviewOverlayChipKind.Intel
            ? $"{chip.Unit} · {chip.Meta}"
            : $"{chip.Unit} {chip.Meta}";
    }

    /// <summary>遥修微条：盾 / 甲 / 构 三段硬边渐变；不足两段或全 0 时返回 null。</summary>
    private static Brush? BuildLayerBrush(IReadOnlyList<double>? shares)
    {
        if (shares is null || shares.Count < 3)
        {
            return null;
        }

        var total = shares[0] + shares[1] + shares[2];
        if (total <= 0 || shares.Count(p => p > 0) < 2)
        {
            return null;
        }

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
        };

        var offset = 0d;
        var colors = new[] { LayerShieldColor, LayerArmorColor, LayerHullColor };
        for (var i = 0; i < 3; i++)
        {
            var share = shares[i] / total;
            if (share <= 0)
            {
                continue;
            }

            brush.GradientStops.Add(new GradientStop(colors[i], offset));
            offset = Math.Min(1d, offset + share);
            brush.GradientStops.Add(new GradientStop(colors[i], offset));
        }

        brush.Freeze();
        return brush;
    }

    private static readonly Color LayerShieldColor = Color.FromRgb(0x5F, 0xA8, 0xE8);
    private static readonly Color LayerArmorColor = Color.FromRgb(0xD9, 0xB4, 0x5A);
    private static readonly Color LayerHullColor = Color.FromRgb(0xE0, 0x66, 0x66);

    /// <summary>预警没有战斗分类：最近=红、最新=黄、降级=灰。</summary>
    private static string ToneOfSeverity(PreviewOverlaySeverity severity) => severity switch
    {
        PreviewOverlaySeverity.Critical => "harm",
        PreviewOverlaySeverity.Caution => "caution",
        _ => "neutral",
    };

    /// <summary>tone → 主题色（与日志监控战斗胶囊同一套：红 / 绿 / 紫 / 黄 / 中性）。</summary>
    private static Color ToneColor(string tone) => tone switch
    {
        "harm" => ThemeColor("SystemFillColorCriticalBrush", Color.FromRgb(0xC4, 0x2B, 0x1C)),
        "benefit" => ThemeColor("SystemFillColorSuccessBrush", Color.FromRgb(0x0F, 0x7B, 0x0F)),
        "ew" => Color.FromRgb(0xC0, 0x84, 0xFC),
        "caution" => ThemeColor("SystemFillColorCautionBrush", Color.FromRgb(0x9D, 0x5D, 0x00)),
        _ => ThemeColor("TextFillColorPrimaryBrush", Colors.White),
    };

    private static Color ThemeColor(string key, Color fallback)
    {
        try
        {
            return Application.Current?.TryFindResource(key) is SolidColorBrush brush ? brush.Color : fallback;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return fallback;
        }
    }

    private static Brush ToneBrush(string tone)
    {
        var brush = new SolidColorBrush(ToneColor(tone));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// 胶囊底色：把 tone 按 28% 混到深色底上，得到"日志监控胶囊 + 深色背板"合并后的一层实色。
    /// 叠加层没有底板，必须保证在游戏画面上仍有足够对比。
    /// </summary>
    private static Brush ToneSurfaceBrush(string tone)
    {
        var color = ToneColor(tone);
        const double mix = 0.28;
        var red = (byte)Math.Round(color.R * mix + 0x12 * (1 - mix));
        var green = (byte)Math.Round(color.G * mix + 0x16 * (1 - mix));
        var blue = (byte)Math.Round(color.B * mix + 0x1C * (1 - mix));
        var brush = new SolidColorBrush(Color.FromArgb(0xE6, red, green, blue));
        brush.Freeze();
        return brush;
    }


    /// <summary>zone 显隐与间距：第一个可见的 zone 不要上间距。</summary>
    private void UpdateZoneLayout()
    {
        var first = true;
        foreach (var panel in new[] { IntelPanel, StatePanel, ValuePanel })
        {
            var has = panel.Children.Count > 0;
            panel.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            if (!has)
            {
                continue;
            }

            panel.Margin = new Thickness(0, first ? 0 : 4, 0, 0);
            first = false;
        }
    }

    // ---------- 行数预算 / +N 折叠（阶段 382） ----------

    /// <summary>
    /// 把某个 zone 折叠到 maxRows 行：从末尾开始隐藏（伤害 / 溢出标记不参与），
    /// 追加一枚 <c>+N</c>；如果 +N 自己又撑出一行，就再多折叠一枚。
    /// </summary>
    private void Fold(WrapPanel panel, int maxRows)
    {
        if (panel.Children.Count == 0)
        {
            return;
        }

        // 移除上一次的 +N，并恢复所有胶囊可见
        for (var i = panel.Children.Count - 1; i >= 0; i--)
        {
            if (panel.Children[i] is FrameworkElement { Tag: "overflow" })
            {
                panel.Children.RemoveAt(i);
            }
        }

        foreach (FrameworkElement child in panel.Children)
        {
            child.Visibility = Visibility.Visible;
        }

        var width = PanelWidth();
        var hidden = 0;
        var guard = 0;
        while (RowCount(panel, width) > maxRows && guard++ < 64)
        {
            var last = LastFoldable(panel);
            if (last is null)
            {
                break;
            }

            last.Visibility = Visibility.Collapsed;
            hidden++;
        }

        if (hidden == 0)
        {
            return;
        }

        var marker = CreateOverflowChip(hidden, out var markerText);
        panel.Children.Add(marker);

        guard = 0;
        while (RowCount(panel, width) > maxRows && guard++ < 64)
        {
            var next = LastFoldable(panel);
            if (next is null)
            {
                break;
            }

            next.Visibility = Visibility.Collapsed;
            hidden++;
            markerText.Text = $"+{hidden}";
        }
    }

    private Border CreateOverflowChip(int count, out TextBlock text)
    {
        var foreground = ToneBrush("neutral");
        text = new TextBlock
        {
            Text = $"+{count}",
            FontFamily = InfoFontFamily,
            FontSize = _chipFontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground,
            Opacity = 0.8,
            VerticalAlignment = VerticalAlignment.Center,
        };

        return new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(0),
            Background = ToneSurfaceBrush("neutral"),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 4, 4),
            Child = text,
            Tag = "overflow",
        };
    }

    /// <summary>下一枚可以被折叠的胶囊（从末尾找，跳过 +N 与伤害）。</summary>
    private static FrameworkElement? LastFoldable(WrapPanel panel)
    {
        for (var i = panel.Children.Count - 1; i >= 0; i--)
        {
            if (panel.Children[i] is not FrameworkElement child || child.Visibility != Visibility.Visible)
            {
                continue;
            }

            if (Equals(child.Tag, "overflow") || Equals(child.Tag, "pinned"))
            {
                continue;
            }

            return child;
        }

        return null;
    }

    /// <summary>量一次并返回 zone 实际占了几行（按每个胶囊的顶边聚类，容差 4 DIP）。</summary>
    private static int RowCount(WrapPanel panel, double width)
    {
        panel.Measure(new Size(Math.Max(1, width), double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, Math.Max(1, panel.DesiredSize.Width), Math.Max(1, panel.DesiredSize.Height)));

        var rows = 0;
        var lastTop = double.NaN;
        foreach (UIElement child in panel.Children)
        {
            if (child.Visibility != Visibility.Visible)
            {
                continue;
            }

            var top = child.TransformToAncestor(panel).Transform(new Point(0, 0)).Y;
            if (double.IsNaN(lastTop) || Math.Abs(top - lastTop) > 4)
            {
                rows++;
                lastTop = top;
            }
        }

        return rows;
    }

    /// <summary>胶囊可用的宽度：底板已经不带边框/内边距（阶段 384），直接用上限。</summary>
    private double PanelWidth()
    {
        var cap = RootPanel.MaxWidth;
        if (double.IsInfinity(cap) || cap <= 0)
        {
            return 4096;
        }

        return Math.Max(80, cap);
    }

    private static bool AreClose(double left, double right)
    {
        if (double.IsInfinity(left) || double.IsInfinity(right))
        {
            return double.IsInfinity(left) && double.IsInfinity(right);
        }

        return Math.Abs(left - right) < 0.5;
    }

    /// <summary>解析字体名；未安装/非法名称返回 null（调用方保留原字体）。</summary>
    private static FontFamily? ResolveFontFamily(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return null;
        }

        try
        {
            return new FontFamily(family);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT);
    }

    /// <summary>按物理像素摆放（不激活、不改 Z 序，保证仍贴在预览窗口之上）。</summary>
    public void SetBounds(int x, int y, int width, int height)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetWindowBounds(hwnd, x, y, Math.Max(1, width), Math.Max(1, height));
    }

    /// <summary>窗口销毁时把悬停浮层一起关掉（ToolTip 是独立 popup，不会随窗口自动消失）。</summary>
    protected override void OnClosed(EventArgs e)
    {
        _detailTip.IsOpen = false;
        base.OnClosed(e);
    }
}
