using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Links;

namespace TheGuideToTheNewEden.WPF.ViewModels.Links;

/// <summary>
/// 快速链接页：数据编排（读取 / 搜索 / 分类筛选 / 只看收藏 / 分组 / 增删改与落盘）。
/// 界面只做绑定与交互，所有筛选规则集中在这里。
/// </summary>
/// <remarks>
/// 相对 WinUI 版的增强（详见 REFACTORING.md）：
/// ① 搜索（名称/概述/描述/地址/分类/平台/语言）与分类筛选、只看收藏；
/// ② 收藏置顶（多分组：收藏项**同时**保留在原分类，并额外出现在最上面的「收藏」分组，所以统计要走去重）；
/// ③ 导入/导出 Links.json、恢复默认、打开配置目录、从磁盘重载；
/// ④ 分组可折叠、按条数排序、分类快捷跳转；卡片与跳转胶囊都不放图标（见 <see cref="LinkItemViewModel"/>）。
/// </remarks>
public sealed class LinksPageViewModel : INotifyPropertyChanged
{
    private readonly List<LinkItemViewModel> _all = [];
    private HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>分类筛选的备用键，索引与 <see cref="CategoryOptions"/> 对齐（0 = 全部）。</summary>
    private readonly List<string?> _categoryKeys = [null];

    private string _searchText = string.Empty;
    private int _selectedCategoryIndex;
    private bool _favoritesOnly;
    private bool _isLoaded;

    /// <summary>重建分类下拉项期间忽略 ComboBox 回写的 -1（清空集合会让它把选中项置空）。</summary>
    private bool _rebuildingCategories;

    /// <summary>
    /// 被用户折叠的分组（按 <see cref="LinkGroupViewModel.GroupKey"/> 记）。
    /// 分组在每次搜索/筛选时都会重建，折叠状态必须存在 VM 里才不会"一搜索就全弹开"；
    /// 页面是常驻缓存的，所以切走再切回也还在（仅本进程有效，未落盘）。
    /// </summary>
    private readonly HashSet<string> _collapsedGroups = new(StringComparer.Ordinal);

    /// <summary>分组显示序号：按条数从多到少（"常用的几组放前面"）。</summary>
    private readonly Dictionary<string, int> _groupOrder = new(StringComparer.Ordinal);

    /// <summary>当前显示的去重条数（见 <see cref="VisibleCount"/>）。</summary>
    private int _visibleCount;

    public LinksPageViewModel()
    {
        // 分组标题、「全部分类」等是"求值时查资源"拼出来的字符串，换语言后必须重算（见 §9 第 63 条）。
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>按分组呈现的可见链接。</summary>
    public ObservableCollection<LinkGroupViewModel> Groups { get; } = [];

    /// <summary>分类下拉框的选项（第 0 项是「全部分类」）。</summary>
    public ObservableCollection<string> CategoryOptions { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value ?? string.Empty;
            OnPropertyChanged();
            Refresh();
        }
    }

    public int SelectedCategoryIndex
    {
        get => _selectedCategoryIndex;
        set
        {
            if (_rebuildingCategories || _selectedCategoryIndex == value)
            {
                return;
            }

            _selectedCategoryIndex = value;
            OnPropertyChanged();
            Refresh();
        }
    }

    public bool FavoritesOnly
    {
        get => _favoritesOnly;
        set
        {
            if (_favoritesOnly == value)
            {
                return;
            }

            _favoritesOnly = value;
            OnPropertyChanged();
            Refresh();
        }
    }

    public bool IsLoaded
    {
        get => _isLoaded;
        private set
        {
            if (_isLoaded == value)
            {
                return;
            }

            _isLoaded = value;
            OnPropertyChanged();
        }
    }

    public int TotalCount => _all.Count;

    /// <summary>
    /// 当前显示的**去重**链接数。收藏项会同时出现在「收藏」组与原分类组（见 <see cref="Refresh"/>），
    /// 所以不能把各组条数直接相加——那会得到"共 82 个链接，当前显示 85 个"这种自相矛盾的数字。
    /// 在 <see cref="Refresh"/> 里算好存下来，避免每次绑定取值都跑一遍 LINQ。
    /// </summary>
    public int VisibleCount => _visibleCount;

    /// <summary>没有任何链接（连数据文件都是空的）：引导用户新建。</summary>
    public bool IsSourceEmpty => _all.Count == 0;

    /// <summary>有链接但当前筛选条件下一条都没匹配上。</summary>
    public bool IsFilteredEmpty => _all.Count > 0 && Groups.Count == 0;

    /// <summary>是否显示「分类快捷跳转」条（有分组才显示，空态下不占位置）。</summary>
    public bool HasJumpTargets => Groups.Count > 0;

    public string SummaryText
        => string.Format(FindString("LinksPage_Summary"), TotalCount, VisibleCount);

    // ---------- 载入与刷新 ----------

    /// <summary>首次载入（页面 Loaded 时调用一次）。</summary>
    public void Load()
    {
        _favorites = LinkStore.LoadFavorites();
        ApplyModels(LinkStore.Load());
        IsLoaded = true;
        Refresh();
    }

