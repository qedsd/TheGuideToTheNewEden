using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.WPF.Services.Home;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.ViewModels.Home;

/// <summary>
/// 主页上的一个磁贴。除了正常功能磁贴，末尾那个「＋ 添加功能」也用同一套模板渲染
/// （<see cref="IsAddSlot"/> = true）——这样它们处在同一个 <c>WrapPanel</c> 里自然换行，
/// 不必把两个 ItemsControl 嵌套进 WrapPanel（嵌套会带来"无限宽度测量"的排版问题）。
/// </summary>
public sealed class HomeShortcutTileViewModel : INotifyPropertyChanged
{
    public const double SmallWidth = 112;
    public const double WideWidth = 232;
    public const double TileHeight = 112;

    private bool _isEditing;
    private bool _isDragging;
    private bool _isDropTarget;

    /// <summary>「＋ 添加功能」槽位。</summary>
    public HomeShortcutTileViewModel()
    {
        IsAddSlot = true;
        Id = string.Empty;
    }

    public HomeShortcutTileViewModel(HomeShortcutItem item, HomeFeature feature)
    {
        Item = item;
        Feature = feature;
        Id = feature.Id;
        IsWide = item.IsWide;
    }

    /// <summary>对应的数据项（参数化磁贴的 <c>Param</c> / <c>Label</c> 在这里）。</summary>
    public HomeShortcutItem? Item { get; }

    public bool IsAddSlot { get; }

    public string Id { get; }

    /// <summary>参数（无参数为 null）。服务层的增删排序都按 (Id, Param) 定位。</summary>
    public string? Param => Item?.Param;

    public HomeFeature? Feature { get; }

    public SymbolRegular Icon => Feature?.Icon ?? SymbolRegular.Add24;

    public bool IsWide { get; private set; }

    /// <summary>小磁贴内容（非宽、非添加槽位）。</summary>
    public bool IsSmall => !IsAddSlot && !IsWide;

    public double TileWidth => IsAddSlot ? SmallWidth : IsWide ? WideWidth : SmallWidth;

    /// <summary>
    /// 标题：带参数的磁贴显示参数名（如"因卡萨斯级"），否则显示功能名。
    /// </summary>
    public string Title => IsAddSlot
        ? Resolve("Home.Quick.AddTile")
        : string.IsNullOrWhiteSpace(Item?.Label) ? Feature?.Title ?? Id : Item!.Label!;

    /// <summary>宽磁贴的第二行：无参数时是所属分组名，带参数时是功能名。</summary>
    public string Subtitle => string.IsNullOrWhiteSpace(Item?.Label)
        ? Feature?.GroupTitle ?? string.Empty
        : Feature?.Title ?? string.Empty;

    /// <summary>是否显示状态角标（运行态由 <c>HomeFeatureStatusService</c> 提供）。</summary>
    public bool HasBadge { get; private set; }

    /// <summary>角标提示（"预警中：2 个角色"这类）。</summary>
    public string BadgeToolTip { get; private set; } = string.Empty;

    /// <summary>参数化磁贴的提示（显示完整的"功能 + 参数"）。</summary>
    public string DetailToolTip => IsAddSlot
        ? Resolve("Home.Quick.AddTip")
        : string.IsNullOrWhiteSpace(Item?.Label)
            ? $"{Feature?.Title} · {Feature?.Description}"
            : $"{Feature?.Title} · {Item!.Label}";

    /// <summary>编辑态且不是添加槽位时才显示右上角 ✕。</summary>
    public bool ShowRemoveButton => IsEditing && !IsAddSlot;

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value)
            {
                return;
            }

            _isEditing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowRemoveButton));
        }
    }

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

    /// <summary>切小 / 宽磁贴后刷新与尺寸有关的通知。</summary>
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
    }

    /// <summary>语言切换后标题 / 副标题重新取本地化文本。</summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(DetailToolTip));
    }

    /// <summary>
    /// 刷新状态角标。运行态只从各处<b>已有的</b>状态里读（见
    /// <see cref="Services.Home.HomeFeatureStatusService"/>），不启动任何后台任务。
    /// </summary>
    public void RefreshBadge()
    {
        var status = IsAddSlot ? null : Services.Home.HomeFeatureStatusService.Get(Id);

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
