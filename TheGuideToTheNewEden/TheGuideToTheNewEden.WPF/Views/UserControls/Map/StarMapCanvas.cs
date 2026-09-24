using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using TheGuideToTheNewEden.WPF.Services.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

public enum MapColorMode
{
    /// <summary>按安全等级着色（高安青绿 / 低安橙 / 00 红）。</summary>
    Security,
    /// <summary>按主权联盟分组着色（需要 ESI 主权数据）。</summary>
    Sovereignty,
    /// <summary>按行星资源热力着色（需要本地库行星资源数据）。</summary>
    PlanetResource,
    /// <summary>按击杀热度着色（需要 ESI 统计数据）。</summary>
    Kills,
    /// <summary>按通行量热度着色（需要 ESI 统计数据）。</summary>
    Jumps,
}

/// <summary>星图节点（一个恒星系）。坐标为归一化世界坐标（保持纵横比，长边 = 1）。</summary>
public sealed class MapSystemNode
{
    public int Id { get; init; }
    public string Name { get; set; } = string.Empty;
    public int RegionId { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public double Security { get; init; }
    /// <summary>归一化世界坐标 X（0..1）。</summary>
    public double NX { get; set; }
    /// <summary>归一化世界坐标 Y（0..1，已做 Y 轴翻转，屏幕方向）。</summary>
    public double NY { get; set; }

    /// <summary>节点颜色（按着色模式在数据装载/切换时统一计算）。</summary>
    public SKColor Color { get; set; }

    /// <summary>热度值（击杀/通行量），着色模式为热度时使用；-1 表示无数据。</summary>
    public double Heat { get; set; } = -1;

    /// <summary>该星系所属主权分组（SOV 着色用；0 = 无主权）。</summary>
    public long GroupId { get; set; }

    /// <summary>该星系所属主权联盟 ID（0 = 无主权；主权模式圆点叠加联盟徽标用）。</summary>
    public long AllianceId { get; set; }

    /// <summary>行星资源值（行星资源着色用；-1 表示无数据）。</summary>
    public double Resource { get; set; } = -1;

    /// <summary>是否启用（星域 / 安等批量筛选用；false 时画成灰化色，交互不受影响）。</summary>
    public bool Enabled { get; set; } = true;

    public bool Visible { get; internal set; }
}

/// <summary>情报红圈标记（一个星系一条，聚合该星系的 ZKB 击杀与频道情报）。</summary>
public sealed class IntelMarker
{
    public int SystemId { get; init; }

    /// <summary>威胁权重（Σ舰船数 + 频道消息数 × 5），决定圆圈大小。</summary>
    public double Weight { get; set; }

    /// <summary>该星系的舰船统计（舰船类型 ID → 数量），高缩放下逐个画图标。</summary>
    public IReadOnlyDictionary<int, int> Ships { get; init; } = new Dictionary<int, int>();

    /// <summary>舰船总数。</summary>
    public int ShipCount { get; set; }

    /// <summary>ZKB 击杀消息条数。</summary>
    public int ZkbCount { get; set; }

    /// <summary>频道情报条数。</summary>
    public int ChannelCount { get; set; }

    /// <summary>最早一条情报的时间（显示"多少分钟前"）。</summary>
    public DateTime OldestUtc { get; set; }
}

/// <summary>角色标记。</summary>
public sealed class CharacterMarker
{
    public long CharacterId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int SystemId { get; init; }
}

/// <summary>
/// SkiaSharp 星图画布（替代 WinUI 版 Win2D MapCanvas 的重设计实现）：
/// 深空背景 + 视差星尘 + 星门连线 + 跳桥点线 + 发光星系节点
/// （按安等 / 主权分组 / 行星资源 / 击杀 / 通行量着色），
/// 情报红圈脉冲（含舰船图标与计数）、角色头像标记、航线发光折线与流动光点、一跳覆盖圈。
/// 滚轮缩放（以鼠标为锚）、拖拽平移、悬停高亮、单击选中、定位飞行动画。
/// LOD：连线在放大后淡入，安等数字与星系名按缩放分级显示；视口外节点全部剔除。
/// 连续动画（脉冲/飞行/流动光点）由 CompositionTarget.Rendering 驱动，静止时不重绘。
/// </summary>
public class StarMapCanvas : SKElement
{
    // ---------- 数据 ----------
    private MapSystemNode[] _nodes = [];
    private Dictionary<int, int> _indexById = new();
    private (int A, int B)[] _links = [];
    private List<int>[] _adjacency = [];

    // 世界尺寸（长边 = 1，短边按纵横比缩放）
    private double _worldW = 1, _worldH = 1;

    // 视图状态：screen = world * zoom + offset
    private double _zoom = 1, _offsetX = 0, _offsetY = 0;
    private double _fitZoom = 1;
    private bool _needFit = true;

    // ---------- 覆盖层数据 ----------
    private List<IntelMarker> _intel = [];
    private List<CharacterMarker> _characters = [];
    private readonly Dictionary<long, SKBitmap?> _characterImages = [];
    private readonly Dictionary<long, SKBitmap> _sovIcons = [];
    private readonly Dictionary<long, (SKImage Image, SKBitmap Src)> _sovLogoSprites = [];   // 联盟→圆形徽标精灵（v18：圆裁+描边预烘，Src 用于检测源位图更换）
    private readonly Dictionary<int, SKBitmap?> _intelShipImages = [];
    private IReadOnlyList<int> _routePath = [];
    private IReadOnlyList<int> _routeWaypoints = [];

    // 跳桥（点线，画进底图缓存）与一跳覆盖圈（覆盖层）
    private List<(int A, int B)> _bridges = [];
    private bool _showBridges = true;
    private HashSet<int> _coverIds = [];

    // ---------- 交互状态 ----------
    private MapSystemNode? _hovered;
    private MapSystemNode? _selected;
    private MapSystemNode? _highlight;
    private DateTime _highlightTime;
    private bool _dragging;
    private bool _dragMoved;
    private Point _dragLast;
    private double _lastHoverTest;

    // 飞行动画
    private (double zoom, double x, double y) _animFrom;
    private (double zoom, double x, double y) _animTo;
    private DateTime _animStart;
    private TimeSpan _animDuration = TimeSpan.Zero;

    // 脉冲相位（秒）
    private double _pulsePhase;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    // 底图缓存（v21 pad-and-shift）：key **不含 offset**——纯拖动时固定 zoom 下世界层只整体平移，
    // 命中缓存只做 1:1 裁切贴图（零重画），拖出 pad 边界才重新烘焙；zoom/尺寸/dpi/数据/模式/fade 任一变化仍全量重建。
    private SKBitmap? _baseCache;
    private (double zoom, int w, int h, float dpi, int dataVer, int colorMode, int sovFadeFrame)? _baseKey;
    private double _baseBakeOffsetX, _baseBakeOffsetY;   // 烘焙时的视口偏移：拖动位移 = 当前 − 烘焙（决定裁切窗口与重建时机）
    private int _dataVersion;
    private MapColorMode _currentColorMode = MapColorMode.Security;

    // v21 拖动优化（pad-and-shift）参数
    private const float BasePadDips = 256f;
    private float _basePadUsed;                 // 当前缓存实际使用的 pad（DIP）；0 = 超预算回退模式（旧行为）
    private float _bakePad;                     // 烘焙期间的 pad；0 = 普通帧（各绘制方法据此把出屏裁剪外扩到 pad 环）
    private SKBitmap? _bgUnderlay;              // 背景渐变底（**屏幕锚定**，烘进 pad 位图会随拖动漂移 → 单独半分辨率缓存）
    private (int W, int H, float Dpi, bool Light)? _bgKey;
#pragma warning disable CS0618
    private readonly SKPaint _bgBlitPaint = new() { FilterQuality = SKFilterQuality.Low };
    private readonly SKPaint _baseBlitPaint = new() { FilterQuality = SKFilterQuality.Low };   // 底图贴图（拖动位移常为亚像素，双线性防像素爬行）
#pragma warning restore CS0618

    // ---------- 绘制资源 ----------
    /// <summary>
    /// 画布字体候选（**必须中英通吃**）：Segoe UI 没有 CJK 字形，星系名会渲染成"口口口"；
    /// 这里按"中文 UI 字体优先"的顺序挑系统里第一个真实存在的家族。
    /// </summary>
    private static readonly string[] PreferredFontFamilies =
    [
        "Microsoft YaHei UI",
        "Microsoft YaHei",
        "微软雅黑",
        "PingFang SC",
        "Noto Sans CJK SC",
        "Source Han Sans SC",
        "Meiryo",
        "Yu Gothic UI",
        "Malgun Gothic",
        "Segoe UI",
    ];

    private static readonly SKTypeface _typeface = ResolveTypeface(SKFontStyle.Normal);
    private static readonly SKTypeface _typefaceBold = ResolveTypeface(SKFontStyle.Bold);

    /// <summary>画 emoji（📢 / 🕒）用的字体：Windows 上首选 Segoe UI Emoji；拿不到返回 null（调用方退回 UI 字体）。</summary>
    private static readonly SKTypeface? _emojiTypeface = ResolveEmojiTypeface();

    private static SKTypeface? ResolveEmojiTypeface()
    {
        foreach (var family in new[] { "Segoe UI Emoji", "Segoe UI Symbol" })
        {
            var typeface = SKTypeface.FromFamilyName(family, SKFontStyle.Normal);
            if (typeface is not null && string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase))
            {
                return typeface;
            }
        }

        return null;
    }

    /// <summary>
    /// 取一款存在的中文字体：逐个候选家族创建，**并用返回的 FamilyName 校验是否真的命中**
    /// （<c>FromFamilyName</c> 找不到家族时会静默回退到默认字体，只能靠 FamilyName 判断）；
    /// 全都没命中时让 Skia 按"中"字兜一个能显示中文的字体，最后才退回默认。
    /// </summary>
    private static SKTypeface ResolveTypeface(SKFontStyle style)
    {
        foreach (var family in PreferredFontFamilies)
        {
            var typeface = SKTypeface.FromFamilyName(family, style);
            if (typeface is not null && string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase))
            {
                return typeface;
            }
        }

        try
        {
            var fallback = SKFontManager.Default.MatchCharacter('星');
            if (fallback is not null)
            {
                return SKTypeface.FromFamilyName(fallback.FamilyName, style) ?? fallback;
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return SKTypeface.Default;
    }

    private readonly List<(double x, double y, float r, byte a, SKColor c)> _starsFar = [];
    private readonly List<(double x, double y, float r, byte a, SKColor c)> _starsNear = [];

    /// <summary>节点外发光精灵缓存（按颜色量化缓存，避免每帧每节点创建渐变着色器）。</summary>
    private readonly Dictionary<int, SKImage> _glowCache = [];

    private const double MaxZoomMultiplier = 600;
    private const double MinZoomMultiplier = 0.5;
    private const int StarCountFar = 220;
    private const int StarCountNear = 140;
    /// <summary>单个星系最多画几个舰船图标（其余折叠成 "+N"）。</summary>
    private const int MaxIntelShipIcons = 8;
    /// <summary>画舰船图标所需的最小缩放倍数（对齐 WinUI 的 zoom &gt; 12）。</summary>
    private const double IntelShipIconZoom = 12;
    /// <summary>显示"多少分钟前"所需的最小缩放倍数（对齐 WinUI 的 zoom &gt; 16）。</summary>
    private const double IntelElapsedZoom = 16;

    public StarMapCanvas()
    {
        GenerateStars();
        PaintSurface += OnPaintSurface;
        Loaded += (_, _) =>
        {
            if (_needFit)
            {
                FitView();
            }
            CompositionTarget.Rendering += OnRendering;
        };
        Unloaded += (_, _) =>
        {
            CompositionTarget.Rendering -= OnRendering;
            ReleaseCaches();
        };
        SizeChanged += (_, _) => { _needFit = true; InvalidateVisual(); };
    }

    /// <summary>连续动画驱动：有活动动画时每帧重绘，静止时不产生任何开销。</summary>
    private void OnRendering(object? sender, EventArgs e)
    {
        if (HasActiveAnimation)
        {
            InvalidateVisual();
        }
    }

    private bool HasActiveAnimation =>
        _animDuration > TimeSpan.Zero
        || _intel.Count > 0
        || _routePath.Count > 1
        || (_highlight is not null && (DateTime.UtcNow - _highlightTime).TotalMilliseconds < 1200);

    // ---------- 事件 ----------
    /// <summary>悬停节点变化（null = 离开）。</summary>
    public event EventHandler<MapSystemNode?>? HoveredChanged;
    /// <summary>单击选中节点变化（null = 点空白取消）。</summary>
    public event EventHandler<MapSystemNode?>? SelectedChanged;

    public MapSystemNode? Selected => _selected;
    public MapSystemNode? Hovered => _hovered;

    // ---------- 数据装载 ----------

    /// <summary>
    /// 装载星系与星门数据（星门按两端 Id 去重）。
    /// 节点坐标（NX/NY）应已按世界坐标归一化并保持纵横比，本方法内部平移到 [0,1] 并把长边缩放为 1。
    /// </summary>
    public void SetData(IReadOnlyList<MapSystemNode> nodes, IEnumerable<(int From, int To)> links)
    {
        _nodes = nodes.ToArray();
        // 数据换了：热力色块作废（等页面按新数据再次 SetColorMode 时重建）
        _heatCells = [];
        _heatRanks = [];
        _indexById = new Dictionary<int, int>(_nodes.Length);
        var minX = double.MaxValue;
        var maxX = double.MinValue;
        var minY = double.MaxValue;
        var maxY = double.MinValue;
        foreach (var node in _nodes)
        {
            if (node.NX < minX) minX = node.NX;
            if (node.NX > maxX) maxX = node.NX;
            if (node.NY < minY) minY = node.NY;
            if (node.NY > maxY) maxY = node.NY;
        }

        var spanX = Math.Max(1e-9, maxX - minX);
        var spanY = Math.Max(1e-9, maxY - minY);
        var scale = 1.0 / Math.Max(spanX, spanY);
        _worldW = spanX * scale;
        _worldH = spanY * scale;
        for (var i = 0; i < _nodes.Length; i++)
        {
            var node = _nodes[i];
            node.NX = (node.NX - minX) * scale;
            node.NY = (node.NY - minY) * scale;
            node.Color = SecurityColor(node.Security);
            _indexById[node.Id] = i;
        }

        // 星门连线（去重、剔除不在图内的端点）+ 邻接表
        _adjacency = new List<int>[_nodes.Length];
        for (var i = 0; i < _adjacency.Length; i++)
        {
            _adjacency[i] = [];
        }

        var seen = new HashSet<long>();
        var linkList = new List<(int, int)>();
        foreach (var (from, to) in links)
        {
            if (!_indexById.TryGetValue(from, out var a) || !_indexById.TryGetValue(to, out var b))
            {
                continue;
            }

            var lo = Math.Min(a, b);
            var hi = Math.Max(a, b);
            var mark = lo * 1000000L + hi;
            if (!seen.Add(mark))
            {
                continue;
            }

            linkList.Add((a, b));
            _adjacency[a].Add(b);
            _adjacency[b].Add(a);
        }
        _links = [.. linkList];

        _dataVersion++;
        _needFit = true;
        InvalidateVisual();
    }

    // ---------- 视图操作 ----------

    private void FitView()
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 10 || h < 10 || _worldW <= 0)
        {
            return;
        }

