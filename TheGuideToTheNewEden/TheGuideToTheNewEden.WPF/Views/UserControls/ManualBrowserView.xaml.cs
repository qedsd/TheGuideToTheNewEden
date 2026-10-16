using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Manual;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 说明书浏览视图（左列表 + 右正文）：宿主是 <see cref="Views.Windows.ManualWindow"/> 的工具窗口。
/// <para>
/// 数据全部来自 <see cref="ManualService"/>（语言切换 → 服务重建索引 → 这里刷新列表）；
/// 正文里的手册内链（<c>[邮件](characters/mail)</c>）由 MarkdownRenderer 转成 <c>manual:</c> 协议，
/// 最终回到 <see cref="Views.Windows.ManualWindow.Show"/>，在同一个窗口里跳转。
/// </para>
/// </summary>
public partial class ManualBrowserView : UserControl
{
    /// <summary>列表项。</summary>
    public sealed record ItemModel(string Id, string Title, string Group);

    private bool _suppressSelection;

    public ManualBrowserView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
        RefreshList();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => LanguageService.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, string language) => Dispatcher.Invoke(RefreshList);

    /// <summary>跳到指定手册（窗口复用时由 <c>ManualWindow.Show(id)</c> 调用）。找不到时正文给提示。</summary>
    public void NavigateTo(string? manualId)
    {
        var key = (manualId ?? string.Empty).Trim();
        var hash = key.IndexOf('#');
        if (hash >= 0)
        {
            key = key[..hash]; // 暂不支持节内滚动：锚点先剥掉，直接打开整篇
        }

        if (key.Length == 0)
        {
            return;
        }

        var index = IndexOfId(key);
        if (index < 0)
        {
            ShowMissing(key);
            return;
        }

        _suppressSelection = true;
        ManualList.SelectedIndex = index;
        _suppressSelection = false;
        ManualList.ScrollIntoView(ManualList.SelectedItem);
        Render(ManualList.SelectedItem as ItemModel);
    }

    private void RefreshList()
    {
        var documents = ManualService.All;
        var items = documents
            .Select(document => new ItemModel(document.Id, document.Title, document.Group))
            .ToList();

        var search = SearchBox.Text?.Trim() ?? string.Empty;
        if (search.Length > 0)
        {
            var hitIds = ManualService.Search(search, 30).Select(hit => hit.Id).ToHashSet(StringComparer.Ordinal);
            items = items.Where(item => hitIds.Contains(item.Id)).ToList();
        }

        _suppressSelection = true;
        ManualList.ItemsSource = items;
        _suppressSelection = false;

        if (items.Count > 0)
        {
            ManualList.SelectedIndex = 0;
        }
        else
        {
            ManualTitle.Text = string.Empty;
            ManualSubTitle.Text = string.Empty;
            BodyPresenter.Markdown = Resolve("Manual.Search.Empty");
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => RefreshList();

    private void OnManualSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
        {
            return;
        }

        Render(ManualList.SelectedItem as ItemModel);
    }

    private void Render(ItemModel? item)
    {
        if (item is null)
        {
            return;
        }

        var document = ManualService.Find(item.Id);
        if (document is null)
        {
            ShowMissing(item.Id);
            return;
        }

        ManualTitle.Text = document.Title;
        var parts = new List<string> { document.Id };
        if (document.Updated is { Length: > 0 } updated)
        {
            parts.Add(updated);
        }

        ManualSubTitle.Text = string.Join(" · ", parts);
        BodyPresenter.Markdown = document.Body;
    }

    private void ShowMissing(string id)
    {
        ManualTitle.Text = id;
        ManualSubTitle.Text = string.Empty;
        BodyPresenter.Markdown = string.Format(Resolve("Manual.NotFound"), id);
    }

    private int IndexOfId(string id)
    {
        if (ManualList.ItemsSource is not System.Collections.IEnumerable items)
        {
            return -1;
        }

        var index = 0;
        foreach (var item in items)
        {
            if (item is ItemModel model && string.Equals(model.Id, id, StringComparison.Ordinal))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static string Resolve(string key)
        => System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}
