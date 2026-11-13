using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.Core.Models.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 预警小窗的星图（重设计版）：
/// <para>
/// 数据集合严格等于配置的 N 跳范围（<see cref="ChannelIntelSetting.IntelJumps"/>），所有星系都绘制；
/// “重点”只影响标签/节点亮度/星门强调，不删除任何星系。2D 坐标由 <c>SolarSystemPosHelper.ResetXY</c>
/// 阶段 312：2D 坐标按全量 min/max 等比适配（无几何离群贴边），星系可悬停与右键解除。
/// </para>
/// </summary>
public partial class IntelMapView : UserControl
{
    private IntelSolarSystemMap? _intelMap;
    private ChannelIntelSetting? _setting;
    private int _overlapStyle;

    private static readonly Brush DefaultBrush = Frozen(Color.FromRgb(151, 169, 193));
    private static readonly Brush HomeBrush = Frozen(Color.FromRgb(70, 211, 155));
    private static readonly Brush IntelBrush = Frozen(Color.FromRgb(255, 138, 76));
    private static readonly Brush DowngradeBrush = Frozen(Color.FromRgb(195, 164, 255));
    private static readonly Brush QuietLineBrush = Frozen(Color.FromRgb(79, 102, 128));
    private static readonly Brush ActiveLineBrush = Frozen(Color.FromRgb(152, 102, 74));
    private static readonly Brush HoverLineBrush = Frozen(Color.FromRgb(135, 227, 205));

    private const double QuietSize = 3.4;
    /// <summary>阶段 305：画布宽度低于该值（px）时不显示星系名标签，只用点的颜色/大小表达预警。</summary>
    private const double IntelSize = 4.4;
    private const double HomeSize = 5.2;
    private const double IntelScale = 1.45;
    private const double DowngradeScale = 1.3;
    private const double MinZoom = 0.6;
    private const double MaxZoom = 4.0;

    private readonly Dictionary<int, Ellipse> _ellipseDic = [];
    private readonly Dictionary<int, TextBlock> _labelDic = [];
    private readonly Dictionary<int, Point> _centerDic = [];
    private readonly List<IntelSolarSystemMap> _allSolarSystem = [];

    /// <summary>正处于预警中的星系。</summary>
    private readonly HashSet<int> _intelings = [];
    /// <summary>已降过级的星系。</summary>
    private readonly HashSet<int> _downgradeds = [];
    private readonly Dictionary<int, string> _intelContent = [];

    private Ellipse? _lastHovered;
    private double _zoom = 1;
    private bool _dragging;
    private Point _dragStart;
    private double _panStartX;
    private double _panStartY;

    /// <summary>右键解除单个预警时触发；窗口据此清掉 _startTimes 并刷新状态。</summary>
    public event EventHandler<int>? SystemClearRequested;

    /// <summary>当前预警中的星系数。</summary>
    public int ActiveCount => _intelings.Count;

    /// <summary>当前已降级的星系数。</summary>
    public int DowngradedCount => _downgradeds.Count;

    public IntelMapView()
    {
        InitializeComponent();
        // 阶段 307：全局设置变化（阈值/布局样式等）即时刷新星图
        IntelGlobalSettingService.SettingChanged += OnGlobalIntelSettingChanged;
    }

    private void OnGlobalIntelSettingChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateUI();
        }
        else
        {
            Dispatcher.BeginInvoke(UpdateUI);
        }
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public void Init(IntelSolarSystemMap intelMap, ChannelIntelSetting setting)
    {
        _intelMap = intelMap;
        _setting = setting;
        _overlapStyle = setting.OverlapStyle;
        setting.PropertyChanged -= Setting_PropertyChanged;
        setting.PropertyChanged += Setting_PropertyChanged;
        _intelings.Clear();
        _downgradeds.Clear();
        _intelContent.Clear();
        _zoom = 1;
        ZoomTransform.ScaleX = ZoomTransform.ScaleY = 1;
        PanTransform.X = PanTransform.Y = 0;
        UpdateUI();
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    private void Setting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChannelIntelSetting.OverlapStyle))
        {
            _overlapStyle = _setting?.OverlapStyle ?? 0;
            UpdateUI();
        }
    }

    // ---------- 预警状态 ----------

    public void Intel(EarlyWarningContent content)
    {
        // 先更新状态，再应用到已布局的节点：窗口尚未显示/尺寸还是 0 时收到的预警也不能丢。
        if (content.IntelType == IntelChatType.Intel)
        {
            _intelings.Add(content.SolarSystemId);
            _downgradeds.Remove(content.SolarSystemId);
            _intelContent[content.SolarSystemId] = content.Content;
            if (_ellipseDic.TryGetValue(content.SolarSystemId, out var intelEllipse))
            {
                SetIntelState(intelEllipse, content.SolarSystemId);
            }
        }
        else if (content.IntelType == IntelChatType.Clear)
        {
            _intelings.Remove(content.SolarSystemId);
            _downgradeds.Remove(content.SolarSystemId);
            _intelContent.Remove(content.SolarSystemId);
            if (_ellipseDic.TryGetValue(content.SolarSystemId, out var clearEllipse))
            {
                ResetEllipse(clearEllipse, content.SolarSystemId);
            }
            ShowLabel(content.SolarSystemId, content.SolarSystemId == _setting?.LocationID);
        }

    }

    public void Clear(IEnumerable<int> systemIds)
    {
        foreach (var id in systemIds.ToList())
        {
            _intelings.Remove(id);
            _downgradeds.Remove(id);
            _intelContent.Remove(id);
            if (_ellipseDic.TryGetValue(id, out var ellipse))
            {
                ResetEllipse(ellipse, id);
            }
            ShowLabel(id, id == _setting?.LocationID);
        }

    }

    public void Clear()
    {
        Clear(_intelings.Union(_downgradeds).ToList());
        _intelContent.Clear();
    }

    public void Downgrade(IEnumerable<int> systemIds)
    {
        foreach (var id in systemIds.ToList())
        {
            _intelings.Remove(id);
            _downgradeds.Add(id);
            if (_ellipseDic.TryGetValue(id, out var ellipse))
            {
                ellipse.Fill = DowngradeBrush;
                SetScale(ellipse, DowngradeScale);
                ShowLabel(id, true);
            }
        }

    }

    private void SetIntelState(Ellipse ellipse, int systemId)
    {
        ellipse.Fill = IntelBrush;
        SetScale(ellipse, IntelScale);
        ShowLabel(systemId, true);
    }

    private void ResetEllipse(Ellipse ellipse, int systemId)
    {
        SetScale(ellipse, 1);
        ellipse.Fill = systemId == _setting?.LocationID ? HomeBrush : DefaultBrush;
        ShowLabel(systemId, systemId == _setting?.LocationID);
    }

    private void SetScale(Ellipse ellipse, double scale)
    {
        if (Math.Abs(scale - 1) < 0.01)
        {
            ellipse.RenderTransform = null;
        }
        else
        {
            ellipse.RenderTransformOrigin = new Point(0.5, 0.5);
            ellipse.RenderTransform = new ScaleTransform(scale, scale);
        }
    }

    private bool IsHighlighted(int systemId)
        => systemId == _setting?.LocationID || _intelings.Contains(systemId) || _downgradeds.Contains(systemId);

    // ---------- 绘制 ----------

    public void UpdateUI()
    {
        if (_intelMap is null)
        {
            return;
        }

        MapCanvas.Children.Clear();
        LineCanvas.Children.Clear();
        LabelCanvas.Children.Clear();
        TempCanvas.Children.Clear();
        TipBorder.Visibility = Visibility.Collapsed;
        _lastHovered = null;
        _ellipseDic.Clear();
        _labelDic.Clear();
        _centerDic.Clear();
        _allSolarSystem.Clear();

        var width = MapCanvas.ActualWidth;
        var height = MapCanvas.ActualHeight;
        if (width <= 1 || height <= 1)
        {
            return;
        }

        var all = _intelMap.GetAllSolarSystem();
        if (all is not { Count: > 0 })
        {
                return;
        }

        _allSolarSystem.AddRange(all);
        var centers = BuildCenters(width, height);
        foreach (var item in _allSolarSystem)
        {
            var center = centers.TryGetValue(item.SolarSystemID, out var point)
                ? point
                : new Point(width * item.X2, height * item.Y2);
            var highlighted = IsHighlighted(item.SolarSystemID);
            var isHome = item.SolarSystemID == _setting?.LocationID;
            var size = isHome ? HomeSize : highlighted ? IntelSize : QuietSize;
            var ellipse = new Ellipse
            {
                StrokeThickness = 0,
                Tag = item,
                Width = size * 2,
                Height = size * 2,
                Fill = isHome ? HomeBrush : _intelings.Contains(item.SolarSystemID) ? IntelBrush
                    : _downgradeds.Contains(item.SolarSystemID) ? DowngradeBrush : DefaultBrush,
                Opacity = highlighted ? 1 : 0.82,
                Cursor = Cursors.Hand,
            };
            _ellipseDic[item.SolarSystemID] = ellipse;
            MapCanvas.Children.Add(ellipse);
            ellipse.MouseEnter += Ellipse_MouseEnter;
            ellipse.MouseLeave += Ellipse_MouseLeave;
            ellipse.MouseRightButtonUp += Ellipse_RightClick;

            var left = center.X - size;
            var top = center.Y - size;
            Canvas.SetLeft(ellipse, left);
            Canvas.SetTop(ellipse, top);

            if (highlighted)
            {
                SetScale(ellipse, _intelings.Contains(item.SolarSystemID) ? IntelScale
                    : _downgradeds.Contains(item.SolarSystemID) ? DowngradeScale : 1);
            }

            // 标签只给本地/预警/降级；安静星系没有标签，但仍然是可交互节点。
            var label = new TextBlock
            {
                Text = item.SolarSystemName,
                FontSize = 10,
                FontWeight = isHome ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isHome ? HomeBrush : _intelings.Contains(item.SolarSystemID) ? IntelBrush
                    : _downgradeds.Contains(item.SolarSystemID) ? DowngradeBrush : DefaultBrush,
                IsHitTestVisible = false,
                Visibility = highlighted ? Visibility.Visible : Visibility.Collapsed,
            };
            _labelDic[item.SolarSystemID] = label;
            LabelCanvas.Children.Add(label);
            _centerDic[item.SolarSystemID] = center;
        }

        // 阶段 304：可见标签做防重叠布局（替代固定左右摆放）
        RelayoutLabels(width, height);

        // 星门连线：所有 N 跳内星系的直接星门都画；有情报/本地的连线才加亮。
        var drawn = new HashSet<long>();
        foreach (var item in _allSolarSystem)
        {
            if (item.Jumps is not { Count: > 0 } || !_ellipseDic.TryGetValue(item.SolarSystemID, out var itemEllipse))
            {
                continue;
            }

            foreach (var jumpTo in item.Jumps)
            {
                if (!_ellipseDic.TryGetValue(jumpTo.SolarSystemID, out var jumpToEllipse))
                {
                    continue;
                }

                var min = Math.Min(item.SolarSystemID, jumpTo.SolarSystemID) - 30000000L;
                var max = Math.Max(item.SolarSystemID, jumpTo.SolarSystemID) - 30000000L;
                var mark = min * 100000000L + max;
                if (!drawn.Add(mark))
                {
                    continue;
                }

                var active = IsHighlighted(item.SolarSystemID) || IsHighlighted(jumpTo.SolarSystemID);
                LineCanvas.Children.Add(new Line
                {
                    X1 = Canvas.GetLeft(itemEllipse) + itemEllipse.Width / 2,
                    Y1 = Canvas.GetTop(itemEllipse) + itemEllipse.Height / 2,
                    X2 = Canvas.GetLeft(jumpToEllipse) + jumpToEllipse.Width / 2,
                    Y2 = Canvas.GetTop(jumpToEllipse) + jumpToEllipse.Height / 2,
                    StrokeThickness = active ? 1.4 : 0.9,
                    Stroke = active ? ActiveLineBrush : QuietLineBrush,
                    Opacity = active ? 0.9 : 0.78,
                });
            }
        }

    }

    /// <summary>
    /// 默认样式按 2D 坐标；SMT/Near2 按跳数分层：
    /// SMT 从本地开始向下逐跳，Near2 是 SMT 的纵向翻转（最外层在上、本地在下）。
    /// </summary>
    private Dictionary<int, Point> BuildCenters(double width, double height)
    {
        var centers = new Dictionary<int, Point>();
        if (_overlapStyle == 0 || _intelMap is null)
        {
            foreach (var item in _allSolarSystem)
            {
                centers[item.SolarSystemID] = new Point(width * item.X2, height * item.Y2);
            }

            return centers;
        }

        var groups = _intelMap.GroupByJump();
        if (groups.Count == 0)
        {
            return centers;
        }

        var perJumpHeight = height / groups.Count;
        for (var i = 0; i < groups.Count; i++)
        {
            var layer = _overlapStyle == 1 ? i : groups.Count - 1 - i;
            var layerItems = groups[i];
            if (layerItems.Length == 0)
            {
                continue;
            }

            var perWidth = width / layerItems.Length;
            for (var j = 0; j < layerItems.Length; j++)
            {
                centers[layerItems[j].SolarSystemID] = new Point(
                    j * perWidth + perWidth / 2,
                    layer * perJumpHeight + perJumpHeight / 2);
            }
        }

        return centers;
    }

    /// <summary>阶段 307：名称标签宽度阈值上收到全局设置；无设置时回退 0（总是显示名称）。</summary>
    private double GetLabelWidthThreshold()
        => IntelGlobalSettingService.Value.LabelWidthThreshold;
    private void ShowLabel(int systemId, bool visible)
    {
        if (_labelDic.TryGetValue(systemId, out var label))
        {
            // 阶段 305：窗口过小时不显示星系名（预警用点的颜色/大小表达），悬停/右键仍可用
            label.Visibility = visible && MapCanvas.ActualWidth >= GetLabelWidthThreshold()
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // 阶段 304：可见集合变了，重新做防重叠布局（把空出来的位置还给别的标签）
        RelayoutLabels(MapCanvas.ActualWidth, MapCanvas.ActualHeight);
    }

    /// <summary>
    /// 阶段 304：标签防重叠布局。
    /// <para>
    /// 可见标签按优先级排布：本地 &gt; 预警中 &gt; 已降级（同级保持创建顺序）。
    /// 每个标签在所属圆点四周尝试 12 个候选位（右/左/上/下 × 上下错行），取第一个
    /// 与已放置标签不相交的；全部碰撞时取重叠面积最小的。位置全程钳制在画布内。
    /// </para>
    /// </summary>
    private void RelayoutLabels(double width, double height)
    {
        // 阶段 305：小窗口不显示名称，只靠点的颜色/大小表达预警状态
        if (width < GetLabelWidthThreshold())
        {
            foreach (var label in _labelDic.Values)
            {
                label.Visibility = Visibility.Collapsed;
            }

            return;
        }

        if (width <= 1 || height <= 1 || _labelDic.Count == 0)
        {
            return;
        }

        var visible = new List<(int Id, TextBlock Label, Point Center, int Priority)>();
        foreach (var pair in _labelDic)
        {
            if (pair.Value.Visibility != Visibility.Visible)
            {
                continue;
            }

            if (!_centerDic.TryGetValue(pair.Key, out var center))
            {
                continue;
            }

            var priority = pair.Key == _setting?.LocationID ? 0
                : _intelings.Contains(pair.Key) ? 1
                : _downgradeds.Contains(pair.Key) ? 2 : 3;
            visible.Add((pair.Key, pair.Value, center, priority));
        }

        if (visible.Count == 0)
        {
            return;
        }

        visible.Sort((a, b) =>
        {
            var byPriority = a.Priority.CompareTo(b.Priority);
            return byPriority != 0 ? byPriority : a.Id.CompareTo(b.Id);
        });

        var placed = new List<Rect>();
        foreach (var entry in visible)
        {
            var label = entry.Label;
            var size = MeasureLabel(label);
            var w = size.Width;
            var h = size.Height;
            var r = entry.Center.X > 0 && _ellipseDic.TryGetValue(entry.Id, out var dot) ? dot.Width / 2 : 6;
            var cx = entry.Center.X;
            var cy = entry.Center.Y;

            // 候选位：右 / 左 / 上 / 下，以及右侧、左侧的上下错行（同点多标签时上下堆叠）
            (double x, double y)[] candidates =
            [
                (cx + r + 3, cy - h / 2),
                (cx - r - 3 - w, cy - h / 2),
                (cx - w / 2, cy - r - 3 - h),
                (cx - w / 2, cy + r + 3),
                (cx + r + 3, cy - h / 2 - h - 3),
                (cx + r + 3, cy + h / 2 + 3),
                (cx - r - 3 - w, cy - h / 2 - h - 3),
                (cx - r - 3 - w, cy + h / 2 + 3),
                (cx + r + 3, cy - h / 2 - (h + 3) * 2),
                (cx + r + 3, cy + h / 2 + h + 6),
                (cx - r - 3 - w, cy - h / 2 - (h + 3) * 2),
                (cx - r - 3 - w, cy + h / 2 + h + 6),
            ];

            Rect? best = null;
            var bestOverlap = double.MaxValue;
            foreach (var (x, y) in candidates)
            {
                var rect = ClampRect(new Rect(x, y, w, h), width, height);
                if (!placed.Any(p => p.IntersectsWith(rect)))
                {
                    best = rect;
                    break;
                }

                var overlap = placed.Sum(p => IntersectArea(p, rect));
                if (overlap < bestOverlap)
                {
                    bestOverlap = overlap;
                    best = rect;
                }
            }

            if (best is not { } chosen)
            {
                continue;
            }

            Canvas.SetLeft(label, chosen.X);
            Canvas.SetTop(label, chosen.Y);
            placed.Add(chosen);
        }
    }

    private static Rect ClampRect(Rect rect, double width, double height)
    {
        var x = Math.Clamp(rect.X, 2, Math.Max(2, width - rect.Width - 2));
        var y = Math.Clamp(rect.Y, 2, Math.Max(2, height - rect.Height - 2));
        return new Rect(x, y, rect.Width, rect.Height);
    }

    private static double IntersectArea(Rect a, Rect b)
    {
        a.Intersect(b);
        return a.IsEmpty ? 0 : a.Width * a.Height;
    }

    private Size MeasureLabel(TextBlock label)
    {
        var typeface = new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, FontStretches.Normal);
        var text = new FormattedText(
            label.Text ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            label.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        return new Size(Math.Max(56, text.Width + 4), Math.Max(12, text.Height + 2));
    }

    public void UpdateHome(IntelSolarSystemMap intelMap)
    {
        _intelMap = intelMap;
        _intelings.Clear();
        _downgradeds.Clear();
        _intelContent.Clear();
        UpdateUI();
    }

    // ---------- 缩放 / 平移 ----------

    public void ZoomIn() => ApplyZoom(1.18, new Point(ActualWidth / 2, ActualHeight / 2));

    public void ZoomOut() => ApplyZoom(1 / 1.18, new Point(ActualWidth / 2, ActualHeight / 2));

    public void ResetView()
    {
        _zoom = 1;
        ZoomTransform.ScaleX = ZoomTransform.ScaleY = 1;
        PanTransform.X = PanTransform.Y = 0;
    }

    private void ApplyZoom(double factor, Point origin)
    {
        var next = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(next - _zoom) < 0.001)
        {
            return;
        }

        var scale = next / _zoom;
        PanTransform.X = origin.X - (origin.X - PanTransform.X) * scale;
        PanTransform.Y = origin.Y - (origin.Y - PanTransform.Y) * scale;
        _zoom = next;
        ZoomTransform.ScaleX = ZoomTransform.ScaleY = next;
    }

    private void Root_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ApplyZoom(e.Delta > 0 ? 1.12 : 1 / 1.12, e.GetPosition(this));
        e.Handled = true;
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragStart = e.GetPosition(this);
        _panStartX = PanTransform.X;
        _panStartY = PanTransform.Y;
        CaptureMouse();
    }

    private void Root_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        PanTransform.X = _panStartX + (current.X - _dragStart.X);
        PanTransform.Y = _panStartY + (current.Y - _dragStart.Y);
    }

    private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
        }
    }

    private void Root_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ResetView();
        e.Handled = true;
    }

    private void FocusSystem(IntelSolarSystemMap item)
    {
        var width = MapCanvas.ActualWidth;
        var height = MapCanvas.ActualHeight;
        if (width <= 1 || height <= 1)
        {
            return;
        }

        _zoom = Math.Max(1.15, _zoom);
        ZoomTransform.ScaleX = ZoomTransform.ScaleY = _zoom;
        PanTransform.X = ActualWidth / 2 - width * item.X2 * _zoom;
        PanTransform.Y = ActualHeight / 2 - height * item.Y2 * _zoom;
    }

    // ---------- 交互 ----------

    private void Ellipse_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Ellipse ellipse || ellipse.Tag is not IntelSolarSystemMap map)
        {
            return;
        }

        TempCanvas.Children.Clear();
        CancelHover();

        if (map.Jumps is { Count: > 0 })
        {
            foreach (var jump in map.Jumps)
            {
                if (_ellipseDic.TryGetValue(jump.SolarSystemID, out var jumpToEllipse))
                {
                    TempCanvas.Children.Add(new Line
                    {
                        X1 = Canvas.GetLeft(ellipse) + ellipse.Width / 2,
                        Y1 = Canvas.GetTop(ellipse) + ellipse.Height / 2,
                        X2 = Canvas.GetLeft(jumpToEllipse) + jumpToEllipse.Width / 2,
                        Y2 = Canvas.GetTop(jumpToEllipse) + jumpToEllipse.Height / 2,
                        StrokeThickness = 1.4,
                        Stroke = HoverLineBrush,
                        Opacity = 0.9,
                    });
                }
            }
        }

        _lastHovered = ellipse;
        SetScale(ellipse, Math.Max(_intelings.Contains(map.SolarSystemID) ? IntelScale : 1.3, 1.3));

        var jumps = _intelMap?.JumpsOf(map.SolarSystemID) ?? -1;
        var text = $"{map.SolarSystemName}  {jumps} {FindString("EarlyWarningPage_Jumps")}";
        if (_intelContent.TryGetValue(map.SolarSystemID, out var content) && !string.IsNullOrWhiteSpace(content))
        {
            text += $"\n{content}";
        }
        TipTextBlock.Text = text;
        TipBorder.Visibility = Visibility.Visible;

        var position = e.GetPosition(this);
        var tipWidth = TipBorder.ActualWidth > 0 ? TipBorder.ActualWidth : 240;
        var tipHeight = TipBorder.ActualHeight > 0 ? TipBorder.ActualHeight : 60;
        var left = position.X + 14;
        var top = position.Y + 16;
        if (left + tipWidth > ActualWidth)
        {
            left = Math.Max(4, position.X - tipWidth - 14);
        }
        if (top + tipHeight > ActualHeight)
        {
            top = Math.Max(4, position.Y - tipHeight - 14);
        }

        TipBorder.Margin = new Thickness(left, top, 0, 0);
    }

    private void Ellipse_MouseLeave(object sender, MouseEventArgs e)
    {
        TempCanvas.Children.Clear();
        CancelHover();
    }

    private void CancelHover()
    {
        if (_lastHovered is not null)
        {
            RestoreNodeScale(_lastHovered);
            _lastHovered = null;
        }

        TipBorder.Visibility = Visibility.Collapsed;
    }

    private void RestoreNodeScale(Ellipse ellipse)
    {
        if (ellipse.Tag is not IntelSolarSystemMap map)
        {
            SetScale(ellipse, 1);
            return;
        }

        SetScale(
            ellipse,
            _intelings.Contains(map.SolarSystemID) ? IntelScale
            : _downgradeds.Contains(map.SolarSystemID) ? DowngradeScale
            : 1);
    }

    private void Ellipse_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Ellipse { Tag: IntelSolarSystemMap map }
            && (_intelings.Contains(map.SolarSystemID) || _downgradeds.Contains(map.SolarSystemID)))
        {
            CancelHover();
            if (SystemClearRequested is null)
            {
                Clear([map.SolarSystemID]);
            }
            else
            {
                SystemClearRequested.Invoke(this, map.SolarSystemID);
            }
            e.Handled = true;
        }
    }

    private void MapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateUI();
    }

    /// <summary>阶段 307：控件卸载时解绑全局设置事件。</summary>
    public void DetachGlobalSetting()
        => IntelGlobalSettingService.SettingChanged -= OnGlobalIntelSettingChanged;
}