    /// <summary>丢弃内存里的改动，重新从磁盘读取（用户在外面手改了 Links.json 时用）。</summary>
    public void ReloadFromDisk()
    {
        _favorites = LinkStore.LoadFavorites();
        ApplyModels(LinkStore.Load());
        Refresh();
    }

    private void ApplyModels(List<LinkInfo> models)
    {
        _all.Clear();
        foreach (var model in models)
        {
            _all.Add(new LinkItemViewModel(model, _favorites.Contains(model.Url ?? string.Empty)));
        }

        // 删掉的链接要顺手清掉它的收藏记录，否则设置文件里会一直留着无效条目。
        _favorites = LinkStore.PruneFavorites(_favorites, models);
        RebuildCategoryOptions();
    }

    /// <summary>按当前搜索/筛选条件重建分组。</summary>
    public void Refresh()
    {
        IEnumerable<LinkItemViewModel> query = _all;

        if (_favoritesOnly)
        {
            query = query.Where(p => p.IsFavorite);
        }

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var keyword = _searchText.Trim().ToLowerInvariant();
            query = query.Where(p => p.SearchText.Contains(keyword));
        }

        var categoryKey = SelectedCategoryKey;
        if (categoryKey is not null)
        {
            query = query.Where(p => p.CategoryKey == categoryKey);
        }

        var visible = query.ToList();
        Groups.Clear();

        if (_favoritesOnly)
        {
            // 此模式下每一条都是收藏，再分「收藏 + 分类」只会重复，直接平铺一组。
            if (visible.Count > 0)
            {
                Groups.Add(CreateGroup(FindString("LinksPage_FavoriteGroup"), visible, isFavoriteGroup: true));
            }
        }
        else
        {
            // 收藏项**同时**留在原分类里（用户要求：支持"一条链接出现在多个分组"）：
            // 「收藏」组是"置顶常用"的视图，不是把条目从分类里搬走——否则收藏多了以后原分类会"缺人"，
            // 想按分类看全貌时信息不完整。代价是同一张卡可能出现两次（带星标，不会误认为重复数据），
            // 所以统计口径要走去重（见 VisibleCount）。
            var favorites = visible.Where(p => p.IsFavorite).ToList();
            if (favorites.Count > 0)
            {
                Groups.Add(CreateGroup(FindString("LinksPage_FavoriteGroup"), favorites, isFavoriteGroup: true));
            }

            // 顺序按 `_all` 里的条数从多到少（"常用的几组放前面"），**不按当前筛选结果的条数**——
            // 否则每敲一个搜索字，分组顺序都在跳。GroupBy 保持源顺序、OrderBy 是稳定排序，
            // 所以条数相同的分组仍维持 `Links.json` 里的先后。
            foreach (var group in visible
                         .GroupBy(p => p.CategoryKey)
                         .OrderBy(p => Rank(p.Key)))
            {
                var items = group.ToList();
                var title = group.Key.Length == 0
                    ? FindString("LinksPage_Uncategorized")
                    : items[0].CategoryDisplay ?? FindString("LinksPage_Uncategorized");
                Groups.Add(CreateGroup(title, items));
            }
        }