        var margin = 0.92; // 留少量边距
        _fitZoom = Math.Min(w / _worldW, h / _worldH) * margin;
        _zoom = _fitZoom;
        _offsetX = (w - _worldW * _zoom) / 2;
        _offsetY = (h - _worldH * _zoom) / 2;
        _needFit = false;
        InvalidateVisual();
    }

    /// <summary>飞向指定星系并高亮。zoomMultiplier 为相对整图适配视图的倍数。</summary>
    public void ToSystem(int id, double zoomMultiplier = 24, bool highlight = true)
    {
        if (!_indexById.TryGetValue(id, out var index))
        {
            return;
        }

        if (_needFit)
        {
            FitView();
        }

        var node = _nodes[index];
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 10 || h < 10)
        {
            return;
        }

        var targetZoom = _fitZoom * Math.Clamp(zoomMultiplier, MinZoomMultiplier, MaxZoomMultiplier);
        var targetX = w / 2 - node.NX * _worldW * targetZoom;
        var targetY = h / 2 - node.NY * _worldH * targetZoom;
        FlyTo(targetZoom, targetX, targetY);
        if (highlight)
        {
            _highlight = node;
            _highlightTime = DateTime.UtcNow;
        }
    }

    /// <summary>飞向星域（按星域内节点包围盒适配视图）。</summary>
    public void ToRegion(int regionId)
    {
        if (_nodes.Length == 0)
        {
            return;
        }

        if (_needFit)
        {
            FitView();
        }

        var minX = double.MaxValue;
        var maxX = double.MinValue;
        var minY = double.MaxValue;
        var maxY = double.MinValue;
        foreach (var node in _nodes)
        {
            if (node.RegionId != regionId)
            {
                continue;
            }

            if (node.NX < minX) minX = node.NX;
            if (node.NX > maxX) maxX = node.NX;
            if (node.NY < minY) minY = node.NY;
            if (node.NY > maxY) maxY = node.NY;
        }

        if (minX > maxX)
        {
            return;
        }

        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 10 || h < 10)
        {
            return;
        }

        var spanX = Math.Max(1e-6, (maxX - minX) * _worldW);
        var spanY = Math.Max(1e-6, (maxY - minY) * _worldH);
        var targetZoom = Math.Min(w / spanX, h / spanY) * 0.75;
        var targetX = w / 2 - (minX + (maxX - minX) / 2) * _worldW * targetZoom;
        var targetY = h / 2 - (minY + (maxY - minY) / 2) * _worldH * targetZoom;
        FlyTo(targetZoom, targetX, targetY);
    }

    /// <summary>飞回全图概览并清除高亮（顶栏星域下拉选「全部」时用；边距与 FitView 一致）。</summary>
    public void ToOverview()
    {
        if (_nodes.Length == 0 || _worldW <= 0)
        {
            return;
        }

        if (_needFit)
        {
            FitView();
        }

        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 10 || h < 10)
        {
            return;
        }

        var targetZoom = Math.Min(w / _worldW, h / _worldH) * 0.92;
        var targetX = (w - _worldW * targetZoom) / 2;
        var targetY = (h - _worldH * targetZoom) / 2;
        _highlight = null;
        FlyTo(targetZoom, targetX, targetY);
    }

    private void FlyTo(double zoom, double x, double y)
    {
        _animFrom = (_zoom, _offsetX, _offsetY);
        _animTo = (Math.Clamp(zoom, _fitZoom * MinZoomMultiplier, _fitZoom * MaxZoomMultiplier), x, y);
        _animStart = DateTime.UtcNow;
        _animDuration = TimeSpan.FromMilliseconds(420);
        InvalidateVisual();
    }

    // ---------- 覆盖层 ----------

    public void SetIntel(IEnumerable<IntelMarker> markers)
    {
        _intel = markers?.ToList() ?? [];
        InvalidateVisual();
    }

    public void SetCharacters(IEnumerable<CharacterMarker> markers)
    {
        _characters = markers?.ToList() ?? [];
        // 清理已不存在的头像缓存
        var ids = _characters.Select(p => p.CharacterId).ToHashSet();
        foreach (var key in _characterImages.Keys.Where(p => !ids.Contains(p)).ToList())
        {
            if (_characterImages[key] is { } bmp)
            {
                bmp.Dispose();
            }
            _characterImages.Remove(key);
        }
        InvalidateVisual();
    }

    /// <summary>设置角色头像（可为 null 占位；位图生命周期由画布管理）。</summary>
    public void SetCharacterImage(long characterId, SKBitmap? bitmap)
    {
        if (_characterImages.TryGetValue(characterId, out var old) && old is not null)
        {
            old.Dispose();
        }
        _characterImages[characterId] = bitmap;
        InvalidateVisual();
    }

    /// <summary>
    /// 设置联盟徽标（主权模式的节点图标，画进底图缓存 → 每来一个就使缓存失效重建一次）。
    /// </summary>
    public void SetSovIcon(long allianceId, SKBitmap? bitmap)
    {
        if (allianceId <= 0 || bitmap is null)
        {
            return;
        }

        // 同一引用重复回调（VM 缓存命中路径每次装载都会发）不能 Dispose：旧精灵还引用它（ReferenceEquals 判重）
        if (_sovIcons.TryGetValue(allianceId, out var old) && old is not null && !ReferenceEquals(old, bitmap))
        {
            old.Dispose();
        }
        else if (old is not null && ReferenceEquals(old, bitmap))
        {
            return;   // 已是这张图，无需失效重建
        }

        _sovIcons[allianceId] = bitmap;
        _dataVersion++;
        InvalidateVisual();
    }

    /// <summary>设置航线（途经星系 Id 列表，含中间路径）；waypointIndices 为关键航点在 path 中的下标。</summary>
    public void SetRoute(IReadOnlyList<int> path, IReadOnlyList<int> waypointIndices)
    {
        _routePath = path ?? [];
        _routeWaypoints = waypointIndices ?? [];
        InvalidateVisual();
    }

    public void ClearRoute()
    {
        _routePath = [];
        _routeWaypoints = [];
        InvalidateVisual();
    }

    /// <summary>设置跳桥连线（两端星系 Id）。跳桥画在底图缓存里，因此会触发底图重建。</summary>
    public void SetBridges(IEnumerable<(int A, int B)> bridges, bool show = true)
    {
        _bridges = bridges?.ToList() ?? [];
        _showBridges = show;
        _dataVersion++;
        InvalidateVisual();
    }

    /// <summary>设置一跳覆盖高亮（星系 Id 集合；null / 空集合表示清除）。</summary>
    public void SetCover(IEnumerable<int>? systemIds)
    {
        _coverIds = systemIds is null ? [] : systemIds.ToHashSet();
        InvalidateVisual();
    }

    /// <summary>设置情报舰船图标（可为 null 占位；位图生命周期由画布管理）。</summary>
    public void SetIntelShipImage(int shipTypeId, SKBitmap? bitmap)
    {
        if (_intelShipImages.TryGetValue(shipTypeId, out var old) && old is not null)
        {
            old.Dispose();
        }
        _intelShipImages[shipTypeId] = bitmap;
        InvalidateVisual();
    }

    // ---------- 命中测试 ----------

    public MapSystemNode? FindNodeAt(Point point)
    {
        MapSystemNode? best = null;
        var bestDist = double.MaxValue;
        var zmult = _fitZoom > 0 ? _zoom / _fitZoom : 1;
        var r = NodeRadius(zmult);
        foreach (var node in _nodes)
        {
            var x = node.NX * _worldW * _zoom + _offsetX;
            var y = node.NY * _worldH * _zoom + _offsetY;
            if (x < point.X - r - 4 || x > point.X + r + 4 || y < point.Y - r - 4 || y > point.Y + r + 4)
            {
                continue;
            }

            var d = (x - point.X) * (x - point.X) + (y - point.Y) * (y - point.Y);
            var limit = (r + 4) * (r + 4);
            if (d <= limit && d < bestDist)
            {
                bestDist = d;
                best = node;
            }
        }
        return best;
    }

    public bool TryGetNode(int id, out MapSystemNode? node)
    {
        if (_indexById.TryGetValue(id, out var index))
        {
            node = _nodes[index];
            return true;
        }
        node = null;
        return false;
    }

    /// <summary>取指定星系的星门邻接星系 Id。</summary>
    public IReadOnlyList<int> GetNeighbors(int id)
    {
        if (_indexById.TryGetValue(id, out var index))
        {
            return _adjacency[index];
        }
        return [];
    }

    /// <summary>重置视图为整图适配。</summary>
    public void Fit()
    {
        _needFit = true;
        FitView();
    }

    /// <summary>
    /// 节点 <see cref="MapSystemNode.Enabled"/> 被外部（星域 / 安等筛选）批量改动后调用：
    /// 递增数据版本以重建底图（灰化画在底图里，缓存键不含 Enabled，必须显式失效）。
    /// </summary>
    public void RefreshNodeStates()
    {
        _dataVersion++;
        InvalidateVisual();
    }

    /// <summary>筛选项灰化色：把原色向中性灰收敛（保留一点色相便于分辨安等区间）。</summary>
    // ---------- 主题调色板 ----------
    // 画布不是 XAML，拿不到 DynamicResource：所有颜色按 _light 二选一。
    // 亮色是"海图"风格——浅底深字，强调色整体加深（原青色 7DF9FF 在白底上几乎不可见）。

    /// <summary>是否亮色调色板（由页面根据应用主题调用 <see cref="SetTheme"/>）。</summary>
    private bool _light;

    /// <summary>切换亮/暗调色板；变化时递增数据版本以重建底图。</summary>
    public void SetTheme(bool light)
    {
        if (_light == light)
        {
            return;
        }

        _light = light;
        _dataVersion++;
        InvalidateVisual();
    }

    private SKColor BgBase => _light ? new SKColor(245, 247, 252) : new SKColor(4, 6, 12);
    private SKColor BgCenter => _light ? new SKColor(252, 253, 255) : new SKColor(13, 20, 42);
    private SKColor BgMid => _light ? new SKColor(233, 238, 248) : new SKColor(6, 9, 20);
    private SKColor BgEdge => _light ? new SKColor(215, 224, 240) : new SKColor(3, 4, 10);

    /// <summary>星尘：亮色主题下改画深蓝灰小点（原调色板是亮色，白底不可见）。</summary>
    private SKColor StarColor(SKColor c, byte a) => _light
        ? new SKColor(72, 88, 122, (byte)Math.Min(255, a * 3 / 2))
        : new SKColor(c.Red, c.Green, c.Blue, a);

    private float LinkAlpha(double zmult) => (float)Math.Clamp((zmult - 1.05) * (_light ? 80f : 55f), 0, _light ? 120f : 80f);

    private SKColor Accent(byte a) => _light ? new SKColor(9, 132, 160, a) : new SKColor(125, 249, 255, a);
    private SKColor AccentDim(byte a) => _light ? new SKColor(10, 116, 144, a) : new SKColor(150, 235, 255, a);
    private SKColor RouteFlow(byte a) => _light ? new SKColor(8, 145, 178, a) : new SKColor(220, 255, 255, a);
    private SKColor Bridge => _light ? new SKColor(110, 120, 138, 185) : new SKColor(205, 212, 224, 165);
    private SKColor BadgeBg(byte a) => _light ? new SKColor(255, 255, 255, a) : new SKColor(10, 14, 24, a);
    private SKColor BadgeText(byte a) => _light ? new SKColor(27, 36, 55, a) : new SKColor(235, 242, 255, a);
    private SKColor NameText(byte a) => _light ? new SKColor(52, 62, 86, a) : new SKColor(205, 220, 245, a);
    private SKColor Kernel(byte a) => _light ? new SKColor(20, 28, 46, a) : new SKColor(255, 255, 255, a);
    private SKColor HoverRing(byte a) => _light ? new SKColor(27, 36, 55, a) : new SKColor(255, 255, 255, a);

    private SKColor IntelGlow(byte a) => _light
        ? new SKColor(206, 32, 54, (byte)(a * 55 / 100))
        : new SKColor(255, 59, 80, a);
    private SKColor IntelRed(byte a) => _light ? new SKColor(206, 32, 54, a) : new SKColor(255, 72, 92, a);
    private SKColor IntelText(byte a) => _light ? new SKColor(122, 22, 38, a) : new SKColor(255, 235, 235, a);
    private SKColor IntelSoft(byte a) => _light ? new SKColor(206, 60, 78, a) : new SKColor(255, 150, 160, a);
    private SKColor IntelTime(byte a) => _light ? new SKColor(178, 100, 22, a) : new SKColor(255, 200, 150, a);

    private SKColor CharBlue(byte a) => _light ? new SKColor(24, 108, 180, a) : new SKColor(88, 196, 255, a);
    private SKColor CharText(byte a) => _light ? new SKColor(38, 78, 128, a) : new SKColor(160, 214, 255, a);

    private static SKColor DimColor(SKColor color) => LerpColor(color, new SKColor(96, 102, 116), 0.72f);

    // ---------- 输入 ----------

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.18 : 1 / 1.18;
        var zmult = _fitZoom > 0 ? _zoom / _fitZoom : 1;
        var newZmult = Math.Clamp(zmult * factor, MinZoomMultiplier, MaxZoomMultiplier);
        var newZoom = newZmult * _fitZoom;

        // 以鼠标为锚缩放
        var p = e.GetPosition(this);
        var wx = (p.X - _offsetX) / _zoom;
        var wy = (p.Y - _offsetY) / _zoom;
        _offsetX = p.X - wx * newZoom;
        _offsetY = p.Y - wy * newZoom;
        _zoom = newZoom;
        Debug.WriteLine(_zoom);
        _animDuration = TimeSpan.Zero; // 手动缩放打断飞行动画
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragMoved = false;
        _dragLast = e.GetPosition(this);
        CaptureMouse();
        Focus();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        if (_dragging)
        {
            var dx = p.X - _dragLast.X;
            var dy = p.Y - _dragLast.Y;
            if (_dragMoved || Math.Abs(dx) > 3 || Math.Abs(dy) > 3)
            {
                _dragMoved = true;
                _offsetX += dx;
                _offsetY += dy;
                _dragLast = p;
                _animDuration = TimeSpan.Zero;
                Cursor = Cursors.SizeAll;
                InvalidateVisual();
            }
        }
        else
        {
            // 悬停命中测试节流（30ms）
            var now = _clock.Elapsed.TotalMilliseconds;
            if (now - _lastHoverTest > 30)
            {
                _lastHoverTest = now;
                var hit = FindNodeAt(p);
                if (!ReferenceEquals(hit, _hovered))
                {
                    _hovered = hit;
                    Cursor = hit is null ? Cursors.Arrow : Cursors.Hand;
                    HoveredChanged?.Invoke(this, hit);
                    InvalidateVisual();
                }
            }
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
            Cursor = _hovered is null ? Cursors.Arrow : Cursors.Hand;
            if (!_dragMoved)
            {
                var hit = FindNodeAt(e.GetPosition(this));
                _selected = hit;
                SelectedChanged?.Invoke(this, hit);
                InvalidateVisual();
            }
            e.Handled = true;
        }
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        var hit = FindNodeAt(e.GetPosition(this));
        if (hit is null && _selected is not null)
        {
            _selected = null; // 右键空白清除选中
            SelectedChanged?.Invoke(this, null);
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (!_dragging && _hovered is not null)
        {
            _hovered = null;
            HoveredChanged?.Invoke(this, null);
            InvalidateVisual();
        }
        base.OnMouseLeave(e);
    }

    // ---------- 渲染 ----------

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(BgBase);

        var w = (float)ActualWidth;
        var h = (float)ActualHeight;
        if (w < 10 || h < 10 || _nodes.Length == 0)
        {
            return;
        }

        // DPI 缩放：之后全部使用 DIP 坐标
        var dpiScale = e.Info.Width / w;
        _renderDpiScale = dpiScale;
        canvas.Scale(dpiScale, dpiScale);

        if (_needFit)
        {
            FitView();
        }

        // 推进飞行动画
        if (_animDuration > TimeSpan.Zero)
        {
            var t = (DateTime.UtcNow - _animStart) / _animDuration;
            if (t >= 1)
            {
                _animDuration = TimeSpan.Zero;
                _zoom = _animTo.zoom;
                _offsetX = _animTo.x;
                _offsetY = _animTo.y;
            }
            else
            {
                var ease = 1 - Math.Pow(1 - t, 3); // easeOutCubic
                _zoom = _animFrom.zoom + (_animTo.zoom - _animFrom.zoom) * ease;
                _offsetX = _animFrom.x + (_animTo.x - _animFrom.x) * ease;
                _offsetY = _animFrom.y + (_animTo.y - _animFrom.y) * ease;
            }
        }
        _pulsePhase = _clock.Elapsed.TotalSeconds;

        var zmult = _fitZoom > 0 ? _zoom / _fitZoom : 1;
        var nodeR = NodeRadius(zmult);

        // 底图缓存（v21 pad-and-shift）：key **不含 offset**。纯拖动（zoom/尺寸/dpi/数据/模式/fade 全不变）命中缓存 →
        // 世界层零重画，只从 pad 位图按当前位移裁切 1:1 贴图；位移超出 pad 边界 → 以当前视口为中心重新烘焙。
        // key 必须含 _sovFadeFrame：fade 期间每帧自增 → 强制 RebuildBase 真画推进淡化（既有机制不变）。
        var key = (_zoom, (int)w, (int)h, dpiScale, _dataVersion, (int)_currentColorMode, _sovFadeFrame);
        var padOk = _basePadUsed > 0;
        var dx = _offsetX - _baseBakeOffsetX;
        var dy = _offsetY - _baseBakeOffsetY;
        var cacheHit = _baseCache is not null && _baseKey is not null && _baseKey.Value == key
            && padOk && Math.Abs(dx) <= _basePadUsed && Math.Abs(dy) <= _basePadUsed;
        if (_baseCache is null || _baseKey is null || !cacheHit)
        {
            RebuildBase(e.Info.Width, e.Info.Height, w, h, zmult, nodeR);
            _baseKey = key;
            dx = 0; dy = 0;   // 刚烘焙：烘焙 offset = 当前 offset，无位移
        }

        // 屏幕锚定层：渐变底（半分辨率缓存贴图）+ 视差星尘（速度与地图不同，必须实时画）
        DrawBgUnderlay(canvas, e.Info.Width, e.Info.Height, w, h);
        using (var bgPaint = new SKPaint())
        {
            DrawStarLayer(canvas, bgPaint, _starsFar, 0.22, zmult);
            DrawStarLayer(canvas, bgPaint, _starsNear, 0.5, zmult);
        }

        // 世界层：pad 缓存命中 = 1:1 裁切贴图（零重画）；pad=0 回退 = 全图贴（旧行为）
        DrawBaseLayer(canvas, w, h, dx, dy);

        using var paint = new SKPaint { IsAntialias = true };

        if (_coverIds.Count > 0)
        {
            DrawCover(canvas, paint, nodeR);
        }

        if (_routePath.Count > 1)
        {
            DrawRoute(canvas, paint, zmult, nodeR);
        }

        if (_intel.Count > 0)
        {
            DrawIntel(canvas, paint, nodeR, zmult);
        }

        if (_characters.Count > 0)
        {
            DrawCharacters(canvas, paint, nodeR, zmult);
        }

        DrawHover(canvas, paint, nodeR);
        DrawSelection(canvas, paint, nodeR);
        DrawHighlight(canvas, paint, nodeR);
    }

    /// <summary>
    /// 释放底图与发光精灵缓存（页面切走时调用）。
    /// **必须同时清掉 <see cref="_baseKey"/>**：否则切回星图时"缓存仍有效"的判断会误判，
    /// 直接拿已释放的 null 位图去画 → DrawBitmap 抛 ArgumentException（阶段 65 实机：打开星图→切主页面→切回）。
    /// </summary>
    private void ReleaseCaches()
    {
        _baseCache?.Dispose();
        _baseCache = null;
        _baseKey = null;
        _bgUnderlay?.Dispose();
        _bgUnderlay = null;
        _bgKey = null;

        foreach (var kv in _glowCache)
        {
            kv.Value.Dispose();
        }

        _glowCache.Clear();

        foreach (var kv in _sovLogoSprites)
        {
            kv.Value.Image.Dispose();
        }

        _sovLogoSprites.Clear();

        _sovVectorOffscreenCanvas?.Dispose();
        _sovVectorOffscreenCanvas = null;
        _sovVectorOffscreen?.Dispose();
        _sovVectorOffscreen = null;
        _sovScreenCache?.Dispose();
        _sovScreenCache = null;
        _sovScreenKey = null;
    }

    /// <summary>重建底图缓存（背景 + 星尘 + 星门连线 + 跳桥点线 + 全部节点与标签）。</summary>
    /// <remarks>
    /// 位图按尺寸复用：拖拽 / 滚轮 / 飞行动画期间每帧都会重建底图，尺寸不变时不再重新分配，
    /// 避免每帧 new SKBitmap（4K + 高 DPI 下约 30MB/帧）带来的 LOH 压力。
    /// </remarks>
    /// <summary>
    /// 贴底图层：pad 缓存有效时从 pad 位图按拖动位移裁出当前视口的 1:1 子矩形；否则全图贴（pad=0 旧行为）。
    /// 拖动位移常为亚像素（DPI 缩放/分数偏移），双线性采样防止像素爬行。
    /// </summary>
    private void DrawBaseLayer(SKCanvas canvas, float w, float h, double dx, double dy)
    {
        if (_baseCache is null)
        {
            return;
        }

        if (_basePadUsed > 0)
        {
            // pad 位图：设备像素域。烘焙时屏幕坐标 s 的内容落在位图 (s+pad)×scale 处；
            // 内容在烘焙后移动了 dx（当前 offset − 烘焙 offset）→ 采样窗口起点 = pad − dx。
            //（符号反了会变成"拖动方向镜像"——实测踩过）
            var dpi = (float)(_baseKey?.dpi ?? 1);
            var src = new SKRect(
                (float)((_basePadUsed - dx) * dpi),
                (float)((_basePadUsed - dy) * dpi),
                (float)((_basePadUsed - dx + w) * dpi),
                (float)((_basePadUsed - dy + h) * dpi));
            #pragma warning disable CS0618
            canvas.DrawBitmap(_baseCache, src, new SKRect(0, 0, w, h), _baseBlitPaint);
            #pragma warning restore CS0618
        }
        else
        {
            canvas.DrawBitmap(_baseCache, new SKRect(0, 0, w, h));
        }
    }

    /// <summary>背景渐变底（半分辨率烘焙、整帧贴图）：**屏幕锚定**，烘进 pad 位图会随拖动漂移 → 独立缓存。</summary>
    private void DrawBgUnderlay(SKCanvas canvas, int deviceW, int deviceH, float w, float h)
    {
        var bw = Math.Max(1, deviceW / 2);
        var bh = Math.Max(1, deviceH / 2);
        var k = (bw, bh, (float)_renderDpiScale, _light);
        if (_bgUnderlay is null || _bgKey != k)
        {
            _bgUnderlay?.Dispose();
            _bgUnderlay = new SKBitmap(bw, bh);
            using var c = new SKCanvas(_bgUnderlay);
            c.Clear(BgBase);
            c.Scale(bw / w, bh / h);
            using var p = new SKPaint();
            DrawBackgroundGradientOnly(c, p, w, h);
            _bgKey = k;
        }

        #pragma warning disable CS0618
        canvas.DrawBitmap(_bgUnderlay, new SKRect(0, 0, w, h), _bgBlitPaint);
        #pragma warning restore CS0618
    }

    private void DrawBackgroundGradientOnly(SKCanvas canvas, SKPaint paint, float w, float h)
    {
        using var bg = SKShader.CreateRadialGradient(
            new SKPoint(w * 0.5f, h * 0.42f), Math.Max(w, h) * 0.85f,
            [BgCenter, BgMid, BgEdge],
            [0f, 0.55f, 1f], SKShaderTileMode.Clamp);
        paint.Shader = bg;
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRect(0, 0, w, h, paint);
        paint.Shader = null;
    }

    private void RebuildBase(int deviceW, int deviceH, float w, float h, double zmult, float nodeR)
    {
        // v21 拖动优化（pad-and-shift）：底图只烘**世界层**（晕染/连线/跳桥/节点/标签，透明底），
        // 烘焙视口 = 视口 + 四周 BasePadDips DIP；纯拖动帧命中缓存只做 1:1 裁切贴图（零重画），
        // 拖出 pad 边界才重新烘焙（重建频率从"每帧"降到"每 256DIP"）。
        // 屏幕锚定内容（渐变底/视差星尘）烘进去会随拖动漂移/速度不对 → 留在实时层。
        // pad 预算：pad 位图像素 ≤ 2.25× 视口设备像素（每边 256DIP；4K 约 33MB）；超预算退 pad=0（旧行为）。
        var devicePixels = (double)deviceW * deviceH;
        var pad = (float)(devicePixels * 2.25 <= 24_000_000 ? BasePadDips : 0);
        _basePadUsed = pad;
        _bakePad = pad;

        // 烘焙视口（DIP）：pad>0 = (-pad,-pad, w+pad, h+pad)；pad=0 = (0,0,w,h)
        // 烘焙 offset 记录为当前视口：贴图裁切按 (当前 offset − 烘焙 offset + pad) 定位。
        _baseBakeOffsetX = _offsetX;
        _baseBakeOffsetY = _offsetY;
        var vpW = w + pad * 2;
        var vpH = h + pad * 2;
        var bakeW = (int)Math.Ceiling(vpW * deviceW / w);
        var bakeH = (int)Math.Ceiling(vpH * deviceH / h);
        var cache = _baseCache;
        if (cache is null || cache.Width != bakeW || cache.Height != bakeH)
        {
            _baseCache?.Dispose();
            _baseCache = new SKBitmap(bakeW, bakeH);
            cache = _baseCache;
        }

        using var baseCanvas = new SKCanvas(cache);
        baseCanvas.Clear(SKColors.Transparent);   // 世界层透明底；渐变/星尘在实时层先画
        baseCanvas.Scale(bakeW / vpW, bakeH / vpH);
        baseCanvas.Translate(pad, pad);   // 屏幕坐标 s（含烘焙 offset）→ 位图 (s+pad)×scale；与 offset 数值无关

        using var paint = new SKPaint { IsAntialias = true };
        using var font = new SKFont(_typeface, 10);

        DrawHeatMap(baseCanvas, paint, vpW, vpH);
        DrawLinks(baseCanvas, paint, zmult);
        if (_showBridges && _bridges.Count > 0)
        {
            DrawBridges(baseCanvas, paint);
        }

        DrawNodes(baseCanvas, paint, font, zmult, nodeR);
        _bakePad = 0;
    }

    // ---------- 热力图（行星资源 / 击杀 / 通行） ----------

    /// <summary>热力网格的默认格数（世界长边切成多少格）——网格挂在世界坐标上（不是屏幕坐标），
    /// 同一块区域的颜色在缩放/平移时不会变；放大只是把同一块画得更大。格数可由图例面板的滑条调整并持久化。</summary>
    public const int DefaultHeatGridCells = 30;
    /// <summary>热力格数下限（最少格数 = 块最大，8 格 = 世界长边切 8 份）。图例滑条档位 1..100 线性映射到
    /// [本值, <see cref="MaxHeatGridCells"/>]：档位 100 → 本值（即旧版滑条满档的块大小）。</summary>
    public const int MinHeatGridCells = 4;
    /// <summary>热力格数上限（最多格数 = 块最小）。图例滑条档位 1..100 线性映射：档位 1 → 本值。</summary>
    public const int MaxHeatGridCells = 50;

    /// <summary>当前热力网格格数（世界长边；格数越多色块越小）。</summary>
    public int HeatGridCells { get; private set; } = DefaultHeatGridCells;

    /// <summary>调整热力网格格数（即色块大小：格数越少块越大）。合法范围 = Min/MaxHeatGridCells，
    /// 越界静默钳制（含默认值）；图例滑条档位 1..100 经线性映射落入本范围（见 MapPage.HeatSizeSlider_ValueChanged）。</summary>
    public void SetHeatGridSize(int cells)
    {
        cells = Math.Clamp(cells, MinHeatGridCells, MaxHeatGridCells);
        if (HeatGridCells == cells)
        {
            return;
        }

        HeatGridCells = cells;
        _dataVersion++;
        RebuildHeatGrid();
        InvalidateVisual();
    }

    /// <summary>热力网格原点的归一化偏移（相对数据包围盒左上角；正 = 右/下移，1.0 = 世界宽/高）。
    /// 聚合与绘制共用同一偏移，保证色块边界与星系落格始终一致。</summary>
    public double HeatGridOffsetX { get; private set; }
    public double HeatGridOffsetY { get; private set; }

    /// <summary>方向按钮的单步平移量 = 当前格归一化步长的 1/4（随格子大小自适应）。</summary>
    public double HeatGridNudgeStep => _worldW <= 0 ? 0.05 : _worldW / Math.Max(1, HeatGridCells) / 4.0;

    /// <summary>绝对设置热力网格偏移（页面加载回填），变化才重建。</summary>
    public void SetHeatGridOffset(double x, double y)
    {
        x = Math.Clamp(x, -1, 1);
        y = Math.Clamp(y, -1, 1);
        if (HeatGridOffsetX == x && HeatGridOffsetY == y)
        {
            return;
        }

        HeatGridOffsetX = x;
        HeatGridOffsetY = y;
        _dataVersion++;
        RebuildHeatGrid();
        InvalidateVisual();
    }

    /// <summary>按归一化增量平移热力网格（图例方向按钮），越界钳到 ±1。</summary>
    public void NudgeHeatGridOffset(double dx, double dy) => SetHeatGridOffset(HeatGridOffsetX + dx, HeatGridOffsetY + dy);

    /// <summary>热力网格偏移复位（回到默认的左上角对齐）。</summary>
    public void ResetHeatGridOffset() => SetHeatGridOffset(0, 0);

    // ---------- 热力：主权聚合（联盟疆域凸包） ----------

    private bool _heatBySov;                                        // 图例开关：热力按主权联盟聚合
    private bool _sovActive;                                        // 本次重建实际生效（主权数据可用才置位，否则回退格子）
    private readonly List<(long GroupId, SKPoint Center, SKPoint Size)> _sovBlobs = [];   // 联盟疆域片（连通分量聚类，飞地各成一片）：片中心+包围盒宽高（归一化）
    private readonly Dictionary<long, string> _sovNames = [];       // 联盟分组号 → 联盟名（主权文字提示）
    private Dictionary<long, (double Sum, List<SKPoint> Pts)>? _sovLastByGroup;   // 最近一次主权聚合数据（晕染层跟随缩放重建时用）
    private SKBitmap? _sovLayer;                                    // 当前显示的晕染位图（3200 固定宽，只在数据变化时后台重烘一次）
    private SKBitmap? _sovFadeFrom;                                 // 交叉淡化中的旧层（仅数据变化交接时触发，300ms 后 Dispose）
    private SKBitmap? _sovBuiltLayer;                               // 后台线程烘好、待下帧交接的新层
    private DateTime _sovFadeStart;                                 // 交叉淡化开始时刻
    private volatile bool _sovBuilding;                             // 后台构建中（节流，避免重复排队）
    private int _sovBuildId;                                        // 数据版本号：RebuildHeatGrid 时自增，作废后台构建结果
    private double _renderDpiScale = 1;                             // 当前 DPI 缩放（OnRender 每帧更新，显示宽换算用）
    private System.Windows.Threading.DispatcherTimer? _sovFadeTimer;   // 交叉淡化驱动：缩放停止后 OnRender 不再被触发，需 timer 推进 fade 帧
    private int _sovFadeFrame;                                      // fade 帧计数：fade 期间让 baseCache 缓存 key 变化，强制每帧真画（否则冻结在混合态）

    /// <summary>热力当前是否按主权联盟聚合（开关值；实际绘制以 <see cref="_sovActive"/> 回退结果为准）。</summary>
    public bool HeatBySov => _heatBySov;

    private bool _showSovShading = true;                            // 主权着色模式的疆域晕染层开关（只影响主权模式；热力模式色块由各自的"热力"开关管）

    /// <summary>主权着色模式的疆域晕染层是否显示。</summary>
    public bool SovShadingVisible => _showSovShading;

    /// <summary>
    /// 设置主权着色模式的疆域晕染层显隐（关闭后该模式只画联盟色圆点与主权名标签）。
    /// 只影响主权模式；热力模式的色块显隐走 <see cref="SetHeatMapVisible"/>。变化才重画。
    /// </summary>
    public void SetSovShadingVisible(bool visible)
    {
        if (_showSovShading == visible)
        {
            return;
        }

        _showSovShading = visible;
        _dataVersion++;
        InvalidateVisual();
    }

    /// <summary>切换热力聚合方式：true = 按主权联盟疆域（凸包色块），false = 几何等距格子。变化才重建。</summary>
    public void SetHeatBySovereignty(bool enabled)
    {
        if (_heatBySov == enabled)
        {
            return;
        }

        _heatBySov = enabled;
        RebuildHeatGrid();
        _dataVersion++;
        InvalidateVisual();
    }

    private Dictionary<long, double> _heatCells = [];    // 格子 → 聚合值
    private Dictionary<long, double> _heatRanks = [];    // 格子 → 分位 0..1（全图相对排名）
    private bool _heatRanksBuilt;

    /// <summary>各热力模式的色块显隐（每个着色类型独立一份，缺省 = 显示；开关在左下角图例面板）。</summary>
    private readonly Dictionary<MapColorMode, bool> _showHeatByMode = [];

    /// <summary>开关当前着色模式的热力色块（默认开）。关闭/开启都会让底图重建。</summary>
    public void SetHeatMapVisible(bool visible) => SetHeatMapVisible(_currentColorMode, visible);

    /// <summary>
    /// 设置指定模式的热力色块显隐：页面启动时用它回填持久化的各模式状态；
    /// 目标模式是当前模式才触发重建重画，其余只记值（等切到该模式时 <see cref="SetColorMode"/> 会重建）。
    /// </summary>
    public void SetHeatMapVisible(MapColorMode mode, bool visible)
    {
        if (GetHeatMapVisible(mode) == visible)
        {
            return;
        }

        _showHeatByMode[mode] = visible;
        if (mode == _currentColorMode)
        {
            // 圆点颜色与色块显隐无关（始终按值排名上色），这里只需重建色块层
            RebuildHeatGrid();
            _dataVersion++;
            InvalidateVisual();
        }
    }

    /// <summary>查询指定模式的色块显隐（从未设置过 = 显示）。</summary>
    public bool GetHeatMapVisible(MapColorMode mode) => !_showHeatByMode.TryGetValue(mode, out var visible) || visible;

    /// <summary>这三个模式除单点着色外，再叠一层矩形色块热力图。</summary>
    private bool IsHeatMode => _currentColorMode is MapColorMode.Kills or MapColorMode.Jumps or MapColorMode.PlanetResource;

    /// <summary>
    /// 按"世界坐标网格"重建热力色块：每格累加格内星系数值（值 ≤ 0 的星系不计，空区保持干净）。
    /// **归一化用"全图相对排名（分位）"**——最冷的格子必然红、最热的必然青，11 档均匀用满；
    /// 只在**数据变化**时重建（切换模式 / 重新载入地图），缩放平移不重建 → 颜色稳定。
    /// </summary>
    private void RebuildHeatGrid()
    {
        _heatCells = [];
        _heatRanks = [];
        _sovLayer?.Dispose();
        _sovLayer = null;
        _sovFadeFrom?.Dispose();
        _sovFadeFrom = null;
        _sovBuiltLayer?.Dispose();
        _sovBuiltLayer = null;
        _sovBuildId++;          // 作废还在后台构建的旧数据层
        _sovLastByGroup = null;
        _sovBlobs.Clear();
        _sovNames.Clear();
        _sovActive = false;
        _heatRanksBuilt = false;
        // 主权着色模式：叠加疆域晕染层（与节点联盟圆点同色系背景强化），无格子热力
        var isSovMode = _currentColorMode == MapColorMode.Sovereignty;
        if (!isSovMode && (!IsHeatMode || !GetHeatMapVisible(_currentColorMode)) || _nodes.Length == 0 || _worldW <= 0)
        {
            return;
        }

        var isResource = _currentColorMode == MapColorMode.PlanetResource;

        // 主权聚合：每个联盟（GroupId>0）一块"疆域晕染"，热度 = 联盟内星系数值合计（主权模式下每星系计 1）；
        // 热力模式无主权数据/无匹配 → 回退几何格子；主权模式无数据 → 不画
        if (_heatBySov || isSovMode)
        {
            // 联盟名（主权文字提示，一片一个）；主权强刷后 GroupId 会重排 → 每次重建随 SovService.Current 刷新
            foreach (var info in SovService.Current)
            {
                if (info.GroupId > 0 && !string.IsNullOrEmpty(info.AllianceName))
                {
                    _sovNames[info.GroupId] = info.AllianceName;
                }
            }

            var byGroup = new Dictionary<long, (double Sum, List<SKPoint> Pts)>();
            foreach (var node in _nodes)
            {
                // 主权模式：无热度语义，每星系计 1（透明度分位 = 星系数排名）；热力模式：按当前数值
                var value = isSovMode ? 1.0 : isResource ? node.Resource : node.Heat;
                if (value <= 0 || node.GroupId <= 0)
                {
                    continue;
                }

                if (!byGroup.TryGetValue(node.GroupId, out var entry))
                {
                    entry = (0, new List<SKPoint>());
                    byGroup[node.GroupId] = entry;
                }

                entry.Sum += value;
                entry.Pts.Add(new SKPoint((float)node.NX, (float)node.NY));
                byGroup[node.GroupId] = entry;
            }

            if (byGroup.Count > 0)
            {
                // 聚类用固定虚拟分辨率（1000 基准 = 各向同性世界坐标），dotR 虚拟 20 = 世界长边 2%，连片阈值 2.2 倍
                var virtualH = 1000f * (float)(_worldH / _worldW);
                foreach (var (groupId, entry) in byGroup)
                {
                    foreach (var (center, size) in ClusterSovBlobs(entry.Pts, 1000f, virtualH, 20f))
                    {
                        _sovBlobs.Add((groupId, center, size));
                    }
                }

                _sovLastByGroup = byGroup;      // 缓存聚合数据：矢量直绘与位图共用（剖面/分位完全同源）
                // 位图只在数据变化时后台重烘一次（固定 6000 宽）：渲染期按"显示宽 > 6000 即切矢量"分流，
                // 缩放全程零重烘——重烘竞态（快速缩放时倍率冲过整数档）从机制上消失。
                BeginSovLayerRebuild(6000);
                _sovActive = true;
                _heatRanksBuilt = true;
                return;
            }
        }

        if (isSovMode)
        {
            return;   // 主权模式没有格子热力语义（主权数据缺失时宁可不画，也不落格子）
        }

        var step = _worldW / HeatGridCells;
        foreach (var node in _nodes)
        {
            var value = isResource ? node.Resource : node.Heat;
            if (value <= 0)
            {
                continue;
            }

            // 减去网格原点偏移后再落格；偏移可为负索引（PackCell 算术移位保留符号，安全）
            var nx = node.NX - HeatGridOffsetX;
            var ny = node.NY - HeatGridOffsetY;
            var key = PackCell((int)Math.Floor(nx / step), (int)Math.Floor(ny / step));
            _heatCells.TryGetValue(key, out var sum);
            sum += value;
            _heatCells[key] = sum;
        }

        if (_heatCells.Count == 0)
        {
            return;
        }

        var rankByValue = BuildQuantileMap(_heatCells.Values.OrderBy(p => p).ToList());
        foreach (var (key, sum) in _heatCells)
        {
            _heatRanks[key] = rankByValue[sum];
        }

        _heatRanksBuilt = true;
    }

    /// <summary>把"已排序的值序列"映射成 0..1 的分位（相同值同分位；只有一个值时给 0.5）。</summary>
    private static Dictionary<double, double> BuildQuantileMap(List<double> ordered)
    {
        var map = new Dictionary<double, double>(ordered.Count);
        var i = 0;
        while (i < ordered.Count)
        {
            var value = ordered[i];
            var j = i;
            while (j + 1 < ordered.Count && ordered[j + 1] == value)
            {
                j++;
            }

            map[value] = (i + j) / 2.0 / (ordered.Count - 1);
            i = j + 1;
        }

        return map;
    }

    /// <summary>
    /// 矩形色块热力图（画在连线与节点之下）：颜色与透明度用与安等相同的 11 色 + 分位；
    /// 世界 → 屏幕换算必须与 <see cref="NodePos"/> 同一套因子（NX * _worldW * _zoom + offset）。
    /// </summary>
    private void DrawHeatMap(SKCanvas canvas, SKPaint paint, float w, float h)
    {
        if (!_heatRanksBuilt)   // 关闭或非热力模式时 RebuildHeatGrid 不会置位 → 自然跳过
        {
            return;
        }

        if (_sovActive)
        {
            // 晕染开关关闭（用户设置）：跳过全部晕染绘制，仅保留主权名标签（高倍看单个星座时仍有信息价值）；
            // 聚合数据/位图烘焙照常（成本低、频率低，切回开关无需重建）。
            if (!_showSovShading)
            {
                DrawSovLabels(canvas, w, h);
                return;
            }

            // 晕染渲染终案（09-23 v16）：**位图服务"显示宽 ≤ 6000"域（缩放永不重烘），更深放大走矢量直绘**。
            // 机制依据（用户日志四组突变精确对齐 GPU 采样整数倍率档）：位图放大倍率必须远离整数档——
            // 6000 宽位图的整数 2x 档在显示宽 12000 处，域内最大放大仅 1.875x，安全余量充足；
            // 矢量径向渐变解析连续，与位图 stamp 同剖面同分位 alpha（v15 修正 Skia "Shader 覆盖 paint.Color"
            // 陷阱后两侧真正等价），切换边界无浓度变化。
            // v16 性能（用户"放大后还是很卡"）：深放大时屏内软斑互相重叠上千层（fill rate 爆炸）——
            // ① 位图域 3200→6000（过渡区最重的渲染回到一次 DrawBitmap）；② 矢量域贪心去重（同联盟
            // 间距 ≥ 软斑半径的才画、保留点画 2 层补偿叠加浓度），深度放大从 ~700 斑降到 ~10 斑。
            // v17 性能（用户"缩放到 12516→14769 还是明显卡顿"）：矢量域分位/排序/去重结果按数据版本
            // 缓存（EnsureSovVectorCaches），绘制循环零 GC 分配；DrawRect→DrawCircle 再省 ~21% fill。
            if (_sovBuiltLayer is not null)
            {
                // 后台构建完成 → 交接，旧层开始 300ms 淡出（首层无旧层、直接显示）
                _sovFadeFrom = _sovLayer;
                _sovLayer = _sovBuiltLayer;
                _sovBuiltLayer = null;
                _sovFadeStart = DateTime.UtcNow;
                _sovScreenKey = null;   // 屏幕预缩放缓存烘自旧层，内容已失效（_sovBuildId 在 RebuildHeatGrid 开头就自增，靠它作废不覆盖此处）
                StartSovFadeTimer();
            }

            var vectorMode = _sovLayer is null || _worldW * _zoom * _renderDpiScale > 6000.0;
            if (vectorMode)
            {
                // 放大域 / 首层未就绪：矢量直绘（无位图参与 → 无采样档位、无重烘竞态、零后台成本）
                if (_sovFadeFrom is not null)
                {
                    _sovFadeFrom.Dispose();
                    _sovFadeFrom = null;
                    _sovFadeTimer?.Stop();
                }

                DrawSovVectorSpots(canvas);
            }
            else
            {
                // 缩小域：位图铺世界（6000 宽位图此时被缩小/最大 1.875x 放大显示，双线性连续、无整数档可跨）
                // v20 拖动优化：纯拖动（zoom/dpi/数据版本不变）时晕染屏幕投影只平移不变 → 把 6000 宽源按当前
                // zoom 预缩放成屏幕分辨率缓存，拖动帧退化为 1:1 贴图。全图视野下每帧全屏双线性重采样（6000 宽
                // 源→屏宽）是拖动卡顿主因（帧时间近翻倍）。
                // 内存护栏：缓存 = 投影的设备像素尺寸，深位图域投影可达 6000 宽（~80MB）不可接受；
                // 投影超 ~12M 设备像素（贴合视图远达不到，中深度放大才触达）退回逐帧重采样。
                var projW = _worldW * _zoom * _renderDpiScale;
                var projH = _worldH * _zoom * _renderDpiScale;
                var cacheable = _sovLayer is not null && projW * projH <= 12_000_000;
                var screenKey = (_zoom, _renderDpiScale, _sovBuildId);
                // 视图稳定检测：上一帧 key 与本帧相同 → zoom 已停变（拖动中/静止），缓存可建可命中；
                // key 连续变化（滚轮缩放/飞行动画中）→ 不重建（用旧缓存或退回重采样），等稳定一帧后再建
                var viewStable = _sovScreenPrevKey == screenKey;
                _sovScreenPrevKey = screenKey;
                var hasScreenCache = cacheable
                    && viewStable
                    && _sovScreenCache is not null
                    && _sovScreenKey == screenKey
                    && _sovScreenCache.Width == Math.Max(1, (int)Math.Ceiling(projW))
                    && _sovScreenCache.Height == Math.Max(1, (int)Math.Ceiling(projH));
                if (cacheable && viewStable && !hasScreenCache)
                {
                    var cw = Math.Max(1, (int)Math.Ceiling(projW));
                    var ch = Math.Max(1, (int)Math.Ceiling(projH));
                    _sovScreenCache?.Dispose();
                    _sovScreenCache = new SKBitmap(cw, ch);
                    using (var sc = new SKCanvas(_sovScreenCache))
                    {
                        sc.Clear(SKColors.Transparent);
                        var src = new SKRect(0, 0, _sovLayer!.Width, _sovLayer.Height);
                        var dst = new SKRect(0, 0, cw, ch);
                        #pragma warning disable CS0618
                        sc.DrawBitmap(_sovLayer!, src, dst, new SKPaint { FilterQuality = SKFilterQuality.Low });
                        #pragma warning restore CS0618
                    }

                    _sovScreenKey = screenKey;
                }

                // 画布当前已带 ×dpi 变换：dest 用 DIP 坐标，经变换后恰为缓存的设备像素尺寸 → 1:1 贴图
                var dest = SKRect.Create((float)_offsetX, (float)_offsetY, (float)(_worldW * _zoom), (float)(_worldH * _zoom));
                #pragma warning disable CS0618
                if (_sovFadeFrom is not null)
                {
                    // fade 期间新旧两层内容不同：旧层走逐帧重采样（fade 仅 300ms，可接受），新层尽量走缓存
                    var t = (float)Math.Clamp((DateTime.UtcNow - _sovFadeStart).TotalMilliseconds / 300.0, 0, 1);
                    paint.FilterQuality = SKFilterQuality.Low;
                    paint.Color = new SKColor(0, 0, 0, (byte)(255 * (1 - t) * SovAlphaDamp));
                    canvas.DrawBitmap(_sovFadeFrom, dest, paint);
                    paint.Color = new SKColor(0, 0, 0, (byte)(255 * t * SovAlphaDamp));
                    if (hasScreenCache)
                    {
                        paint.FilterQuality = SKFilterQuality.None;
                        canvas.DrawBitmap(_sovScreenCache!, dest, paint);
                    }
                    else
                    {
                        canvas.DrawBitmap(_sovLayer!, dest, paint);
                    }

                    paint.Color = SKColors.Black;
                    paint.FilterQuality = SKFilterQuality.None;
                    if (t >= 1)
                    {
                        _sovFadeFrom.Dispose();
                        _sovFadeFrom = null;
                        _sovFadeTimer?.Stop();
                    }
                }
                else if (hasScreenCache)
                {
                    // 拖动常规路径：1:1 贴图（无重采样）
                    paint.FilterQuality = SKFilterQuality.None;
                    paint.Color = new SKColor(0, 0, 0, (byte)(255 * SovAlphaDamp));   // 全局阻尼：与矢量域同步调浅
                    canvas.DrawBitmap(_sovScreenCache!, dest, paint);
                    paint.Color = SKColors.Black;
                }
                else
                {
                    // 超预算回退：逐帧重采样（旧行为）
                    paint.FilterQuality = SKFilterQuality.Low;
                    paint.Color = new SKColor(0, 0, 0, (byte)(255 * SovAlphaDamp));   // 全局阻尼：与矢量域同步调浅
                    canvas.DrawBitmap(_sovLayer!, dest, paint);
                    paint.Color = SKColors.Black;
                    paint.FilterQuality = SKFilterQuality.None;
                }
                #pragma warning restore CS0618
            }

            // 标签独立于晕染显隐：高倍看单个星座时主权名仍有信息价值
            DrawSovLabels(canvas, w, h);
            return;
        }

        // 注意：NX/NY 是"归一化世界坐标"，屏幕换算要和 NodePos 一致——必须带上 _worldW / _worldH 因子
        var stepX = _worldW / HeatGridCells;
        var stepY = stepX;
        var cellW = (float)(stepX * _worldW * _zoom);
        var cellH = (float)(stepY * _worldH * _zoom);
        if (cellW <= 1f || cellH <= 1f)
        {
            return;   // 缩太小：色块比节点还小，没有观察价值
        }

        var cullW = w + _bakePad * 2;    // 烘焙帧含 pad 环：环内色块也要烘（拖动帧会裁到）
        var cullH = h + _bakePad * 2;
        var cullOx = -_bakePad;
        var cullOy = -_bakePad;
        paint.Style = SKPaintStyle.Fill;
        foreach (var key in _heatRanks.Keys)
        {
            var t = _heatRanks[key];
            var cx = (int)(key >> 32);
            var cy = (int)(uint)key;
            var left = (float)((cx * stepX + HeatGridOffsetX) * _worldW * _zoom + _offsetX);
            var top = (float)((cy * stepY + HeatGridOffsetY) * _worldH * _zoom + _offsetY);
            if (left + cellW < cullOx || top + cellH < cullOy || left > cullOx + cullW || top > cullOy + cullH)
            {
                continue;
            }

            var color = SecurityColor(t);
            paint.Color = new SKColor(color.Red, color.Green, color.Blue, (byte)Math.Clamp(40 + (t * 150), 40, 185));
            canvas.DrawRoundRect(new SKRect(left + 0.5f, top + 0.5f, left + cellW - 0.5f, top + cellH - 0.5f), 3f, 3f, paint);
        }
    }

    /// <summary>主权层归一化坐标 → 屏幕（与 NodePos 同一换算）。</summary>
    private SKPoint HeatSovToScreen(SKPoint p) =>
        new((float)(p.X * _worldW * _zoom + _offsetX), (float)(p.Y * _worldH * _zoom + _offsetY));

    /// <summary>
    /// 交叉淡化期间每帧驱动重绘：缩放停止后 OnRender 不会再被触发，fade 需要 300ms 内连续多帧才能推进
    /// （此前 fade 冻结在半途的根因）。t≥1 后绘制侧自动 Stop。
    /// </summary>
    private void StartSovFadeTimer()
    {
        if (_sovFadeTimer is null)
        {
            _sovFadeTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _sovFadeTimer.Tick += (_, _) =>
            {
                if (_sovFadeFrom is null)
                {
                    _sovFadeTimer.Stop();
                    return;
                }

                _sovFadeFrame++;   // 推进缓存 key：fade 每帧真画（见底图缓存 key 注释）
                InvalidateVisual();
            };
        }

        _sovFadeTimer.Start();
    }

    /// <summary>
    /// 后台线程烘 6000 宽软斑晕染位图；只在数据变化（RebuildHeatGrid）时调用一次，渲染期缩放零重烘。
    /// 完成后通过 _sovBuiltLayer 交接，渲染线程下帧开始 300ms 交叉淡化。
    /// 携带发起时的 _sovBuildId：数据重建后（RebuildHeatGrid 自增）结果作废丢弃。
    /// </summary>
    private void BeginSovLayerRebuild(int targetW)
    {
        if (_sovBuilding || _sovLastByGroup is null)
        {
            return;
        }

        _sovBuilding = true;
        var byGroup = _sovLastByGroup;
        var buildId = _sovBuildId;
        var wPx = targetW;
        var hPx = Math.Max(16, (int)Math.Round((double)wPx * _worldH / _worldW));
        Task.Run(() =>
        {
            var bmp = RenderSovBitmap(byGroup, wPx, hPx);
            if (buildId == _sovBuildId)
            {
                _sovBuiltLayer = bmp;   // 交接给渲染线程（数据未失效）
            }
            else
            {
                bmp.Dispose();          // 构建期间数据已重建，结果作废
            }

            _sovBuilding = false;
        });
    }

    private readonly Dictionary<long, SKBitmap> _sovStampByGroup = [];   // 联盟 → 彩色软斑（剖面 alpha + 联盟色，缓存复用；groupId→色恒定故不清理）

    /// <summary>
    /// 联盟彩色软斑位图（128px 径向渐变：中心不透明 → 0.4 处 55% → 边缘透明，剖面近似高斯）。
    /// 关键：这是<b>解析定义</b>的剖面，缩放贴到任何分辨率下采样到的都是同一个连续函数——
    /// 跨分辨率重建的位图内容因此一致，重建前后不再有系统性浓度差（blur 是离散卷积，分辨率一变结果就变）。
    /// 注意 SkiaSharp 的 DrawBitmap 只用 paint 的 <b>alpha</b> 调制位图（RGB 被忽略），所以必须按联盟色烘焙彩色 stamp。
    /// </summary>
    private SKBitmap GetSovStamp(long groupId)
    {
        if (_sovStampByGroup.TryGetValue(groupId, out var stamp))
        {
            return stamp;
        }

        const int sz = 128;
        stamp = new SKBitmap(sz, sz);
        using var c = new SKCanvas(stamp);
        var color = SovGroupColor(groupId, 0.5);
        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(sz / 2f, sz / 2f),
            sz / 2f,
            new[] { new SKColor(color.Red, color.Green, color.Blue, 255), new SKColor(color.Red, color.Green, color.Blue, 140), new SKColor(color.Red, color.Green, color.Blue, 0) },
            new[] { 0f, 0.4f, 1f },
            SKShaderTileMode.Clamp);
        using var p = new SKPaint { Shader = shader };
        c.DrawRect(0, 0, sz, sz, p);
        _sovStampByGroup[groupId] = stamp;
        return stamp;
    }

    /// <summary>
    /// 深放大域晕染矢量直绘（v19 改为 1/8 分辨率离屏烘焙 + 一次双线性拉伸铺屏）：
    /// 贪心去重的间距阈值 = 软斑**半径**（0.048 世界单位），相邻保留斑圆心距 = R、半径也是 R
    /// → 每联盟软斑覆盖 ≈ π×屏幕面积 × 屏内联盟数（20~40）= 每帧十几~几十倍屏幕面积的
    /// 抗锯齿径向渐变填充。SKElement 是 CPU 光栅——这才是深放大卡顿的量级来源（与 zoom 无关恒定存在，
    /// 所以 v17 消 GC、v18 消徽标裁剪都无效）。晕染是大半径低频浓度场，1/8 分辨率烘焙 + 双线性放大视觉无损：
    /// 渐变 fill ÷64，拉伸倍率恒定 8x（不随缩放变化 → v13 的"整数采样档"机制不适用），
    /// 内容逐帧按当前 zoom 重烘、over 算子逐像素独立 → 合成结果与直绘等价（仅高频模糊，斑点剖面本就是低频）。
    /// 分位/排序/去重骨架仍按数据版本缓存（<see cref="EnsureSovVectorCaches"/>），
    /// 离屏缓冲/画布/blit 画笔跨帧复用——稳态零 GC。
    /// </summary>
    private void DrawSovVectorSpots(SKCanvas canvas)
    {
        EnsureSovVectorCaches();
        if (_sovVectorCaches.Count == 0)
        {
            return;
        }

        var w = ActualWidth + _bakePad * 2;    // 烘焙帧含 pad 环：环内软斑也要烘（拖动帧会裁到）
        var h = ActualHeight + _bakePad * 2;
        var cullOx = -_bakePad;               // 烘焙帧的坐标原点平移：软斑中心在 (-pad..w+pad) 范围内参与绘制
        var cullOy = -_bakePad;
        var spotScreenR = (float)(0.048 * _worldW * _zoom);   // 软斑半径：世界长边 2%（dotR）×2.4（spotR/dotR），与位图侧同源
        if (spotScreenR < 2f)
        {
            return;
        }

        var dpi = (float)_renderDpiScale;
        var bw = Math.Max(1, (int)Math.Ceiling(w * dpi * SovVectorOffscreenScale));
        var bh = Math.Max(1, (int)Math.Ceiling(h * dpi * SovVectorOffscreenScale));
        if (_sovVectorOffscreen is null || _sovVectorOffscreen.Width != bw || _sovVectorOffscreen.Height != bh)
        {
            _sovVectorOffscreenCanvas?.Dispose();
            _sovVectorOffscreen?.Dispose();
            _sovVectorOffscreen = new SKBitmap(bw, bh);
            _sovVectorOffscreenCanvas = new SKCanvas(_sovVectorOffscreen);
        }

        var off = _sovVectorOffscreenCanvas!;
        off.SetMatrix(SKMatrix.Identity);   // 复用画布：重置上帧矩阵（循环内 Save/Restore 自平衡，剪裁不残留）
        off.Clear(SKColors.Transparent);
        off.Scale(dpi * SovVectorOffscreenScale, dpi * SovVectorOffscreenScale);

        var drawn = 0;
        foreach (var (groupId, cache) in _sovVectorCaches)
        {
            var kept = cache.Kept;
            for (var i = 0; i < kept.Count; i++)
            {
                var (p, k) = kept[i];
                var cx = (float)(p.X * _worldW * _zoom + _offsetX);
                var cy = (float)(p.Y * _worldH * _zoom + _offsetY);
                if (cx < cullOx - spotScreenR || cy < cullOy - spotScreenR
                    || cx > cullOx + w + spotScreenR || cy > cullOy + h + spotScreenR)
                {
                    continue;   // 软斑整体出（烘焙）视口
                }

                // 单位渐变（半径 1，中心原点）+ 画布变换平移缩放：缓存 paint 与屏幕位置解耦
                var paint = GetSovVectorPaint(groupId, cache.T, cache.Color, cache.PaintAlpha, k);
                off.Save();
                off.Translate(cx - cullOx, cy - cullOy);   // 烘焙帧：软斑中心平移进离屏缓冲坐标系
                off.Scale(spotScreenR, spotScreenR);
                off.DrawCircle(0, 0, 1, paint);
                off.Restore();
                drawn++;
            }
        }

        TrimSovVectorPaints();

        // 一次双线性拉伸铺屏（主画布已含 DPI 缩放 → 目标矩形用逻辑坐标；Low = 双线性，缺省 None 会马赛克）
        // 全局 alpha 阻尼（SovAlphaDamp）：DrawImage/DrawBitmap 只吃 paint alpha → 一处乘法调浅整个矢量域
        #pragma warning disable CS0618
        if (_sovBlitPaint is null)
        {
            _sovBlitPaint = new SKPaint { FilterQuality = SKFilterQuality.Low };
        }

        _sovBlitPaint.Color = new SKColor(0, 0, 0, (byte)(255 * SovAlphaDamp));
        #pragma warning restore CS0618
        // 缓冲坐标 = 屏幕坐标 + pad（软斑画在 cx+pad）。烘焙帧画布已 Translate(pad,pad)：
        // dest 原点必须是 −pad，缓冲像素 b 才落在画布点 b−pad = 屏幕坐标 s（再经 Translate 变成位图 (s+pad)×scale，与世界层对齐）。
        // 写成 (0,0,w,h) 会双重 +pad → 整层晕染向右下漂移 2×pad 且左/上环空缺（实机截图踩过）。
        var destL = -_bakePad;
        var destT = -_bakePad;
        canvas.DrawBitmap(_sovVectorOffscreen!, new SKRect(destL, destT, destL + (float)w, destT + (float)h), _sovBlitPaint);

#if DEBUG
        if (++_sovVecLogFrame % 90 == 1)
        {
            Debug.WriteLine($"[SOV-VEC] off={bw}x{bh} spots={drawn} spotR={spotScreenR:F0} zoom={_zoom:F0}");
        }
#endif
    }

    /// <summary>矢量晕染离屏烘焙分辨率（相对物理像素；1/8 → 渐变 fill 降 64 倍，双线性放大视觉无损）。</summary>
    private const float SovVectorOffscreenScale = 1f / 8f;

    /// <summary>晕染全局 alpha 阻尼（09-24 用户"颜色深了点，可以调浅点"）：两个域的叠加浓度等比调浅。
    /// **必须同时作用于位图域与矢量域**——单侧调浅会让显示宽 6000 的切换边界重新出现浓度台阶。
    /// 觉得还深就调小（0.80），太浅调大（0.90~1.0）。</summary>
    private const float SovAlphaDamp = 0.5f;

    private SKBitmap? _sovVectorOffscreen;              // 矢量晕染离屏缓冲（尺寸随窗口变化才重分配）
    private SKCanvas? _sovVectorOffscreenCanvas;        // 离屏画布（跨帧复用）
    private SKPaint? _sovBlitPaint;                     // 铺屏画笔（双线性，跨帧复用）
    private SKBitmap? _sovScreenCache;                  // 位图域晕染预缩放到屏幕分辨率的缓存（v20 拖动优化：拖动帧 1:1 贴图，
                                                        // 省掉每帧 6000 宽源的全屏双线性重采样——全图视野拖动卡顿的根因）
    private (double Zoom, double Dpi, int BuildId)? _sovScreenKey;   // 预缩放缓存的生效条件（zoom/dpi/数据版本任一变化即失效）
    private (double Zoom, double Dpi, int BuildId)? _sovScreenPrevKey;   // 上一帧的视图 key：连续两帧相同（视图稳定）才构建缓存，
                                                                        // 滚轮缩放期间每帧 key 都变 → 退回直接重采样（避免每帧重建反而更糟）
