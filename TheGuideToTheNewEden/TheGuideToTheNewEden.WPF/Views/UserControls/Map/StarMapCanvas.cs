using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

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

    // 底图缓存：视图未变时动画帧只重绘覆盖层，避免 8k 节点全量重绘
    private SKBitmap? _baseCache;
    private (double zoom, double ox, double oy, int w, int h, float dpi, int dataVer, int colorMode)? _baseKey;
    private int _dataVersion;
    private MapColorMode _currentColorMode = MapColorMode.Security;

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

        // 底图缓存：视图/数据未变时直接贴图，动画帧只叠加覆盖层
        var key = (_zoom, _offsetX, _offsetY, (int)w, (int)h, dpiScale, _dataVersion, (int)_currentColorMode);
        if (_baseCache is null || _baseKey is null || _baseKey.Value != key)
        {
            RebuildBase(e.Info.Width, e.Info.Height, w, h, zmult, nodeR);
            _baseKey = key;
        }
        canvas.DrawBitmap(_baseCache, new SKRect(0, 0, w, h));

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

        foreach (var kv in _glowCache)
        {
            kv.Value.Dispose();
        }

        _glowCache.Clear();
    }

    /// <summary>重建底图缓存（背景 + 星尘 + 星门连线 + 跳桥点线 + 全部节点与标签）。</summary>
    /// <remarks>
    /// 位图按尺寸复用：拖拽 / 滚轮 / 飞行动画期间每帧都会重建底图，尺寸不变时不再重新分配，
    /// 避免每帧 new SKBitmap（4K + 高 DPI 下约 30MB/帧）带来的 LOH 压力。
    /// </remarks>
    private void RebuildBase(int deviceW, int deviceH, float w, float h, double zmult, float nodeR)
    {
        var cache = _baseCache;
        if (cache is null || cache.Width != deviceW || cache.Height != deviceH)
        {
            _baseCache?.Dispose();
            _baseCache = new SKBitmap(deviceW, deviceH);
            cache = _baseCache;
        }

        using var baseCanvas = new SKCanvas(cache);
        baseCanvas.Clear(BgBase);
        baseCanvas.Scale(deviceW / w, deviceH / h);
        using var paint = new SKPaint { IsAntialias = true };
        using var font = new SKFont(_typeface, 10);

        DrawBackground(baseCanvas, paint, w, h, zmult);
        DrawLinks(baseCanvas, paint, zmult);
        if (_showBridges && _bridges.Count > 0)
        {
            DrawBridges(baseCanvas, paint);
        }

        DrawNodes(baseCanvas, paint, font, zmult, nodeR);
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
        var w = ActualWidth;
        var h = ActualHeight;
        foreach (var (a, b) in _links)
        {
            var na = _nodes[a];
            var nb = _nodes[b];
            var pa = NodePos(na);
            var pb = NodePos(nb);
            if (Math.Max(pa.X, pb.X) < 0 || Math.Min(pa.X, pb.X) > w || Math.Max(pa.Y, pb.Y) < 0 || Math.Min(pa.Y, pb.Y) > h)
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
        var w = ActualWidth;
        var h = ActualHeight;
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
            if (Math.Max(pa.X, pb.X) < 0 || Math.Min(pa.X, pb.X) > w || Math.Max(pa.Y, pb.Y) < 0 || Math.Min(pa.Y, pb.Y) > h)
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
        var w = ActualWidth;
        var h = ActualHeight;
        var showSec = zmult >= 13;
        var showName = zmult >= 5.5;
        var glowR = nodeR * GlowScale(zmult);
        var glowAlpha = (byte)GlowAlpha(zmult);

        var secTextSize = (float)Math.Clamp(zmult * 0.55, 6, 11);
        var nameTextSize = (float)Math.Clamp(zmult * 0.7, 6.5, 12.5);

        foreach (var node in _nodes)
        {
            var p = NodePos(node);
            if (p.X < -glowR || p.Y < -glowR || p.X > w + glowR || p.Y > h + glowR)
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

            if (showSec)
            {
                // 主权着色时内圈显示分组号（与 WinUI 的 InnerText 语义一致），其余模式显示安全等级
                var secText = _currentColorMode == MapColorMode.Sovereignty && node.GroupId > 0
                    ? node.GroupId.ToString(CultureInfo.InvariantCulture)
                    : FormatSecurity(node.Security);
                font.Typeface = _typefaceBold;
                font.Size = secTextSize;
                var width = font.MeasureText(secText);
                paint.Color = BadgeBg(210);
                canvas.DrawCircle(p, secTextSize * 0.85f + 2.5f, paint);
                paint.Color = BadgeText(230);
                canvas.DrawText(secText, p.X - width / 2, p.Y + secTextSize * 0.36f, font, paint);
                font.Typeface = _typeface;
            }

            if (showName)
            {
                font.Size = nameTextSize;
                paint.Color = NameText((byte)Math.Clamp(60 + zmult * 12, 80, 235));
                canvas.DrawText(node.Name, p.X + nodeR + 3, p.Y - nodeR - 2, font, paint);
            }
        }
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

    private static string FormatSecurity(double sec)
    {
        var v = Math.Round(Math.Max(sec, -1), 1);
        return v <= 0 ? "0.0" : v.ToString("0.0", CultureInfo.InvariantCulture);
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

    /// <summary>切换着色模式并重算节点颜色（热度用 Heat，行星资源用 Resource，主权用分组号）。</summary>
    public void SetColorMode(MapColorMode mode, double killsMax = 0, double jumpsMax = 0, double resourceMax = 0)
    {
        _currentColorMode = mode;
        _dataVersion++;
        foreach (var node in _nodes)
        {
            node.Color = mode switch
            {
                MapColorMode.Kills => HeatColor(node.Heat, killsMax),
                MapColorMode.Jumps => HeatColor(node.Heat, jumpsMax),
                MapColorMode.PlanetResource => HeatColor(node.Resource, resourceMax),
                MapColorMode.Sovereignty => SovGroupColor(node.GroupId, node.Security),
                _ => SecurityColor(node.Security),
            };
        }
        InvalidateVisual();
    }

    /// <summary>
    /// 主权分组配色：按分组号取黄金角散列色相（同一分组永远同色、跨会话稳定，优于 WinUI 的每次随机）。
    /// 无主权星系（GroupId ≤ 0）退回灰化后的安全等级色。
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

    public static SKColor SecurityColor(double sec)
    {
        if (sec >= 0.5)
        {
            return new SKColor(46, 230, 168);
        }
        if (sec > 0)
        {
            // 低安：橙 → 青绿过渡
            var t = (float)(sec / 0.5);
            return LerpColor(new SKColor(255, 178, 61), new SKColor(46, 230, 168), t);
        }
        return new SKColor(255, 77, 106);
    }

    public static SKColor HeatColor(double value, double maxValue)
    {
        if (maxValue <= 0 || value < 0)
        {
            return new SKColor(120, 130, 160);
        }
        var t = Math.Clamp(Math.Log(1 + value) / Math.Log(1 + maxValue), 0, 1);
        return t < 0.5
            ? LerpColor(new SKColor(61, 107, 255), new SKColor(255, 177, 61), (float)(t * 2))
            : LerpColor(new SKColor(255, 177, 61), new SKColor(255, 68, 68), (float)((t - 0.5) * 2));
    }

    private static SKColor LerpColor(SKColor a, SKColor b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t));
    }
}
