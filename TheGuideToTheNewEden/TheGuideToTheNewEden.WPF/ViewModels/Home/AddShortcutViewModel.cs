using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.WPF.Services.Home;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.ViewModels.Home;

/// <summary>「添加功能」窗口左侧的一个分组（<c>Key == null</c> 表示"全部功能"）。</summary>
public sealed class AddShortcutGroup
{
    public string? Key { get; init; }

    public string Title { get; init; } = string.Empty;

    /// <summary>该分组所属功能的 <c>IconKey</c>（用于左侧彩色矢量图标；"全部功能"为 null 走 <c>NavIcon.home</c>）。</summary>
    public string? IconKey { get; init; }

    /// <summary>
    /// 彩色矢量图标对象（供 <c>Image.Source</c> 绑定）。
    /// <b>必须返回对象而不是字符串键</b> —— 绑字符串时 <c>ImageSourceConverter</c> 会把它当文件路径解析，
    /// 而不是去资源字典里查。
    /// </summary>
    public object? IconSource =>
        Application.Current?.TryFindResource("NavIcon." + (IconKey ?? "home"));

    public override string ToString() => Title;
}

/// <summary>「添加功能」窗口右侧的一行功能。</summary>
public sealed class AddShortcutRow : INotifyPropertyChanged
{
    private bool _isAdded;

    public AddShortcutRow(HomeFeature feature, bool isAdded)
    {
        Feature = feature;
        _isAdded = isAdded;
    }

    public HomeFeature Feature { get; }

    /// <summary>字体图标（尚无彩色矢量对应的场景）。</summary>
    public SymbolRegular Icon => Feature.Icon;

    /// <summary>彩色矢量图标对象（右侧列表用，与导航/磁贴同款）。</summary>
    public object? TileIcon => Application.Current?.TryFindResource(Feature.IconResourceKey);

    public string Title => Feature.Title;

    public string Description => Feature.Description;

    public bool IsAdded
    {
        get => _isAdded;
        private set
        {
            if (_isAdded == value)
            {
                return;
            }

            _isAdded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActionText));
        }
    }

    /// <summary>右侧按钮文案：已添加 / 添加。</summary>
    public string ActionText => Resolve(IsAdded ? "Home.Add.Added" : "Home.Add.Add");

    /// <summary>同步服务的当前状态（主页那边删掉了也要立刻反映）。</summary>
    public void SyncAdded(bool isAdded) => IsAdded = isAdded;

    internal static string Resolve(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 「添加功能」窗口的视图模型：分组筛选 + 搜索 + 逐个添加 / 移除。
/// <para>
/// 改动直接写 <see cref="HomeShortcutService"/>，主页那边通过 <c>Changed</c> 实时刷新——
/// 这个窗口<b>不缓存</b>自己的快捷方式列表，避免两处状态不一致。
/// </para>
/// </summary>
public sealed class AddShortcutViewModel : INotifyPropertyChanged
{
    private AddShortcutGroup? _selectedGroup;
    private string _searchText = string.Empty;

    public AddShortcutViewModel()
    {
        Groups.Add(new AddShortcutGroup { Key = null, Title = Resolve("Home.Add.GroupAll") });
        foreach (var key in HomeFeatureCatalog.GroupKeys)
        {
            // 分组图标取该分组下第一个功能的 IconKey（分组本身就是"某个导航页"或"某类设置"）。
            var iconKey = HomeFeatureCatalog.Features
                .FirstOrDefault(p => p.GroupKey == key)?.IconKey;

            Groups.Add(new AddShortcutGroup
            {
                Key = key,
                IconKey = iconKey,
                Title = Application.Current?.TryFindResource(key) as string ?? key,
            });
        }

        _selectedGroup = Groups[0];
        HomeShortcutService.Changed += OnShortcutsChanged;
        RefreshRows();
    }

    public ObservableCollection<AddShortcutGroup> Groups { get; } = [];

    public ObservableCollection<AddShortcutRow> Rows { get; } = [];

    public AddShortcutGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (ReferenceEquals(_selectedGroup, value))
            {
                return;
            }

            _selectedGroup = value;
            OnPropertyChanged();
            RefreshRows();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged();
            RefreshRows();
        }
    }

    /// <summary>「已添加 12 个功能」。</summary>
    public string SummaryText => string.Format(
        Resolve("Home.Add.Summary"), HomeShortcutService.Items.Count, HomeShortcutService.MaxItems);

    /// <summary>达到上限后不再允许添加（已添加的仍可移除）。</summary>
    public bool CanAdd => !HomeShortcutService.IsFull;

    public void Dispose() => HomeShortcutService.Changed -= OnShortcutsChanged;

    /// <summary>添加 / 移除一行。</summary>
    public void Toggle(AddShortcutRow row)
    {
        if (row.IsAdded)
        {
            HomeShortcutService.Remove(row.Feature.Id);
        }
        else if (!HomeShortcutService.Add(row.Feature.Id))
        {
            // 典型原因是达到上限：给出明确提示，而不是"点了没反应"。
            Services.PageNotifyService.Warning(string.Format(Resolve("Home.Quick.LimitReached"), HomeShortcutService.MaxItems));
        }
    }

    /// <summary>语言切换后刷新分组名、行文案与统计（分组标题是本地化文本，只能整体重建后按 key 还原选中项）。</summary>
    public void RefreshTexts()
    {
        var selectedKey = _selectedGroup?.Key;

        var rebuilt = new List<AddShortcutGroup>
        {
            new() { Key = null, Title = Resolve("Home.Add.GroupAll") },
        };
        rebuilt.AddRange(HomeFeatureCatalog.GroupKeys.Select(key => new AddShortcutGroup
        {
            Key = key,
            Title = Application.Current?.TryFindResource(key) as string ?? key,
        }));

        Groups.Clear();
        foreach (var group in rebuilt)
        {
            Groups.Add(group);
        }

        // Clear() 会让 ListBox 的 SelectedItem 归零（并回调一次 SelectionChanged），
        // 这里按 key 把选中项放回去；找不到（理论上不会）就退回"全部功能"。
        _selectedGroup = Groups.FirstOrDefault(p => p.Key == selectedKey) ?? Groups[0];
        OnPropertyChanged(nameof(SelectedGroup));

        RefreshRows();
    }

    private void OnShortcutsChanged()
    {
        foreach (var row in Rows)
        {
            row.SyncAdded(HomeShortcutService.Contains(row.Feature.Id));
        }

        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(CanAdd));
    }

    private void RefreshRows()
    {
        var keyword = _searchText?.Trim();
        var groupKey = _selectedGroup?.Key;

        Rows.Clear();
        foreach (var feature in HomeFeatureCatalog.Features)
        {
            if (groupKey is not null && !string.Equals(feature.GroupKey, groupKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(keyword)
                && !Matches(feature, keyword))
            {
                continue;
            }

            Rows.Add(new AddShortcutRow(feature, HomeShortcutService.Contains(feature.Id)));
        }

        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(CanAdd));
    }

    private static bool Matches(HomeFeature feature, string keyword)
        => feature.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
           || feature.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)
           || feature.GroupTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase)
           || feature.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static string Resolve(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
