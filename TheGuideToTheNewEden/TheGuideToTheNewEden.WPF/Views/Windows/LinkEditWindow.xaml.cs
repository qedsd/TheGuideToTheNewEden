using System.Windows;
using TheGuideToTheNewEden.Core.Models;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 快速链接的新建/编辑对话框（对应 WinUI 版的 <c>EditLinkInfoDialog</c>）。
/// 编辑的是模型的**副本**，取消即丢弃；<c>IconUrl</c> 不在界面上（WPF 版不显示图标），
/// 但会原样保留，用户回到 WinUI 版仍能看到原来的图标。
/// </summary>
/// <remarks>
/// 分类的输入方式：文本框仍是**唯一数据源**（保存的就是那串逗号分隔文本，用户可以随时手改），
/// 下方的「已有分类」是**可点胶囊**——点一下加入、再点移出，避免"每个分组都得手打、还容易打错成新分组"。
/// 胶囊状态不单独存，而是每次从文本框内容重算并重建，所以不存在"文本与勾选不同步"的问题。
/// </remarks>
public partial class LinkEditWindow : FluentWindow
{
    private readonly LinkInfo _link;
    private readonly IReadOnlyList<string> _knownCategories;

    /// <summary>正在按程序改写文本框（重建胶囊）——期间忽略 TextChanged，避免自触发。</summary>
    private bool _syncingCategories;

    public LinkEditWindow(LinkInfo? link, IEnumerable<string> knownCategories)
    {
        _link = link is null ? new LinkInfo() : Copy(link);
        _knownCategories = (knownCategories ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        InitializeComponent();

        var title = FindString(link is null ? "LinksPage_Add" : "LinksPage_Edit");
        TitleBar.Title = title;
        Title = title;

        NameBox.Text = _link.Name;
        UrlBox.Text = _link.Url;
        ShortDescriptionBox.Text = _link.ShortDescription;
        DescriptionBox.Text = _link.Description;
        CategoriesBox.Text = _link.GetCategories(',');
        PlatformsBox.Text = _link.GetPlatforms(',');
        LangsBox.Text = _link.GetLangs(',');

        RebuildCategoryChips();
        CategoriesPickTip.Visibility = _knownCategories.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        NameBox.Focus();
    }

    /// <summary>确定后的结果（新建时为新建的模型；编辑时为编辑后的副本）。取消时为 null。</summary>
    public LinkInfo? Result { get; private set; }

    // ---------- 分类胶囊 ----------

    private void OnCategoriesTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_syncingCategories)
        {
            RebuildCategoryChips();
        }
    }

    private void OnCategoryChipClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not CategoryChoice choice)
        {
            return;
        }

        var selected = ParseCategories(CategoriesBox.Text).ToList();
        var index = selected.FindIndex(p => string.Equals(p, choice.Name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            selected.RemoveAt(index);
        }
        else
        {
            selected.Add(choice.Name);
        }

        WriteCategories(selected);
        CategoriesBox.CaretIndex = CategoriesBox.Text.Length;
    }

    /// <summary>按当前文本框内容重算每个已有分类的选中态，并重建胶囊行。</summary>
    private void RebuildCategoryChips()
    {
        if (_knownCategories.Count == 0)
        {
            CategoryChips.Visibility = Visibility.Collapsed;
            return;
        }

        var selected = ParseCategories(CategoriesBox.Text);
        CategoryChips.ItemsSource = _knownCategories
            .Select(p => new CategoryChoice(p, selected.Contains(p, StringComparer.OrdinalIgnoreCase)))
            .ToList();
        CategoryChips.Visibility = Visibility.Visible;
    }

    /// <summary>把选中集合写回文本框（保持界面顺序 = 已有分类的展示顺序，最后接上手打的其它分类）。</summary>
    private void WriteCategories(IReadOnlyList<string> selected)
    {
        _syncingCategories = true;
        try
        {
            CategoriesBox.Text = string.Join(',', selected);
        }
        finally
        {
            _syncingCategories = false;
        }

        RebuildCategoryChips();
    }

    /// <summary>按逗号/中文逗号/顿号切分（与保存时的写法一致），去空白、去重。</summary>
    private static List<string> ParseCategories(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var result = new List<string>();
        foreach (var part in text.Split([',', '，', '、'], StringSplitOptions.RemoveEmptyEntries))
        {
            var name = part.Trim();
            if (name.Length > 0 && !result.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(name);
            }
        }

        return result;
    }

    /// <summary>胶囊的数据项（重建式刷新，所以不需要 INotifyPropertyChanged）。</summary>
    public sealed record CategoryChoice(string Name, bool Selected);

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim() ?? string.Empty;
        var url = UrlBox.Text?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            ShowError("LinksPage_ErrorNameRequired");
            return;
        }

        if (url.Length == 0)
        {
            ShowError("LinksPage_ErrorUrlRequired");
            return;
        }

        // 只认协议前缀，不要求地址能被 Uri 完整解析：库里存的是站点地址，个别站点带特殊字符，
        // 用 Uri.TryCreate 反而可能把合法地址误判掉。
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            ShowError("LinksPage_ErrorUrlInvalid");
            return;
        }

        _link.Name = name;
        _link.Url = url;
        _link.ShortDescription = TextOrNull(ShortDescriptionBox.Text);
        _link.Description = TextOrNull(DescriptionBox.Text);
        // 用与胶囊同一套切分/去重逻辑，保证"界面看到的"和"存进去的"一致。
        var categories = ParseCategories(CategoriesBox.Text);
        _link.Categories = categories.Count > 0 ? categories.ToArray() : null;
        _link.Platforms = SplitOrNull(PlatformsBox.Text);
        _link.Langs = SplitOrNull(LangsBox.Text);

        Result = _link;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string key)
    {
        ErrorText.Text = FindString(key);
        ErrorText.Visibility = Visibility.Visible;
    }

    private static string? TextOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[]? SplitOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value
            .Split([',', '，', '、'], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();

        return parts.Length == 0 ? null : parts;
    }

    private static LinkInfo Copy(LinkInfo source) => new()
    {
        Name = source.Name,
        Url = source.Url,
        ShortDescription = source.ShortDescription,
        Description = source.Description,
        Langs = source.Langs,
        Platforms = source.Platforms,
        Categories = source.Categories,
        IconUrl = source.IconUrl,
    };

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}