#if DEBUG
    private int _sovVecLogFrame;                        // [SOV-VEC] 诊断日志节流
#endif

    private sealed class SovGroupVectorCache
    {
        public double T;                                       // 联盟热度全图分位（0~1）
        public byte PaintAlpha;                                // 画笔基础 alpha（55 + t*130）
        public SKColor Color;                                  // 联盟色
        public List<(SKPoint P, int K)> Kept = [];             // 贪心去重骨架点 + 吸收星系数（世界域常量，跨帧复用）
    }

    private readonly Dictionary<long, SovGroupVectorCache> _sovVectorCaches = [];      // 联盟 → 预计算缓存（数据版本内常量）
    private int _sovVectorCacheVersion = -1;                     // 缓存对应的数据版本（_sovBuildId）

    /// <summary>
    /// 矢量域预计算缓存（v17）：分位表 / 排序 / 贪心去重结果全部是**世界域常量**（与 zoom/offset 无关），
    /// 却在每帧重算（OrderBy/ToList/Zip/分位字典）→ 深放大滚动帧率被 GC 拖垮。
    /// 按 _sovBuildId 版本只构建一次；版本变化时先释放画笔缓存（联盟色/分位/吸收数可能全部重排）。
    /// </summary>
    private void EnsureSovVectorCaches()
    {
        if (_sovVectorCacheVersion == _sovBuildId && _sovVectorCaches.Count > 0)
        {
            return;
        }

        foreach (var grp in _sovVectorPaints.Values)
        {
            foreach (var pnt in grp.Values)
            {
                pnt.Dispose();
            }

            grp.Clear();
        }

        _sovVectorCaches.Clear();
        _sovVectorCacheVersion = _sovBuildId;
        if (_sovLastByGroup is null)
        {
            return;
        }

        var rankByValue = BuildQuantileMap(_sovLastByGroup.Values.Select(e => e.Sum).OrderBy(p => p).ToList());
        const float minDist = 0.048f;                        // 软斑世界半径（NX/NY 归一化坐标，各向同性基准）
        var minDistSq = minDist * minDist;
        foreach (var (groupId, entry) in _sovLastByGroup)
        {
            if (entry.Pts.Count == 0)
            {
                continue;
            }

            var t = rankByValue.TryGetValue(entry.Sum, out var tv) ? tv : 0;
            var cache = new SovGroupVectorCache
            {
                T = t,
                Color = SovGroupColor(groupId, 0.5),
                PaintAlpha = (byte)Math.Clamp(55 + (t * 130), 55, 185),
            };
            var pts = entry.Pts;
            if (pts.Count == 1)
            {
                cache.Kept.Add((pts[0], 1));
            }
            else
            {
                var sorted = pts.OrderBy(p => p.X).ToList();   // 数据版本内仅一次
                cache.Kept.Add((sorted[0], 1));
                for (var i = 1; i < sorted.Count; i++)
                {
                    var p = sorted[i];
                    var dx = p.X - cache.Kept[^1].P.X;
                    var dy = p.Y - cache.Kept[^1].P.Y;
                    if (dx * dx + dy * dy >= minDistSq)
                    {
                        cache.Kept.Add((p, 1));
                    }
                    else
                    {
                        var last = cache.Kept[^1];
                        cache.Kept[^1] = (last.P, last.K + 1);   // 被前一保留点吸收
                    }
                }
            }

            _sovVectorCaches[groupId] = cache;
        }
    }

    private readonly Dictionary<long, Dictionary<(double Rank, int K), SKPaint>> _sovVectorPaints = [];   // (联盟, 分位, 吸收数) → 画笔（跨帧缓存）

    /// <summary>画笔缓存随预计算缓存一起失效（EnsureSovVectorCaches 版本变化时释放），此处仅保留结构性判活。</summary>

    /// <summary>
    /// 取（或建）指定联盟+分位+k 层吸收的单位径向渐变画笔：坐标单位化（中心原点、半径 1），与画布变换配合。
    /// alpha = k 层叠加浓度 1-(1-a)^k（a = 分位 alpha/255）预烘进渐变顶点色——与位图侧 k 层逐点叠加的合成结果对齐
    /// （Skia 规则：带 Shader 的 paint，Color 被完全覆盖，所以 alpha 必须进顶点色）。
    /// k 只取 [1..6]+饱和 7 档（≥7 档间浓度差 &lt;1/255 已不可分辨）——缓存组合数有界。
    /// </summary>
    private SKPaint GetSovVectorPaint(long groupId, double t, SKColor color, byte alpha, int k)
    {
        var kIdx = Math.Min(k, 7);
        if (!_sovVectorPaints.TryGetValue(groupId, out var byRank))
        {
            byRank = [];
            _sovVectorPaints[groupId] = byRank;
        }

        if (byRank.TryGetValue((t, kIdx), out var cached))
        {
            return cached;
        }

        // k 层叠加浓度（源叠加非线性合成）：中心 a1 = 1-(1-a)^k，0.4 位处剖面值同步合成
        var a = alpha / 255.0;
        var centerA = (int)Math.Round(255 * (1 - Math.Pow(1 - a, kIdx)));
        var midA = (int)Math.Round(255 * (1 - Math.Pow(1 - (a * 140 / 255.0), kIdx)));
        var shader = SKShader.CreateRadialGradient(
            new SKPoint(0, 0),
            1f,
            new[] { new SKColor(color.Red, color.Green, color.Blue, (byte)centerA), new SKColor(color.Red, color.Green, color.Blue, (byte)midA), new SKColor(color.Red, color.Green, color.Blue, 0) },
            new[] { 0f, 0.4f, 1f },
            SKShaderTileMode.Clamp);
        var paint = new SKPaint { IsAntialias = true, Shader = shader };
        byRank[(t, kIdx)] = paint;
        return paint;
    }

    /// <summary>缓存上限保护：联盟×分位组合数超过 256 时全量清空（正常 ≤ 40 联盟 × ~几档，不会触达）。</summary>
    private void TrimSovVectorPaints()
    {
        var count = 0;
        foreach (var grp in _sovVectorPaints.Values)
        {
            count += grp.Count;
        }

        if (count > 256)
        {
            foreach (var grp in _sovVectorPaints.Values)
            {
                foreach (var pnt in grp.Values)
                {
                    pnt.Dispose();
                }

                grp.Clear();
            }
        }
    }

    /// <summary>
    /// 烘主权疆域晕染位图（软斑盖章法）：每个有热度星系按联盟色把单位软斑缩放贴上，相邻星系自然连成疆域、空隙保留；
    /// 透明度 = 联盟热度全图分位（与格子同公式）；联盟重叠区后画覆盖前画，不脏混。
    /// 比 blur 快一个量级，且跨分辨率内容一致（重建不再带来观感跳变）。纯 CPU 位图操作，可后台调用。
    /// </summary>
    private SKBitmap RenderSovBitmap(Dictionary<long, (double Sum, List<SKPoint> Pts)> byGroup, int wPx, int hPx)
    {
        var bmp = new SKBitmap(wPx, hPx);
        using var layerCanvas = new SKCanvas(bmp);
        layerCanvas.Clear(SKColors.Transparent);

        var dotR = 0.02f * wPx;              // 星系核半径 = 世界长边 2%：相邻星系连片、大空隙保留
        var spotR = dotR * 2.4f;             // 软斑可视半径（剖面渐变到 0），略大于核半径保证相邻连片
        using var dot = new SKPaint();
        var rankByValue = BuildQuantileMap(byGroup.Values.Select(e => e.Sum).OrderBy(p => p).ToList());
        foreach (var (groupId, entry) in byGroup)
        {
            var t = rankByValue[entry.Sum];
            var stamp = GetSovStamp(groupId);
            // DrawBitmap 只用 paint 的 alpha 调制位图（RGB 由 stamp 自带）
            dot.Color = new SKColor(255, 255, 255, (byte)Math.Clamp(55 + (t * 130), 55, 185));
            foreach (var p in entry.Pts)
            {
                var cx = p.X * wPx;
                var cy = p.Y * hPx;
                layerCanvas.DrawBitmap(stamp, new SKRect(cx - spotR, cy - spotR, cx + spotR, cy + spotR), dot);
            }
        }

        return bmp;
    }

    /// <summary>
    /// 联盟疆域连通分量聚类：像素距离 &lt; 圆斑连片阈值（dotR×2.2）的星系算同一片——相邻星系晕染自然融合，
    /// 飞地/跨区长跳与主体断开各成一片。网格哈希邻桶 BFS，约 O(n)。
    /// 返回每片的包围盒中心与宽高（归一化世界坐标）。
    /// </summary>
    private static List<(SKPoint Center, SKPoint Size)> ClusterSovBlobs(List<SKPoint> pts, float wPx, float hPx, float dotR)
    {
        var n = pts.Count;
        var result = new List<(SKPoint Center, SKPoint Size)>();
        if (n == 0)
        {
            return result;
        }

        var link = dotR * 2.2f;   // 判连片的像素距离：两个圆斑视觉上明显相触
        var linkSq = link * link;
        var px = new float[n];
        var py = new float[n];
        var buckets = new Dictionary<long, List<int>>(n);
        for (var i = 0; i < n; i++)
        {
            px[i] = pts[i].X * wPx;
            py[i] = pts[i].Y * hPx;
            var key = ((long)(px[i] / link) << 32) ^ (uint)(py[i] / link);
            if (!buckets.TryGetValue(key, out var list))
            {
                list = [];
                buckets[key] = list;
            }
            list.Add(i);
        }

        var visited = new bool[n];
        var queue = new Queue<int>();
        for (var seed = 0; seed < n; seed++)
        {
            if (visited[seed])
            {
                continue;
            }

            visited[seed] = true;
            queue.Enqueue(seed);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            while (queue.Count > 0)
            {
                var i = queue.Dequeue();
                minX = Math.Min(minX, px[i]); minY = Math.Min(minY, py[i]);
                maxX = Math.Max(maxX, px[i]); maxY = Math.Max(maxY, py[i]);
                var bx = (int)(px[i] / link);
                var by = (int)(py[i] / link);
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        if (!buckets.TryGetValue(((long)(bx + dx) << 32) ^ (uint)(by + dy), out var list))
                        {
                            continue;
                        }
                        foreach (var j in list)
                        {
                            if (visited[j])
                            {
                                continue;
                            }
                            var ddx = px[j] - px[i];
                            var ddy = py[j] - py[i];
                            if (ddx * ddx + ddy * ddy <= linkSq)
                            {
                                visited[j] = true;
                                queue.Enqueue(j);
                            }
                        }
                    }
                }
            }

            result.Add((new SKPoint((minX + maxX) / 2 / wPx, (minY + maxY) / 2 / hPx),
                        new SKPoint((maxX - minX) / wPx, (maxY - minY) / hPx)));
        }
        return result;
    }

    /// <summary>
    /// 主权文字提示：每片联盟疆域中心画一个联盟名（飞地各画各的）。字号随缩放（整图适配≈9px，封顶 20px），
    /// 且随片大小自适应——目标文字宽≈片屏宽 1.6 倍（允许横跨疆域边缘），压到 5px 以下的极小碎片跳过；
    /// 片按包围盒面积降序"大片优先占位"，与已画文字矩形重叠的让位跳过（防低缩放叠成一锅粥）。
    /// 纯色字（无描边）：浅色主题 = 深联盟色，深色主题 = 亮联盟色（与圆点同色相）。
    /// </summary>
    private void DrawSovLabels(SKCanvas canvas, float w, float h)
    {
        if (_sovBlobs.Count == 0)
        {
            return;
        }

        var zmult = _fitZoom > 0 ? _zoom / _fitZoom : 1;
        var fontPx = (float)Math.Clamp(12 * zmult, 6, 20);
        using var font = new SKFont(_typefaceBold, fontPx);
        using var fill = new SKPaint { IsAntialias = true };

        // 大片优先：每片独立参与排序与占位（同一联盟的多片各是各的候选）
        var entries = new List<(long GroupId, SKPoint Center, float Area, float ScreenW)>(_sovBlobs.Count);
        foreach (var blob in _sovBlobs)
        {
            entries.Add((blob.GroupId, blob.Center, blob.Size.X * blob.Size.Y, blob.Size.X * (float)(_worldW * _zoom)));
        }
        entries.Sort((a, b) => b.Area.CompareTo(a.Area));

        var placed = new List<SKRect>(entries.Count);
        foreach (var entry in entries)
        {
            if (!_sovNames.TryGetValue(entry.GroupId, out var name) || string.IsNullOrEmpty(name))
            {
                continue;
            }

            var p = HeatSovToScreen(entry.Center);
            var baseWidth = font.MeasureText(name);
            // 字号自适应：目标文字宽度≈片屏宽 1.6 倍（允许横跨疆域边缘，参考官方星图）；反解后超过全局字号则封顶，
            // 压到 5px 以下的极小碎片跳过。重叠让位兜底密度。
            var fitPx = baseWidth > 0 ? entry.ScreenW * 1.6f * fontPx / baseWidth : 0;
            var px = Math.Clamp(fitPx, 0, fontPx);
            if (px < 5f)
            {
                continue;
            }
            font.Size = px;
            var textWidth = font.MeasureText(name);
            if (p.X + textWidth / 2 < 0 || p.X - textWidth / 2 > w || p.Y < -20 || p.Y > h + 20)
            {
                continue;   // 整体出屏
            }

            var rect = SKRect.Create(p.X - textWidth / 2 - 1, p.Y - px * 0.55f, textWidth + 2, px * 1.1f);
            var overlapped = false;
            foreach (var r in placed)
            {
                if (r.IntersectsWithInclusive(rect))
                {
                    overlapped = true;
                    break;
                }
            }
            if (overlapped)
            {
                continue;   // 与已画文字重叠，让位给更大的片
            }
            placed.Add(rect);

            // 文字色：同联盟色相；浅色底用深字（V 低）、深色底用亮字（与圆点同亮度）
            var hue = (float)((entry.GroupId * 137.508) % 360);
            fill.Color = _light ? FromHsv(hue, 0.75f, 0.38f) : FromHsv(hue, 0.62f, 0.95f);
            canvas.DrawText(name, p.X - textWidth / 2, p.Y + px * 0.36f, font, fill);
        }
    }

    private float NodeRadius(double zmult) => (float)Math.Clamp(1.5 * Math.Pow(zmult, 0.42), 1.2, 26);

    /// <summary>
    /// 节点外发光的透明度：**整图适配（zmult≈1）时几乎不发光**，放大后才渐显。
    /// 不这样收敛，8k 个节点的光晕在低缩放会互相叠加糊成一片（实测反馈"缩小状态下一片模糊"）。
    /// 1.0 → 0；1.6 → 33；2.0 → 55；3.0 → 110；5.0+ → 220（封顶）。
    /// </summary>
    private float GlowAlpha(double zmult) => (float)Math.Clamp((zmult - 1.0) * 55, 0, 220) * (_light ? 0.45f : 1f);

    /// <summary>外发光半径相对节点半径的倍数：低缩放收敛到 1.15（贴着节点），高缩放展开到 3.4。</summary>
    private static float GlowScale(double zmult) => (float)Math.Clamp(1.15 + (zmult - 1.0) * 0.45, 1.15, 3.4);

    private SKPoint NodePos(MapSystemNode node) =>
        new((float)(node.NX * _worldW * _zoom + _offsetX), (float)(node.NY * _worldH * _zoom + _offsetY));

    // ---------- 背景与星尘 ----------

    private void GenerateStars()
    {
        var rnd = new Random(42);
        SKColor[] palette = [new(255, 255, 255), new(159, 200, 255), new(255, 217, 160), new(200, 180, 255)];
        for (var i = 0; i < StarCountFar; i++)
        {
            _starsFar.Add((rnd.NextDouble(), rnd.NextDouble(), 0.4f + (float)rnd.NextDouble() * 0.8f, (byte)(28 + rnd.Next(60)), palette[rnd.Next(palette.Length)]));
        }
        for (var i = 0; i < StarCountNear; i++)
        {
            _starsNear.Add((rnd.NextDouble(), rnd.NextDouble(), 0.7f + (float)rnd.NextDouble() * 1.1f, (byte)(45 + rnd.Next(90)), palette[rnd.Next(palette.Length)]));
        }
    }

    private void DrawBackground(SKCanvas canvas, SKPaint paint, float w, float h, double zmult)
    {
        // 深空底色渐变
        using var bg = SKShader.CreateRadialGradient(
            new SKPoint(w * 0.5f, h * 0.42f), Math.Max(w, h) * 0.85f,
            [BgCenter, BgMid, BgEdge],
            [0f, 0.55f, 1f], SKShaderTileMode.Clamp);
        paint.Shader = bg;
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRect(0, 0, w, h, paint);
        paint.Shader = null;

        // 视差星尘（两层，随平移缩放做深度视差）
        DrawStarLayer(canvas, paint, _starsFar, 0.22, zmult);
        DrawStarLayer(canvas, paint, _starsNear, 0.5, zmult);
    }

    private void DrawStarLayer(SKCanvas canvas, SKPaint paint, List<(double x, double y, float r, byte a, SKColor c)> stars, double parallax, double zmult)
    {
        var pz = Math.Pow(zmult, parallax * 0.5);
        var baseW = ActualWidth > 10 ? ActualWidth : 1000;
        var baseH = ActualHeight > 10 ? ActualHeight : 800;
        var spread = Math.Max(baseW, baseH) * pz * 1.4;
        var ox = _offsetX * parallax;
        var oy = _offsetY * parallax;
        paint.Style = SKPaintStyle.Fill;
        foreach (var (x, y, r, a, c) in stars)
        {
            var px = (float)(x * spread + ox);
            var py = (float)(y * spread + oy);
            if (px < -2 || py < -2 || px > ActualWidth + 2 || py > ActualHeight + 2)
            {
                continue;
            }

            paint.Color = StarColor(c, a);
            canvas.DrawCircle(px, py, r, paint);
        }
    }

    // ---------- 连线与节点 ----------

    private void DrawLinks(SKCanvas canvas, SKPaint paint, double zmult)
    {
        // 连线透明度随缩放淡入
        var alpha = LinkAlpha(zmult);
        if (alpha <= 1)
        {
            return;
        }

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = Math.Max(0.5f, (float)(0.75 * Math.Pow(zmult, 0.25)));
        var w = ActualWidth + _bakePad;   // 烘焙帧含 pad 环：环内连线也要烘（拖动帧会裁到）；左界见 cullOx
        var h = ActualHeight + _bakePad;
        var cullOx = -_bakePad;
        var cullOy = -_bakePad;
        foreach (var (a, b) in _links)
        {
            var na = _nodes[a];
            var nb = _nodes[b];
            var pa = NodePos(na);
            var pb = NodePos(nb);
            if (Math.Max(pa.X, pb.X) < cullOx || Math.Min(pa.X, pb.X) > w || Math.Max(pa.Y, pb.Y) < cullOy || Math.Min(pa.Y, pb.Y) > h)
            {
                continue;
            }

            // 取两端颜色的混合，保持区域色彩感；任一端被筛选灰化时连线也灰化并降透明度
            var mixed = new SKColor(
                (byte)((na.Color.Red + nb.Color.Red) / 2),
                (byte)((na.Color.Green + nb.Color.Green) / 2),
                (byte)((na.Color.Blue + nb.Color.Blue) / 2));
            var dimmed = !na.Enabled || !nb.Enabled;
            var mixedColor = dimmed ? DimColor(mixed) : mixed;
            paint.Color = new SKColor(mixedColor.Red, mixedColor.Green, mixedColor.Blue, dimmed ? (byte)(alpha / 3) : (byte)alpha);
            canvas.DrawLine(pa, pb, paint);
        }
    }

    /// <summary>跳桥点线（画进底图缓存；与 WinUI 版一致用点线区分星门实线）。</summary>
    private void DrawBridges(SKCanvas canvas, SKPaint paint)
    {
        var w = ActualWidth + _bakePad;   // 烘焙帧含 pad 环；左界见 cullOx
        var h = ActualHeight + _bakePad;
        var cullOx = -_bakePad;
        var cullOy = -_bakePad;
        using var dash = SKPathEffect.CreateDash([2f, 4f], 0f);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.6f;
        paint.Color = Bridge;
        paint.PathEffect = dash;
        foreach (var (a, b) in _bridges)
        {
            if (!_indexById.TryGetValue(a, out var ia) || !_indexById.TryGetValue(b, out var ib))
            {
                continue;
            }

            var pa = NodePos(_nodes[ia]);
            var pb = NodePos(_nodes[ib]);
            if (Math.Max(pa.X, pb.X) < cullOx || Math.Min(pa.X, pb.X) > w || Math.Max(pa.Y, pb.Y) < cullOy || Math.Min(pa.Y, pb.Y) > h)
            {
                continue;
            }

            canvas.DrawLine(pa, pb, paint);
        }

        paint.PathEffect = null;
    }

    /// <summary>一跳覆盖圈（虚线圆，覆盖层绘制，不参与底图缓存）。</summary>
    private void DrawCover(SKCanvas canvas, SKPaint paint, float nodeR)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.2f;
        paint.Color = Accent(205);
        using var dash = SKPathEffect.CreateDash([3f, 3f], 0f);
        paint.PathEffect = dash;
        var radius = nodeR + 6;
        foreach (var id in _coverIds)
        {
            if (!_indexById.TryGetValue(id, out var index))
            {
                continue;
            }

            var node = _nodes[index];
            if (!node.Visible)
            {
                continue;
            }

            canvas.DrawCircle(NodePos(node), radius, paint);
        }

        paint.PathEffect = null;
    }

    private void DrawNodes(SKCanvas canvas, SKPaint paint, SKFont font, double zmult, float nodeR)
    {
        paint.Style = SKPaintStyle.Fill;
        var w = ActualWidth + _bakePad;   // 烘焙帧含 pad 环：环内节点也要烘（拖动帧会裁到）；左界见 cullOx
        var h = ActualHeight + _bakePad;
        var cullOx = -_bakePad;
        var cullOy = -_bakePad;
        // LOD 连续渐显（09-23 实测：离散开关在翻转帧瞬间全图 8000 标签出现/消失，被感知为"浓度突变"）：
        // zmult 5.5→7.5（name）、13→15（sec）线性 0→1，完全显示后 alpha 封顶——缩放跨阈值变成平滑淡入。
        var secA = Math.Clamp((zmult - 13) / 2.0, 0, 1);
        var nameA = Math.Clamp((zmult - 5.5) / 2.0, 0, 1);
        var showSec = secA > 0;
        var showName = nameA > 0;
        var glowR = nodeR * GlowScale(zmult);
        var glowAlpha = (byte)GlowAlpha(zmult);

        var secTextSize = (float)Math.Clamp(zmult * 0.55, 6, 11);
        var nameTextSize = (float)Math.Clamp(zmult * 0.7, 6.5, 12.5);
        var secBadgeOn = secA > 0.04f;   // 渐显尾部 alpha<10/255 不可见，整屏跳过（省 MeasureText/底圈/文字）
        foreach (var node in _nodes)
        {
            var p = NodePos(node);
            if (p.X < cullOx - glowR || p.Y < cullOy - glowR || p.X > w + glowR || p.Y > h + glowR)
            {
                node.Visible = false;
                continue;
            }
            node.Visible = true;

            var c = node.Enabled ? node.Color : DimColor(node.Color);

            // 外发光（精灵缓存；低缩放时透明度趋近 0，避免 8k 节点糊成一片）
            if (glowAlpha > 3)
            {
                var glow = GetGlowImage(c);
                var dest = new SKRect(p.X - glowR, p.Y - glowR, p.X + glowR, p.Y + glowR);
                paint.Color = new SKColor(255, 255, 255, glowAlpha);
                canvas.DrawImage(glow, dest, paint);
            }

            // 实心节点
            paint.Color = c;
            canvas.DrawCircle(p, nodeR, paint);

            // 高倍缩放：白色内核
            if (zmult > 30)
            {
                paint.Color = Kernel((byte)Math.Clamp((zmult - 30) * 4, 0, 160));
                canvas.DrawCircle(p, nodeR * 0.45f, paint);
            }

            // 主权模式：圆点上叠加联盟徽标（图标未到位 / 节点太小 / 被筛掉时保留分组色圆点）。
            // v18 性能关键：原实现每节点每帧 new SKPath + ClipPath(antialias) + Save/Restore——
            // 抗锯齿裁剪蒙版逐节点重建，可见节点峰值区（zmult≈10~14，数百节点）帧率被拖垮。
            // 改为每联盟一枚预烘圆形精灵（圆裁+白描边都在精灵里），此处只剩一次 DrawImage。
            if (_currentColorMode == MapColorMode.Sovereignty
                && node.Enabled
                && node.AllianceId > 0
                && nodeR >= 2.4f)
            {
                var sprite = GetSovLogoSprite(node.AllianceId);
                if (sprite is not null)
                {
                    var iconR = nodeR * 1.3f;
                    var half = iconR + 0.5f;   // 精灵含 0.5px 白描边外扩
                    paint.Color = SKColors.White;
                    canvas.DrawImage(sprite, new SKRect(p.X - half, p.Y - half, p.X + half, p.Y + half), paint);
                    paint.Style = SKPaintStyle.Fill;
                }
            }

            if (secBadgeOn && showSec)
            {
                // 内圈文字随着色模式变化（安等 / 分组号 / 行星资源值 / 击杀·通行量）；无数据就不画（连底圈一起省掉）
                var label = FormatNodeLabel(node);
                if (label.Length > 0)
                {
                    font.Typeface = _typefaceBold;
                    font.Size = secTextSize;
                    var width = font.MeasureText(label);
                    paint.Color = BadgeBg((byte)(210 * secA));
                    canvas.DrawCircle(p, secTextSize * 0.85f + 2.5f, paint);
                    paint.Color = BadgeText((byte)(230 * secA));
                    canvas.DrawText(label, p.X - width / 2, p.Y + secTextSize * 0.36f, font, paint);
                    font.Typeface = _typeface;
                }
            }

            if (showName)
            {
                font.Size = nameTextSize;
                paint.Color = NameText((byte)(Math.Clamp(60 + zmult * 12, 80, 235) * nameA));
                canvas.DrawText(node.Name, p.X + nodeR + 3, p.Y - nodeR - 2, font, paint);
            }
        }
    }

    /// <summary>
    /// 取（或烘）指定联盟的圆形徽标精灵（v18）：源位图按联盟色圆形裁剪 + 0.5px 白描边，
    /// 一次烘焙成 128px SKImage 后每节点一次 DrawImage——替代每帧每节点 SKPath 裁剪。
    /// 源位图更换（SetSovIcon）时自动重烘；无源图标返回 null（调用方保留分组色圆点）。
    /// </summary>
    private SKImage? GetSovLogoSprite(long allianceId)
    {
        if (!_sovIcons.TryGetValue(allianceId, out var src) || src is null)
        {
            return null;
        }

        if (_sovLogoSprites.TryGetValue(allianceId, out var cached) && ReferenceEquals(cached.Src, src))
        {
            return cached.Image;
        }

        // 换过源图：先丢弃旧精灵
        if (_sovLogoSprites.TryGetValue(allianceId, out var old))
        {
            old.Image.Dispose();
            _sovLogoSprites.Remove(allianceId);
        }

        const int sz = 128;
        var bmp = new SKBitmap(sz, sz);
        using (var c2 = new SKCanvas(bmp))
        {
            c2.Clear(SKColors.Transparent);
            var r = sz / 2f - 0.5f;   // 留 0.5px 给描边
            using var clip = new SKPath();
            clip.AddCircle(sz / 2f, sz / 2f, r, SKPathDirection.Clockwise);
            c2.Save();
            c2.ClipPath(clip, antialias: true);
            c2.DrawBitmap(src, new SKRect(0, 0, sz, sz));
            c2.Restore();
            using var ring = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                Color = new SKColor(255, 255, 255, 90),
            };
            c2.DrawCircle(sz / 2f, sz / 2f, r, ring);
        }

        var image = SKImage.FromBitmap(bmp);
        bmp.Dispose();
        _sovLogoSprites[allianceId] = (image, src);
        return image;
    }

    /// <summary>取（或生成）指定颜色的外发光精灵。颜色量化到 5bit/通道，缓存上限 96。</summary>
    private SKImage GetGlowImage(SKColor color)
    {
        var key = (color.Red >> 3 << 10) | (color.Green >> 3 << 5) | (color.Blue >> 3);
        if (_glowCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        const int size = 96;
        var bitmap = new SKBitmap(size, size);
        using (var surface = new SKCanvas(bitmap))
        {
            using var p = new SKPaint { IsAntialias = true };
            var center = size / 2f;
            p.Shader = SKShader.CreateRadialGradient(
                new SKPoint(center, center), center,
                [
                    new SKColor(color.Red, color.Green, color.Blue, 235),
                    new SKColor(color.Red, color.Green, color.Blue, 55),
                    SKColors.Transparent,
                ],
                [0f, 0.45f, 1f], SKShaderTileMode.Clamp);
            surface.Clear(SKColors.Transparent);
            surface.DrawRect(0, 0, size, size, p);
        }

        var image = SKImage.FromBitmap(bitmap);
        bitmap.Dispose();
        if (_glowCache.Count > 96)
        {
            foreach (var kv in _glowCache)
            {
                kv.Value.Dispose();
            }
            _glowCache.Clear();
        }
        _glowCache[key] = image;
        return image;
    }

    /// <summary>节点上的安全等级文本（负安等按实际显示，见 <see cref="Helpers.MapTextHelper.FormatSecurity"/>）。</summary>
    private static string FormatSecurity(double sec) => Helpers.MapTextHelper.FormatSecurity(Math.Max(sec, -1));

    /// <summary>
    /// 节点内圈文字（对着色模式变化，与 WinUI 的 <c>InnerText</c> 语义一致）：
    /// 安等模式 = 安全等级；主权模式 = **分组号**（无主权不显示）；行星资源模式 = **该星系资源值**；
    /// 击杀 / 通行模式 = **热度值**。无数据返回空串（调用方连底圈一起省掉）。
    /// </summary>
    private string FormatNodeLabel(MapSystemNode node) => _currentColorMode switch
    {
        // 主权模式：圆点显示联盟徽标即可，不再画分组号徽章（用户定论"显示联盟徽标即可，不需要再显示编号"）
        MapColorMode.Sovereignty => string.Empty,
        MapColorMode.PlanetResource => node.Resource >= 0 ? NormalizeCount((long)Math.Round(node.Resource)) : string.Empty,
        MapColorMode.Kills or MapColorMode.Jumps => node.Heat >= 0 ? NormalizeCount((long)Math.Round(node.Heat)) : string.Empty,
        _ => FormatSecurity(node.Security),
    };

    /// <summary>大数字缩写（1.2k / 3.4m / 5.6b / 7.8t；0 显示 "0"），与 WinUI 的 ISKNormalize 口径一致。</summary>
    private static string NormalizeCount(long value)
    {
        if (value <= 0)
        {
            return "0";
        }

        if (value < 1_000)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        if (value < 1_000_000)
        {
            return (value / 1_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "k";
        }

        if (value < 1_000_000_000)
        {
            return (value / 1_000_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "m";
        }

        return value < 1_000_000_000_000
            ? (value / 1_000_000_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "b"
            : (value / 1_000_000_000_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "t";
    }

    // ---------- 航线 ----------

    private void DrawRoute(SKCanvas canvas, SKPaint paint, double zmult, float nodeR)
    {
        var points = new List<SKPoint>(_routePath.Count);
        foreach (var id in _routePath)
        {
            if (_indexById.TryGetValue(id, out var index))
            {
                points.Add(NodePos(_nodes[index]));
            }
        }
        if (points.Count < 2)
        {
            return;
        }

        var path = new SKPath();
        path.MoveTo(points[0]);
        for (var i = 1; i < points.Count; i++)
        {
            path.LineTo(points[i]);
        }

        paint.Style = SKPaintStyle.Stroke;
        // 外发光
        paint.StrokeWidth = Math.Max(4, nodeR * 1.6f);
        paint.Color = Accent(45);
        canvas.DrawPath(path, paint);
        // 主线
        paint.StrokeWidth = Math.Max(1.4f, nodeR * 0.45f);
        paint.Color = Accent(200);
        canvas.DrawPath(path, paint);
        path.Dispose();

        // 流动光点
        var flow = (_pulsePhase * 0.6) % 1;
        var seg = (points.Count - 1) * flow;
        var i0 = Math.Min((int)seg, points.Count - 2);
        var t = seg - i0;
        var fp = new SKPoint(
            points[i0].X + (points[i0 + 1].X - points[i0].X) * (float)t,
            points[i0].Y + (points[i0 + 1].Y - points[i0].Y) * (float)t);
        paint.Style = SKPaintStyle.Fill;
        paint.Color = RouteFlow(230);
        canvas.DrawCircle(fp, Math.Max(2, nodeR * 0.5f), paint);

        // 关键航点徽标
        foreach (var wi in _routeWaypoints)
        {
            if (wi < 0 || wi >= points.Count)
            {
                continue;
            }

            var p = points[wi];
            paint.Color = Accent(235);
            canvas.DrawCircle(p, nodeR + 3.5f, paint);
            paint.Color = BadgeBg(240);
            canvas.DrawCircle(p, nodeR + 1.8f, paint);
            if (zmult > 1.6)
            {
                using var font = new SKFont(_typefaceBold, Math.Max(7, nodeR + 4));
                paint.Color = Accent(255);
                var text = wi.ToString(CultureInfo.InvariantCulture);
                var width = font.MeasureText(text);
                canvas.DrawText(text, p.X - width / 2, p.Y + font.Size * 0.36f, font, paint);
            }
        }
    }

    // ---------- 情报 ----------

    /// <summary>
    /// 情报层：红圈脉冲（圈大小随威胁权重）+ 舰船图标与计数 + 频道标注
    /// （对齐 WinUI IntelDrawer——高缩放逐个画舰船图标、撞到邻近星系就折叠成 "+剩余"，低缩放只画汇总；
    /// 频道情报在圈下方标 📢 条数与最早时间）。
    /// </summary>
    private void DrawIntel(SKCanvas canvas, SKPaint paint, float nodeR, double zmult)
    {
        // 情报图标要做"空白探测避让"：先把本帧可见星系的屏幕网格建好（每帧一次，O(节点数)）
        var iconCell = Math.Max(8f, nodeR * 2.4f);
        BuildIntelOccupancy(iconCell);

        foreach (var marker in _intel)
        {
            if (!_indexById.TryGetValue(marker.SystemId, out var index))
            {
                continue;
            }

            var node = _nodes[index];
            if (!node.Visible)
            {
                continue;
            }

            var p = NodePos(node);
            var pulse = (float)(0.5 + 0.5 * Math.Sin(_pulsePhase * 3.2));
            var weight = Math.Clamp(marker.Weight, 1, 50);
            var radius = nodeR + 3 + (float)(weight * 0.55) * (1 + pulse * 0.12f);

            // 红色警戒光圈（少量标记，直接用渐变着色器）
            using (var glow = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill })
            {
                glow.Shader = SKShader.CreateRadialGradient(p, radius * 1.8f,
                    [IntelGlow((byte)(70 + pulse * 50)), SKColors.Transparent],
                    [0.55f, 1f], SKShaderTileMode.Clamp);
                canvas.DrawCircle(p, radius * 1.8f, glow);
            }

            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1.6f;
            paint.Color = IntelRed((byte)(150 + pulse * 100));
            canvas.DrawCircle(p, radius, paint);

            paint.StrokeWidth = 1f;
            paint.Color = IntelRed(90);
            canvas.DrawCircle(p, radius + 3 + pulse * 2.5f, paint);

            DrawIntelShips(canvas, paint, marker, index, p, nodeR, zmult, iconCell);
        }
    }

    /// <summary>情报层：舰船图标 / 计数 / 相对时间 / 频道标注。图标遇邻近星系占位冲突会让位（折叠成 "+剩余"）。</summary>
    private void DrawIntelShips(SKCanvas canvas, SKPaint paint, IntelMarker marker, int selfIndex, SKPoint p, float nodeR, double zmult, float iconCell)
    {
        if (marker.ShipCount <= 0 && marker.ChannelCount <= 0)
        {
            return;
        }

        var iconSize = Math.Max(10, nodeR * 2.4f);
        var x = p.X + nodeR * 1.4f;
        var y = p.Y;

        if (zmult >= IntelShipIconZoom && marker.Ships.Count > 0)
        {
            var drawn = 0;
            foreach (var (shipTypeId, count) in marker.Ships)
            {
                var rect = new SKRect(x, y - iconSize / 2, x + iconSize, y + iconSize / 2);

                // 空白探测：这一格压到别的星系了就让位，剩下的折成 "+N"
                if (IsIntelSlotOccupied(rect, selfIndex, iconCell))
                {
                    break;
                }

                paint.Style = SKPaintStyle.Fill;
                if (_intelShipImages.TryGetValue(shipTypeId, out var img) && img is not null)
                {
                    canvas.DrawBitmap(img, rect);
                }
                else
                {
                    // 图标未下载完：先画占位方块
                    paint.Color = IntelRed(190);
                    canvas.DrawRoundRect(rect, 2, 2, paint);
                }

                if (count > 1)
                {
                    var text = $"×{count}";
                    using var badgeFont = new SKFont(_typefaceBold, Math.Max(7, iconSize * 0.62f));
                    paint.Color = IntelText(240);
                    canvas.DrawText(text, rect.Right - badgeFont.MeasureText(text), rect.Top + badgeFont.Size * 0.9f, badgeFont, paint);
                }

                x += iconSize + 2;
                drawn++;
                if (drawn >= MaxIntelShipIcons)
                {
                    break;
                }
            }

            if (marker.ShipCount > drawn)
            {
                using var moreFont = new SKFont(_typeface, Math.Max(7, iconSize * 0.7f));
                paint.Color = IntelSoft(235);
                canvas.DrawText($"+{marker.ShipCount - drawn}", x + 1, y + moreFont.Size * 0.4f, moreFont, paint);
            }
        }
        else
        {
            // 低缩放：只画一个汇总（舰船数 + 频道情报条数）
            using var sumFont = new SKFont(_typeface, Math.Max(7, nodeR * 0.9f + 5));
            paint.Color = IntelSoft(235);
            canvas.DrawText($"+{marker.ShipCount}({marker.ChannelCount})", x, y + sumFont.Size * 0.4f, sumFont, paint);
        }

        // 相对时间（🕒 用 emoji 字体画，拿不到就退回 UI 字体）
        if (zmult >= IntelElapsedZoom && marker.OldestUtc != default)
        {
            using var timeFont = new SKFont(_emojiTypeface ?? _typefaceBold, Math.Max(7, nodeR * 0.7f + 4));
            paint.Color = IntelTime(220);
            canvas.DrawText($"🕒 {FormatElapsed(marker.OldestUtc)}", x, y + iconSize * 0.8f, timeFont, paint);
        }

        // 频道情报标注：📢 条数 + 最早一条的时间（画在圈下方，与 WinUI 的 IntelDrawer 一致）
        if (marker.ChannelCount > 0)
        {
            using var channelFont = new SKFont(_emojiTypeface ?? _typeface, Math.Max(7, nodeR * 0.8f + 4));
            paint.Color = IntelTime(230);
            var elapsed = marker.OldestUtc == default ? string.Empty : FormatElapsed(marker.OldestUtc);
            var text = string.Format(FindString("MapPage_IntelChannelTag"), marker.ChannelCount, elapsed);
            canvas.DrawText(text, p.X - nodeR, p.Y + nodeR + channelFont.Size * 1.5f, channelFont, paint);
        }
    }

    /// <summary>相对时间（"42s / 3m / 2h"）。</summary>
    private static string FormatElapsed(DateTime oldestUtc)
    {
        var elapsed = DateTime.UtcNow - oldestUtc;
        if (elapsed.TotalSeconds < 60)
        {
            return $"{(int)Math.Max(0, elapsed.TotalSeconds)}s";
        }

        return elapsed.TotalMinutes < 60 ? $"{(int)elapsed.TotalMinutes}m" : $"{(int)elapsed.TotalHours}h";
    }

    // ---------- 情报图标避让（空白探测） ----------

    private readonly Dictionary<long, List<int>> _intelOccupancy = [];

    /// <summary>为一帧构建"可见星系 → 屏幕网格"索引（格子边长 = 图标尺寸）。</summary>
    private void BuildIntelOccupancy(float cell)
    {
        _intelOccupancy.Clear();
        for (var i = 0; i < _nodes.Length; i++)
        {
            var node = _nodes[i];
            if (!node.Visible)
            {
                continue;
            }

            var p = NodePos(node);
            var key = PackCell((int)Math.Floor(p.X / cell), (int)Math.Floor(p.Y / cell));
            if (!_intelOccupancy.TryGetValue(key, out var list))
            {
                _intelOccupancy[key] = list = [];
            }

            list.Add(i);
        }
    }

    private static long PackCell(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

    /// <summary>该图标格是否压到了"本星系之外"的星系节点（压到就让位，避免糊成一片）。</summary>
    private bool IsIntelSlotOccupied(SKRect rect, int selfIndex, float cell)
    {
        for (var cx = (int)Math.Floor(rect.Left / cell); cx <= (int)Math.Floor(rect.Right / cell); cx++)
        {
            for (var cy = (int)Math.Floor(rect.Top / cell); cy <= (int)Math.Floor(rect.Bottom / cell); cy++)
            {
                if (!_intelOccupancy.TryGetValue(PackCell(cx, cy), out var list))
                {
                    continue;
                }

                foreach (var index in list)
                {
                    if (index == selfIndex)
                    {
                        continue;
                    }

                    var q = NodePos(_nodes[index]);
                    if (q.X >= rect.Left - 2 && q.X <= rect.Right + 2 && q.Y >= rect.Top - 2 && q.Y <= rect.Bottom + 2)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;

    // ---------- 角色标记 ----------

    private void DrawCharacters(SKCanvas canvas, SKPaint paint, float nodeR, double zmult)
    {
        foreach (var marker in _characters)
        {
            if (!_indexById.TryGetValue(marker.SystemId, out var index))
            {
                continue;
            }

            var node = _nodes[index];
            var p = NodePos(node);
            var size = Math.Max(11, nodeR * 2.3f);
            var cy = p.Y - nodeR - size * 0.58f;
            var hasImage = _characterImages.TryGetValue(marker.CharacterId, out var img) && img is not null;

            paint.Style = SKPaintStyle.Stroke;
            // 定位光环 + 连接线
            paint.StrokeWidth = 1.5f;
            paint.Color = CharBlue(220);
            canvas.DrawCircle(p.X, cy, size * 0.68f + 1.6f, paint);
            paint.StrokeWidth = 1f;
            paint.Color = CharBlue(110);
            canvas.DrawLine(p.X, p.Y - nodeR, p.X, cy + size * 0.68f, paint);

            paint.Style = SKPaintStyle.Fill;
            if (hasImage)
            {
                var rect = new SKRect(p.X - size / 2, cy - size / 2, p.X + size / 2, cy + size / 2);
                using var clip = new SKPath();
                clip.AddCircle(p.X, cy, size / 2, SKPathDirection.Clockwise);
                canvas.Save();
                canvas.ClipPath(clip, antialias: true);
                canvas.DrawBitmap(img, rect);
                canvas.Restore();
            }
            else
            {
                paint.Color = CharBlue(235);
                canvas.DrawCircle(p.X, cy, size * 0.42f, paint);
            }

            if (zmult > 2.2)
            {
                using var font = new SKFont(_typeface, Math.Clamp(nodeR * 0.8f + 5, 7, 11));
                paint.Color = CharText(230);
                canvas.DrawText(marker.Name, p.X + size * 0.75f, cy + font.Size * 0.35f, font, paint);
            }
        }
    }

    // ---------- 选中 / 悬停 / 高亮 ----------

    private void DrawHover(SKCanvas canvas, SKPaint paint, float nodeR)
    {
        if (_hovered is null || !_hovered.Visible)
        {
            return;
        }

        var p = NodePos(_hovered);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.4f;
        paint.Color = HoverRing(210);
        canvas.DrawCircle(p, nodeR + 2.6f, paint);

        // 相邻星门高亮
        if (_indexById.TryGetValue(_hovered.Id, out var index))
        {
            paint.StrokeWidth = 1.6f;
            paint.Color = AccentDim(190);
            foreach (var n in _adjacency[index])
            {
                canvas.DrawLine(p, NodePos(_nodes[n]), paint);
            }
        }
    }

    private void DrawSelection(SKCanvas canvas, SKPaint paint, float nodeR)
    {
        if (_selected is null || !_selected.Visible)
        {
            return;
        }

        // 静态双环（不参与动画帧，避免选中态常驻 60fps 重绘）
        var p = NodePos(_selected);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 2f;
        paint.Color = Accent(235);
        canvas.DrawCircle(p, nodeR + 4, paint);
        paint.StrokeWidth = 1.2f;
        paint.Color = Accent(110);
        canvas.DrawCircle(p, nodeR + 8, paint);
    }

    private void DrawHighlight(SKCanvas canvas, SKPaint paint, float nodeR)
    {
        if (_highlight is null)
        {
            return;
        }

        var elapsed = (DateTime.UtcNow - _highlightTime).TotalMilliseconds;
        if (elapsed > 1200)
        {
            _highlight = null;
            return;
        }

        if (_indexById.TryGetValue(_highlight.Id, out var index))
        {
            var p = NodePos(_nodes[index]);
            paint.Style = SKPaintStyle.Stroke;
            for (var i = 0; i < 3; i++)
            {
                var t = (elapsed / 400) - i * 0.33;
                if (t is < 0 or > 1)
                {
                    continue;
                }

                paint.StrokeWidth = 1.5f;
                paint.Color = Accent((byte)(255 * (1 - t)));
                canvas.DrawCircle(p, nodeR + 4 + (float)t * (18 + nodeR * 2), paint);
            }
        }
    }

    // ---------- 着色 ----------

    /// <summary>切换着色模式并重算节点颜色（热力三模式的圆点始终按值排名上色，色块是独立叠加的一层）。</summary>
    public void SetColorMode(MapColorMode mode, double killsMax = 0, double jumpsMax = 0, double resourceMax = 0)
    {
        _currentColorMode = mode;
        _dataVersion++;
        ApplyNodeColors();

        // 热力场挂在世界坐标上，只在"数值变了"时重建（缩放平移不重建 → 颜色稳定）
        RebuildHeatGrid();
        InvalidateVisual();
    }

    /// <summary>
    /// 按当前模式给所有圆点上色。热力三类模式（行星资源 / 击杀 / 通行）**始终按值排名上色**
    /// （用户定论"一直都显示颜色"，撤销早先"色块显示时圆点中性"的方案）：
    /// 有数据星系按全图分位排名取 11 色调色板（相同值同色，红=相对冷 → 青=相对热），无数据星系保持暗中性色；
    /// 色块层只是在这之上叠加的区域聚合视图，显隐不影响圆点。
    /// </summary>
    private void ApplyNodeColors()
    {
        var mode = _currentColorMode;
        if (mode is not (MapColorMode.Kills or MapColorMode.Jumps or MapColorMode.PlanetResource))
        {
            foreach (var node in _nodes)
            {
                node.Color = mode switch
                {
                    // 无主权星系（GroupId ≤ 0）直接用中性灰：原回退"灰化安等色"在低安区呈红棕色，
                    // 容易被误读成"某个联盟的分组色"——主权模式语义应为"只有有主权的星系才参与配色"（用户追问后修正）
                    MapColorMode.Sovereignty => node.GroupId > 0 ? SovGroupColor(node.GroupId, node.Security) : NeutralColor,
                    _ => SecurityColor(node.Security),
                };
            }

            return;
        }

        // 圆点按"全图分位排名"上色（与色块同一套归一化，最冷必红、最热必青、11 档均匀用满）
        var isResource = mode == MapColorMode.PlanetResource;
        var ordered = new List<double>(_nodes.Length);
        foreach (var node in _nodes)
        {
            var value = isResource ? node.Resource : node.Heat;
            if (value > 0)
            {
                ordered.Add(value);
            }
        }

        var rankByValue = ordered.Count > 0 ? BuildQuantileMap(ordered.OrderBy(p => p).ToList()) : [];
        foreach (var node in _nodes)
        {
            var value = isResource ? node.Resource : node.Heat;
            if (value > 0 && rankByValue.TryGetValue(value, out var t))
            {
                node.Color = SecurityColor(t);
            }
            else
            {
                node.Color = DimColor(NeutralColor);
            }
        }
    }

    /// <summary>
    /// 主权分组配色：按分组号取黄金角散列色相（同一分组永远同色、跨会话稳定，优于 WinUI 的每次随机）。
    /// 无主权星系（GroupId ≤ 0）在画布侧（ApplyNodeColors）已改用主题感知中性灰，不再走这里的回退；
    /// 此回退仅作静态兜底（分组设置窗预览只传正分组号，不受影响）。
    /// </summary>
    public static SKColor SovGroupColor(long groupId, double security)
    {
        if (groupId <= 0)
        {
            return LerpColor(SecurityColor(security), new SKColor(66, 72, 88), 0.75f);
        }

        return FromHsv((float)((groupId * 137.508) % 360), 0.62f, 0.92f);
    }

    private static SKColor FromHsv(float hue, float saturation, float value)
    {
        hue = (hue % 360 + 360) % 360;
        var c = value * saturation;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = value - c;
        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0f),
            < 120 => (x, c, 0f),
            < 180 => (0f, c, x),
            < 240 => (0f, x, c),
            < 300 => (x, 0f, c),
            _ => (c, 0f, x),
        };
        return new SKColor(
            (byte)Math.Clamp((r + m) * 255, 0, 255),
            (byte)Math.Clamp((g + m) * 255, 0, 255),
            (byte)Math.Clamp((b + m) * 255, 0, 255));
    }

    /// <summary>
    /// 安全等级 / 热力共用的 11 色调色板（**索引 0 = 最低档 #d10202 红 … 10 = 最高档 #24d7f9 青**），
    /// 与 WinUI 的 <c>SystemSecurityForeground00…1</c> 逐档一致；
    /// 画布与 UI 色阶图例共用这一份定义，避免两处硬编码走样。
    /// </summary>
    public static readonly IReadOnlyList<SKColor> Palette =
    [
        new(0xD1, 0x02, 0x02), // 00 最低
        new(0xC4, 0x26, 0x1E),
        new(0xEB, 0x49, 0x09),
        new(0xF6, 0x4D, 0x19),
        new(0xE5, 0x80, 0x00),
        new(0xD3, 0xD1, 0x12),
        new(0x8F, 0xF9, 0x30),
        new(0x15, 0xF1, 0x00),
        new(0x02, 0xF3, 0x45),
        new(0x2D, 0xD6, 0xC3),
        new(0x24, 0xD7, 0xF9), // 10 最高
    ];

    /// <summary>
    /// 安全等级配色——**与 WinUI 的 <c>SystemSecurityForegroundConverter</c> 完全一致**：
    /// 先 <c>Math.Round(sec, 1)</c> 取 0.1 分档，再取 <see cref="Palette"/> 对应档；
    /// 0.0 与负安等（虫洞 / Pochven 之类）一起落在最低档（#d10202 红）。
    /// </summary>
    public static SKColor SecurityColor(double sec)
    {
        var step = (int)Math.Round(Math.Round(sec, 1) * 10);
        return Palette[Math.Clamp(step, 0, Palette.Count - 1)];
    }

    /// <summary>无数据节点的中性色（深浅主题下都能看清的中灰）。</summary>
    private SKColor NeutralColor => _light ? new SKColor(168, 176, 192) : new SKColor(96, 104, 122);

    private static SKColor LerpColor(SKColor a, SKColor b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t));
    }
}
