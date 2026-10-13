using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.WPF.Services.Home;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.ViewModels.Home;

/// <summary>
/// 主页上的一个磁贴（阶段 101 起只有功能磁贴——「＋ 添加功能」槽位随编辑模式移除，
/// 编辑统一在 设置 → 主页 的行列表里）。主页磁贴与设置页的快捷方式行共用本视图模型：
/// 标题 / 副标题 / 图标 / 尺寸 / 拖动视觉（IsDragging / IsDropTarget）都是同一套。
/// </summary>
public sealed class HomeShortcutTileViewModel : INotifyPropertyChanged
{
    public const double SmallWidth = 112;
    public const double WideWidth = 232;
    public const double TileHeight = 112;

    private bool _isDragging;
    private bool _isDropTarget;

    public HomeShortcutTileViewModel(HomeShortcutItem item, HomeFeature feature)
    {
        Item = item;
        Feature = feature;
        Id = feature.Id;
        IsWide = item.IsWide;
    }

    /// <summary>对应的数据项（参数化磁贴的 <c>Param</c> / <c>Label</c> 在这里）。</summary>
    public HomeShortcutItem? Item { get; }

    public string Id { get; }

    /// <summary>参数（无参数为 null）。服务层的增删排序都按 (Id, Param) 定位。</summary>
    public string? Param => Item?.Param;

    public HomeFeature? Feature { get; }

    public SymbolRegular Icon => Feature?.Icon ?? SymbolRegular.Add24;

    public bool IsWide { get; private set; }

    /// <summary>小磁贴内容（非宽）。</summary>
    public bool IsSmall => !IsWide;

    public double TileWidth => IsWide ? WideWidth : SmallWidth;

    /// <summary>
    /// 标题：带参数的磁贴显示参数名（如"因卡萨斯级"），否则显示功能名。
    /// </summary>
    public string Title => string.IsNullOrWhiteSpace(Item?.Label) ? Feature?.Title ?? Id : Item!.Label!;

    /// <summary>宽磁贴的第二行：无参数时是所属分组名，带参数时是功能名。</summary>
    public string Subtitle => string.IsNullOrWhiteSpace(Item?.Label)
        ? Feature?.GroupTitle ?? string.Empty
        : Feature?.Title ?? string.Empty;

    /// <summary>是否显示状态角标（运行态由 <c>HomeFeatureStatusService</c> 提供）。</summary>
    public bool HasBadge { get; private set; }

    /// <summary>角标提示（"预警中：2 个角色"这类）。</summary>
    public string BadgeToolTip { get; private set; } = string.Empty;

    /// <summary>参数化磁贴的提示（显示完整的"功能 + 参数"）。</summary>
    public string DetailToolTip => string.IsNullOrWhiteSpace(Item?.Label)
        ? $"{Feature?.Title} · {Feature?.Description}"
        : $"{Feature?.Title} · {Item!.Label}";

    /// <summary>
    /// 设置页行内"切宽/切小"按钮的提示：按当前尺寸显示目标尺寸（与主页旧右键菜单同规则）。
    /// </summary>
    public string SizeToggleToolTip => Resolve(IsWide ? "Home.Quick.ToSmall" : "Home.Quick.ToWide");

    /// <summary>设置页行内宽窄切换按钮的图标：小磁贴显示"放大"意图，宽磁贴显示"缩小"意图。</summary>
    public SymbolRegular SizeToggleIcon => IsWide ? SymbolRegular.ArrowMinimize24 : SymbolRegular.ArrowMaximize24;

    /// <summary>正在被拖动（模板里降低不透明度）。</summary>
    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            if (_isDragging == value)
            {
                return;
            }

            _isDragging = value;
            OnPropertyChanged();
        }
    }

    /// <summary>拖动时的落位目标（模板里显示强调色描边）。</summary>
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set
        {
            if (_isDropTarget == value)
            {
                return;
            }

            _isDropTarget = value;
            OnPropertyChanged();
        }
    }

    /// <summary>切小 / 宽磁贴后刷新与尺寸有关的通知（含设置页按钮的提示文案）。</summary>
    public void SetWide(bool wide)
    {
        if (IsWide == wide)
        {
            return;
        }

        IsWide = wide;
        OnPropertyChanged(nameof(IsWide));
        OnPropertyChanged(nameof(IsSmall));
        OnPropertyChanged(nameof(TileWidth));
        OnPropertyChanged(nameof(SizeToggleToolTip));
        OnPropertyChanged(nameof(SizeToggleIcon));
    }

    /// <summary>语言切换后标题 / 副标题重新取本地化文本。</summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(DetailToolTip));
        OnPropertyChanged(nameof(SizeToggleToolTip));
    }

    /// <summary>
    /// 刷新状态角标。运行态只从各处<b>已有的</b>状态里读（见
    /// <see cref="Services.Home.HomeFeatureStatusService"/>），不启动任何后台任务。
    /// </summary>
    public void RefreshBadge()
    {
        var status = Services.Home.HomeFeatureStatusService.Get(Id);

        var hasBadge = status is not null;
        var tooltip = status is null
            ? string.Empty
            : status.Count > 0
                ? string.Format(Resolve("Home.Quick.BadgeCount"), Feature?.Title ?? Id, status.Count)
                : string.Format(Resolve("Home.Quick.BadgeRunning"), Feature?.Title ?? Id);

        if (HasBadge == hasBadge && BadgeToolTip == tooltip)
        {
            return;
        }

        HasBadge = hasBadge;
        BadgeToolTip = tooltip;
        OnPropertyChanged(nameof(HasBadge));
        OnPropertyChanged(nameof(BadgeToolTip));
    }

    private static string Resolve(string key)
        => System.Windows.Application.Current?.TryFindResource(key) as string ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
