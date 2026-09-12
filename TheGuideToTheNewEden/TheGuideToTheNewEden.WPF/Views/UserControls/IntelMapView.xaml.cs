using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 预警小窗的星图（对齐 WinUI 版 DefaultIntelOverlapPage）：
/// 以 N 跳范围内星系为节点按归一化坐标（X2/Y2，由 SolarSystemPosHelper.ResetXY 计算）布点，
/// 星门连线；颜色语义 —— 家=海绿、常规=暗灰、预警中=橙红（放大 1.5 倍）、已降级=黄。
/// 悬停放大并显示星系名/跳数/最新预警内容、高亮相邻星门连线；右键点击预警中的星系可解除该星系预警。
/// </summary>
public partial class IntelMapView : UserControl
{
    private IntelSolarSystemMap? _intelMap;
    private ChannelIntelSetting? _setting;

    private static readonly SolidColorBrush DefaultBrush = new(Colors.DarkGray);
    private static readonly SolidColorBrush HomeBrush = new(Colors.MediumSeaGreen);
    private static readonly SolidColorBrush IntelBrush = new(Colors.OrangeRed);
    private static readonly SolidColorBrush DowngradeBrush = new(Colors.Yellow);
    private static readonly SolidColorBrush DefaultLineBrush = new(Colors.DarkGray);
    private static readonly SolidColorBrush TempLineBrush = new(Color.FromRgb(135, 227, 205));

    private const int DefaultWidth = 8;
    private const int HomeWidth = 12;
    private const double IntelScale = 1.5;

    private Ellipse? _lastHovered;
    private readonly Dictionary<int, Ellipse> _ellipseDic = [];
    private readonly List<IntelSolarSystemMap> _allSolarSystem = [];

    /// <summary>正处于预警中的星系。</summary>
    private readonly HashSet<int> _intelings = [];
    /// <summary>已降过级的星系。</summary>
    private readonly HashSet<int> _downgradeds = [];
    private readonly Dictionary<int, string> _intelContent = [];

    public IntelMapView()
    {
        InitializeComponent();
    }

    public void Init(IntelSolarSystemMap intelMap, ChannelIntelSetting setting)
    {
        _intelMap = intelMap;
        _setting = setting;
        UpdateUI();
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    // ---------- 预警状态 ----------

    public void Intel(EarlyWarningContent content)
    {
        if (_ellipseDic.TryGetValue(content.SolarSystemId, out var ellipse))
        {
            if (content.IntelType == Core.Enums.IntelChatType.Intel)
            {
                SetIntelState(ellipse, content.SolarSystemId);
                _intelings.Add(content.SolarSystemId);
                _downgradeds.Remove(content.SolarSystemId);
                _intelContent.Remove(content.SolarSystemId);
                _intelContent.Add(content.SolarSystemId, content.Content);
            }
            else if (content.IntelType == Core.Enums.IntelChatType.Clear)
            {
                _intelings.Remove(content.SolarSystemId);
                _downgradeds.Remove(content.SolarSystemId);
                _intelContent.Remove(content.SolarSystemId);
                ResetEllipse(ellipse, content.SolarSystemId);
            }
        }
    }

    public void Clear(List<int> systemIds)
    {
        foreach (var id in systemIds)
        {
            if (_ellipseDic.TryGetValue(id, out var ellipse))
            {
                ResetEllipse(ellipse, id);
                _intelings.Remove(id);
                _downgradeds.Remove(id);
                _intelContent.Remove(id);
            }
        }
    }

    public void Clear()
    {
        Clear([.. _intelings]);
    }

    public void Downgrade(List<int> systemIds)
    {
        foreach (var id in systemIds)
        {
            if (_ellipseDic.TryGetValue(id, out var ellipse))
            {
                ellipse.Fill = DowngradeBrush;
                SetScale(ellipse, IntelScale);
                _intelings.Remove(id);
                _downgradeds.Add(id);
            }
        }
    }

    private void SetIntelState(Ellipse ellipse, int systemId)
    {
        if (ellipse.Fill != IntelBrush)
        {
            ellipse.Fill = IntelBrush;
            SetScale(ellipse, IntelScale);
        }
    }

    private void ResetEllipse(Ellipse ellipse, int systemId)
    {
        SetScale(ellipse, 1);
        ellipse.Fill = systemId == _setting?.LocationID ? HomeBrush : DefaultBrush;
    }

    private void SetScale(Ellipse ellipse, double scale)
    {
        if (Math.Abs(scale - 1) < 0.01)
        {
            ellipse.RenderTransform = null;
        }
        else
        {
            // 以中心为原点缩放（WinUI 版靠手工平移 Canvas.Left/Top 补偿，WPF 直接用 RenderTransform）
            ellipse.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            ellipse.RenderTransform = new ScaleTransform(scale, scale);
        }
    }

    // ---------- 绘制 ----------

    public void UpdateUI()
    {
        if (_intelMap is null)
        {
            return;
        }

        MapCanvas.Children.Clear();
        LineCanvas.Children.Clear();
        TempCanvas.Children.Clear();
        _lastHovered = null;
        TipTextBlock.Visibility = Visibility.Collapsed;

        var width = MapCanvas.ActualWidth - 16;
        var height = MapCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _ellipseDic.Clear();
        _allSolarSystem.Clear();
        var all = _intelMap.GetAllSolarSystem();
        if (all is not { Count: > 0 })
        {
            return;
        }

        _allSolarSystem.AddRange(all);
        foreach (var item in _allSolarSystem)
        {
            var ellipse = new Ellipse
            {
                StrokeThickness = 0,
                Tag = item,
                Width = item.SolarSystemID == _intelMap.SolarSystemID ? HomeWidth : DefaultWidth,
                Height = item.SolarSystemID == _intelMap.SolarSystemID ? HomeWidth : DefaultWidth,
                Fill = item.SolarSystemID == _intelMap.SolarSystemID ? HomeBrush : DefaultBrush,
                Cursor = Cursors.Hand,
            };
            _ellipseDic.Add(item.SolarSystemID, ellipse);
            MapCanvas.Children.Add(ellipse);
            ellipse.MouseEnter += Ellipse_MouseEnter;
            ellipse.MouseLeave += Ellipse_MouseLeave;
            ellipse.MouseRightButtonUp += Ellipse_RightClick;
            Canvas.SetLeft(ellipse, width * item.X2);
            Canvas.SetTop(ellipse, height * item.Y2);
        }

        // 星门连线（按两端 ID 去重）
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

                var min = Math.Min(item.SolarSystemID, jumpTo.SolarSystemID) - 30000000;
                var max = Math.Max(item.SolarSystemID, jumpTo.SolarSystemID) - 30000000;
                var mark = min * 100000000L + max;
                if (!drawn.Add(mark))
                {
                    continue;
                }

                var line = new Line
                {
                    X1 = Canvas.GetLeft(itemEllipse) + itemEllipse.Width / 2,
                    Y1 = Canvas.GetTop(itemEllipse) + itemEllipse.Height / 2,
                    X2 = Canvas.GetLeft(jumpToEllipse) + jumpToEllipse.Width / 2,
                    Y2 = Canvas.GetTop(jumpToEllipse) + jumpToEllipse.Height / 2,
                    StrokeThickness = 1,
                    Stroke = DefaultLineBrush,
                };
                LineCanvas.Children.Add(line);
            }
        }

