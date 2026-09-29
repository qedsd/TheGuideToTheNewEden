using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WPF.ViewModels.Links;

/// <summary>
/// 单条快速链接的展示模型：把 <see cref="LinkInfo"/> 里"界面要用"的部分算好（标签、
/// 分类分组键、搜索用文本），原模型原样保留给编辑对话框写回。
/// </summary>
/// <remarks>
/// **卡片不放任何图标**（用户要求，阶段 77 定稿）：条目自带的 `IconUrl` 图片图标不用（本地图标目录没链接进本项目、
/// 网络图标又要引入异步图片控件与缓存），"按分类取 Fluent 图标"的方案也已弃用（要求新增分类就得补映射，
/// 用户自建分类会全落到兜底图标）；<c>IconUrl</c> 字段照旧保留、编辑时不覆盖，用户回到 WinUI 版仍能看到原来的图标。
/// </remarks>
public sealed class LinkItemViewModel : INotifyPropertyChanged
{
    private bool _isFavorite;
    private IReadOnlyList<string> _chips = [];
    private string _categoryKey = string.Empty;
    private string? _categoryDisplay;
    private string _searchText = string.Empty;

    public LinkItemViewModel(LinkInfo model, bool isFavorite)
    {
        Model = model;
        _isFavorite = isFavorite;
        Recompute();
    }

    public LinkInfo Model { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name => Model.Name ?? string.Empty;

    public string Url => Model.Url ?? string.Empty;

    public string ShortDescription => Model.ShortDescription ?? string.Empty;

    public string Description => Model.Description ?? string.Empty;

    /// <summary>分类 + 平台的短标签（分类在前），卡片底部一排小胶囊。</summary>
    public IReadOnlyList<string> Chips => _chips;

    /// <summary>分组用的分类键（小写；空串表示未分类）。</summary>
    public string CategoryKey => _categoryKey;

    /// <summary>分类显示名（按原始大小写，如默认表里的 "kb"）。</summary>
    public string? CategoryDisplay => _categoryDisplay;

    public bool IsFavorite => _isFavorite;

    public string SearchText => _searchText;

    public void SetFavorite(bool value)
    {
        if (_isFavorite == value)
        {
            return;
        }

        _isFavorite = value;
        OnPropertyChanged(nameof(IsFavorite));
    }

    /// <summary>编辑对话框改的是同一个 <see cref="LinkInfo"/> 实例，保存后让界面重读一遍。</summary>
    public void Refresh()
    {
        // 分类可能被改过：胶囊、分组键、搜索文本都必须跟着重算，否则卡片会留在旧分组里。
        Recompute();

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Url));
        OnPropertyChanged(nameof(ShortDescription));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Chips));
        OnPropertyChanged(nameof(CategoryKey));
        OnPropertyChanged(nameof(CategoryDisplay));
        OnPropertyChanged(nameof(SearchText));
    }

    private void Recompute()
    {
        _chips = BuildChips(Model);

        var category = (Model.Categories ?? [])
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
        _categoryDisplay = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        _categoryKey = _categoryDisplay is null ? string.Empty : _categoryDisplay.ToLowerInvariant();

        _searchText = string.Join('\n',
            Model.Name,
            Model.ShortDescription,
            Model.Description,
            Model.Url,
            string.Join(' ', Model.Categories ?? []),
            string.Join(' ', Model.Platforms ?? []),
            string.Join(' ', Model.Langs ?? [])).ToLowerInvariant();
    }

    private static IReadOnlyList<string> BuildChips(LinkInfo model)
    {
        var chips = new List<string>();
        foreach (var category in model.Categories ?? [])
        {
            if (!string.IsNullOrWhiteSpace(category))
            {
                chips.Add(category.Trim());
            }
        }

        foreach (var platform in model.Platforms ?? [])
        {
            if (!string.IsNullOrWhiteSpace(platform))
            {
                chips.Add(platform.Trim());
            }
        }

        return chips;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
