using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TheGuideToTheNewEden.WPF.ViewModels.Links;

/// <summary>快速链接的一个分组（收藏 / 某个分类 / 未分类）。</summary>
public sealed class LinkGroupViewModel : INotifyPropertyChanged
{
    /// <summary>收藏组的折叠状态键。用 NUL 前缀，与"分类名"这种用户可编辑的键不可能撞车。</summary>
    public const string FavoriteGroupKey = "\u0000favorite";

    private readonly Action<string, bool>? _expandedChanged;
    private bool _isExpanded;

    public LinkGroupViewModel(
        string title,
        IReadOnlyList<LinkItemViewModel> items,
        bool isFavoriteGroup = false,
        bool isExpanded = true,
        Action<string, bool>? expandedChanged = null)
    {
        Title = title;
        Items = items;
        IsFavoriteGroup = isFavoriteGroup;
        GroupKey = isFavoriteGroup
            ? FavoriteGroupKey
            : items.FirstOrDefault()?.CategoryKey ?? string.Empty;

        _isExpanded = isExpanded;
        _expandedChanged = expandedChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }

    public IReadOnlyList<LinkItemViewModel> Items { get; }

    public bool IsFavoriteGroup { get; }

    /// <summary>折叠状态的标识（收藏组是 <see cref="FavoriteGroupKey"/>，其余等于分类键）。</summary>
    public string GroupKey { get; }

    public string CountText => Items.Count.ToString();

    /// <summary>是否展开。默认展开；用户折叠后由 <see cref="LinksPageViewModel"/> 记住，
    /// 这样"搜索/筛选重建分组"或"切走再切回"都不会把它弹回展开。</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
            _expandedChanged?.Invoke(GroupKey, value);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