        // 重绘预警中 / 已降级的星系
        foreach (var id in _downgradeds)
        {
            if (_ellipseDic.TryGetValue(id, out var ellipse))
            {
                ellipse.Fill = DowngradeBrush;
                SetScale(ellipse, IntelScale);
            }
        }

        foreach (var id in _intelings)
        {
            if (_ellipseDic.TryGetValue(id, out var ellipse))
            {
                ellipse.Fill = IntelBrush;
                SetScale(ellipse, IntelScale);
            }
        }
    }

    public void UpdateHome(IntelSolarSystemMap intelMap)
    {
        _intelMap = intelMap;
        UpdateUI();
    }

    // ---------- 交互 ----------

    private void Ellipse_MouseEnter(object sender, MouseEventArgs e)
    {
        var ellipse = sender as Ellipse;
        if (ellipse is null || _lastHovered == ellipse || ellipse.Tag is not IntelSolarSystemMap map)
        {
            return;
        }

        TempCanvas.Children.Clear();
        CancelHover();

        // 相邻星系高亮连线
        if (map.JumpTo is { Count: > 0 })
        {
            foreach (var jump in map.JumpTo)
            {
                if (_ellipseDic.TryGetValue(jump, out var jumpToEllipse))
                {
                    var line = new Line
                    {
                        X1 = Canvas.GetLeft(ellipse) + ellipse.Width / 2,
                        Y1 = Canvas.GetTop(ellipse) + ellipse.Height / 2,
                        X2 = Canvas.GetLeft(jumpToEllipse) + jumpToEllipse.Width / 2,
                        Y2 = Canvas.GetTop(jumpToEllipse) + jumpToEllipse.Height / 2,
                        StrokeThickness = 2,
                        Stroke = TempLineBrush,
                    };
                    TempCanvas.Children.Add(line);
                }
            }
        }

        _lastHovered = ellipse;
        SetScale(ellipse, IntelScale);

        var jumps = _intelMap?.JumpsOf(map.SolarSystemID) ?? -1;
        var tip = $"{map.SolarSystemName} {jumps} {FindString("EarlyWarningPage_Jumps")}";
        if (_intelContent.TryGetValue(map.SolarSystemID, out var content))
        {
            tip += $"\n{content}";
        }

        TipTextBlock.Text = tip;
        TipTextBlock.Visibility = Visibility.Visible;
        // 提示跟随星系位置（靠右侧的星系提示放左边）
        var left = Canvas.GetLeft(ellipse);
        TipTextBlock.Margin = new Thickness(
            left > ActualWidth / 2 ? Math.Max(0, left - 270) : left + 16,
            Math.Max(0, Canvas.GetTop(ellipse) - 28),
            0,
            0);
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
            SetScale(_lastHovered, 1);
            _lastHovered = null;
        }

        TipTextBlock.Visibility = Visibility.Collapsed;
    }

    private void Ellipse_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Ellipse { Tag: IntelSolarSystemMap map } && _intelings.Contains(map.SolarSystemID))
        {
            CancelHover();
            Clear([map.SolarSystemID]);
        }
    }

    private void MapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateUI();
    }
}
