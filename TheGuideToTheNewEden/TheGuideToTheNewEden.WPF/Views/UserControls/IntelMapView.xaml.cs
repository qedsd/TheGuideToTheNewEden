using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 预警小窗的星图（重设计版）：
/// <para>
/// 数据集合严格等于配置的 N 跳范围（<see cref="ChannelIntelSetting.IntelJumps"/>），所有星系都绘制；
/// “重点”只影响标签/节点亮度/星门强调，不删除任何星系。2D 坐标由 <c>SolarSystemPosHelper.ResetXY</c>
/// 做 2%–98% 分位适配 + 尾部软压缩，几何离群星系在右侧贴边提示，仍可悬停与右键解除。
/// </para>
/// </summary>
public partial class IntelMapView : UserControl
{
    private IntelSolarSystemMap? _intelMap;
    private ChannelIntelSetting? _setting;

    private static readonly Brush DefaultBrush = Frozen(Color.FromRgb(151, 169, 193));
    private static readonly Brush HomeBrush = Frozen(Color.FromRgb(70, 211, 155));
    private static readonly Brush IntelBrush = Frozen(Color.FromRgb(255, 138, 76));
    private static readonly Brush DowngradeBrush = Frozen(Color.FromRgb(242, 198, 92));
    private static readonly Brush QuietLineBrush = Frozen(Color.FromRgb(79, 102, 128));
    private static readonly Brush ActiveLineBrush = Frozen(Color.FromRgb(152, 102, 74));
    private static readonly Brush HoverLineBrush = Frozen(Color.FromRgb(135, 227, 205));
    private static readonly Brush OutlierBorderBrush = Frozen(Color.FromRgb(126, 105, 166));
    private static readonly Brush OutlierTextBrush = Frozen(Color.FromRgb(216, 200, 245));

    private const double QuietSize = 3.4;
    private const double IntelSize = 4.4;
    private const double HomeSize = 5.2;
    private const double IntelScale = 1.45;
    private const double DowngradeScale = 1.3;
    private const double MinZoom = 0.6;
    private const double MaxZoom = 4.0;

    private readonly Dictionary<int, Ellipse> _ellipseDic = [];
    private readonly Dictionary<int, TextBlock> _labelDic = [];
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

    /// <summary>点击右侧“几何离群”卡片时触发，窗口可据此展示最新预警。</summary>
    public event EventHandler<IntelSolarSystemMap>? OutlierActivated;

    /// <summary>右键解除单个预警时触发；窗口据此清掉 _startTimes 并刷新状态。</summary>
    public event EventHandler<int>? SystemClearRequested;

    /// <summary>当前预警中的星系数。</summary>
    public int ActiveCount => _intelings.Count;

    /// <summary>当前已降级的星系数。</summary>
    public int DowngradedCount => _downgradeds.Count;

    public IntelMapView()
    {
        InitializeComponent();
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

        UpdateOutlierPanel();
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

        UpdateOutlierPanel();
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

        UpdateOutlierPanel();
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
            UpdateOutlierPanel();
            return;
        }

        _allSolarSystem.AddRange(all);
        foreach (var item in _allSolarSystem)
        {
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

            var left = width * item.X2 - size;
            var top = height * item.Y2 - size;
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
            var labelLeft = left + size * 2 + 3;
            if (labelLeft + 90 > width)
            {
                labelLeft = Math.Max(2, left - 76);
            }
            Canvas.SetLeft(label, labelLeft);
            Canvas.SetTop(label, top - 2);
        }

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

        UpdateOutlierPanel();
    }

    private void ShowLabel(int systemId, bool visible)
    {
        if (_labelDic.TryGetValue(systemId, out var label))
        {
            label.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateOutlierPanel()
    {
        OutlierPanel.Children.Clear();
        var outliers = _allSolarSystem
            .Where(p => p.ViewportOutlier && (_intelings.Contains(p.SolarSystemID) || _downgradeds.Contains(p.SolarSystemID)))
            .ToList();
        if (outliers.Count == 0)
        {
            OutlierPanel.Visibility = Visibility.Collapsed;
            return;
        }

        OutlierPanel.Visibility = Visibility.Visible;
        foreach (var item in outliers.Take(3))
        {
            var border = new Border
            {
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(8, 6, 8, 7),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = OutlierBorderBrush,
                Background = Application.Current?.TryFindResource("CardBackgroundFillColorDefaultBrush") as Brush
                             ?? new SolidColorBrush(Color.FromArgb(235, 32, 28, 44)),
                Cursor = Cursors.Hand,
                Tag = item,
                ToolTip = $"{FindString("IntelWindow_OutlierTip")}\n{item.SolarSystemName}",
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = FindString("IntelWindow_Outlier"),
                FontSize = 9,
                Foreground = OutlierTextBrush,
                Opacity = 0.8,
            });
            stack.Children.Add(new TextBlock
            {
                Text = item.SolarSystemName,
                Margin = new Thickness(0, 1, 0, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = OutlierTextBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"{_intelMap?.JumpsOf(item.SolarSystemID) ?? 0} {FindString("EarlyWarningPage_Jumps")}",
                FontSize = 9,
                Foreground = OutlierTextBrush,
                Opacity = 0.66,
            });
            border.Child = stack;
            border.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                FocusSystem(item);
                OutlierActivated?.Invoke(this, item);
            };
            OutlierPanel.Children.Add(border);
        }

        if (outliers.Count > 3)
        {
            OutlierPanel.Children.Add(new TextBlock
            {
                Text = $"+{outliers.Count - 3}",
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = OutlierTextBrush,
                FontSize = 10,
                Opacity = 0.65,
            });
        }
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
        if (OutlierPanel.IsMouseOver)
        {
            return;
        }

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
}