        _visibleCount = Groups.SelectMany(p => p.Items).Distinct().Count();

        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(IsSourceEmpty));
        OnPropertyChanged(nameof(IsFilteredEmpty));
        OnPropertyChanged(nameof(HasJumpTargets));
    }

    /// <summary>按 <see cref="_groupOrder"/> 取分组序号；表里没有的排到最后。</summary>
    private int Rank(string categoryKey)
        => _groupOrder.TryGetValue(categoryKey, out var rank) ? rank : int.MaxValue;

    private LinkGroupViewModel CreateGroup(string title, IReadOnlyList<LinkItemViewModel> items, bool isFavoriteGroup = false)
    {
        var group = new LinkGroupViewModel(title, items, isFavoriteGroup, isExpanded: true, OnGroupExpandedChanged);
        // 用户折叠过的分组，重建后保持折叠。
        group.IsExpanded = !_collapsedGroups.Contains(group.GroupKey);
        return group;
    }

    private void OnGroupExpandedChanged(string groupKey, bool expanded)
    {
        if (expanded)
        {
            _collapsedGroups.Remove(groupKey);
        }
        else
        {
            _collapsedGroups.Add(groupKey);
        }
    }

    private string? SelectedCategoryKey
        => _selectedCategoryIndex > 0 && _selectedCategoryIndex < _categoryKeys.Count
            ? _categoryKeys[_selectedCategoryIndex]
            : null;

    /// <summary>
    /// 重建分类下拉项。原选中项按索引保留——清空 <see cref="CategoryOptions"/> 时 ComboBox 会把
    /// SelectedIndex 回写成 -1，若直接接受，用户的筛选条件会在"新增链接 / 切换语言"后莫名丢失。
    /// </summary>
    private void RebuildCategoryOptions()
    {
        var previous = _selectedCategoryIndex;

        _rebuildingCategories = true;
        try
        {
            CategoryOptions.Clear();
            _categoryKeys.Clear();
            _groupOrder.Clear();

            CategoryOptions.Add(FindString("LinksPage_AllCategories"));
            _categoryKeys.Add(null);

            // 下拉项与页面上的分组用**同一套顺序**（条数从多到少），免得"列表里排第 3、页面上翻到第 8"。
            // 未分类放在最后（它通常只有零星几条）。
            var ordered = _all
                .Where(p => p.CategoryKey.Length > 0)
                .GroupBy(p => p.CategoryKey)
                .OrderByDescending(g => g.Count())
                .Select(g => (Key: g.Key, Display: g.First().CategoryDisplay ?? g.Key))
                .ToList();

            for (var index = 0; index < ordered.Count; index++)
            {
                CategoryOptions.Add(ordered[index].Display);
                _categoryKeys.Add(ordered[index].Key);
                _groupOrder[ordered[index].Key] = index;
            }

            if (_all.Any(p => p.CategoryKey.Length == 0))
            {
                _groupOrder[string.Empty] = ordered.Count;
                CategoryOptions.Add(FindString("LinksPage_Uncategorized"));
                _categoryKeys.Add(string.Empty);
            }
        }
        finally
        {
            _rebuildingCategories = false;
        }

        // 分类被删空的场景下索引会越界，回落到「全部」。
        _selectedCategoryIndex = previous >= 0 && previous < CategoryOptions.Count ? previous : 0;
        OnPropertyChanged(nameof(SelectedCategoryIndex));
    }

    // ---------- 增删改 ----------

    /// <summary>新增一条并落盘。</summary>
    public void Add(LinkInfo link)
    {
        _all.Add(new LinkItemViewModel(link, false));
        SaveAll();

        // 新链接可能带来新分类，下拉框要跟着长（原选中项由 RebuildCategoryOptions 保留）。
        RebuildCategoryOptions();
        Refresh();
    }

    /// <summary>编辑对话框改的是同一个模型实例，这里只负责落盘与刷新界面。</summary>
    public void NotifyEdited(LinkItemViewModel item)
    {
        item.Refresh();
        SaveAll();
        Refresh();
    }

    public void Remove(LinkItemViewModel item)
    {
        _all.Remove(item);
        _favorites.Remove(item.Url);
        LinkStore.SaveFavorites(_favorites);
        SaveAll();
        RebuildCategoryOptions();
        Refresh();
    }

    public void ToggleFavorite(LinkItemViewModel item)
    {
        var value = !item.IsFavorite;
        item.SetFavorite(value);

        if (value)
        {
            _favorites.Add(item.Url);
        }
        else
        {
            _favorites.Remove(item.Url);
        }

        LinkStore.SaveFavorites(_favorites);
        Refresh();
    }

    public void SaveAll()
        => LinkStore.Save(_all.Select(p => p.Model).ToList());

    // ---------- 导入 / 导出 / 恢复默认 ----------

    /// <summary>按 URL 合并导入；返回新增条数。</summary>
    /// <remarks>
    /// 顺序不能颠倒：<see cref="LinkStore.Merge"/> 是把新条目加进传入的那个列表里的，
    /// 必须先 <see cref="ApplyModels"/> 让 <c>_all</c> 指向合并后的结果、再 <see cref="SaveAll"/>，
    /// 否则落盘的仍是合并前的旧列表——界面上看得到新条目、重启后却全部消失（离屏探针实测抓到过）。
    /// </remarks>
    public int Import(IEnumerable<LinkInfo> incoming)
    {
        var models = _all.Select(p => p.Model).ToList();
        var added = LinkStore.Merge(models, incoming);
        if (added > 0)
        {
            ApplyModels(models);
            SaveAll();
            Refresh();
        }

        return added;
    }

    /// <summary>把默认表里"当前还没有的"补齐；返回新增条数。</summary>
    public int RestoreDefaults() => Import(LinkStore.LoadDefaults());

    public IReadOnlyList<LinkInfo> CurrentModels() => _all.Select(p => p.Model).ToList();

    /// <summary>
    /// 库里**已存在**的分类名（按分组顺序，即条数从多到少），给编辑对话框的"点一下加入"胶囊用。
    /// 不含"未分类"（它是分组时的兜底显示名，不是真实分类，写进链接会把兜底名固化成一个新分类）。
    /// </summary>
    public IReadOnlyList<string> KnownCategories => _all
        .Where(p => p.CategoryKey.Length > 0 && !string.IsNullOrWhiteSpace(p.CategoryDisplay))
        .GroupBy(p => p.CategoryKey)
        .OrderBy(p => Rank(p.Key))
        .Select(p => p.First().CategoryDisplay!)
        .ToList();

    // ---------- 语言 ----------

    private void OnLanguageChanged(object? sender, string language)
    {
        // 分类显示名来自数据（不随语言变），只有「收藏 / 未分类 / 全部分类」与统计文案要重算；
        // 选中项由 RebuildCategoryOptions 按索引保留。
        RebuildCategoryOptions();
        Refresh();
    }

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
