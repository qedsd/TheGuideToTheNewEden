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
/// <para>
/// 带 <c>#节</c> 的跳转（AI 回答里的 <c>manual://market#errors</c>）只渲染那一节，
/// 顶部出现「返回全文」；界面语言没有对应手册时顶部给一条"已回退中文"的提示（不静默）。
/// </para>
/// </summary>
public partial class ManualBrowserView : UserControl
{
    /// <summary>列表项。</summary>
    public sealed record ItemModel(string Id, string Title, string Group);

    private bool _suppressSelection;

    /// <summary>下一次 <see cref="Render"/> 要定位到的节 slug（由 <see cref="NavigateTo"/> 传入）。</summary>
    private string? _pendingSection;

    /// <summary>当前正文的 Markdown 原文（「复制」按钮用它；看单节时是那一节）。</summary>
    private string? _currentMarkdown;

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

    /// <summary>当前展示的手册 id（诊断/探针用；列表还没内容时为 <c>null</c>）。</summary>
    public string? CurrentManualId => (ManualList.SelectedItem as ItemModel)?.Id;

    /// <summary>
    /// 跳到指定手册（窗口复用时由 <c>ManualWindow.Show(id)</c> 调用）。
    /// 接受 <c>id</c> / <c>id#节</c> / <c>manual:id</c> / <c>manual://id#节</c> 四种写法；找不到时正文给提示。
    /// </summary>
    public void NavigateTo(string? manualId)
    {
        var key = (manualId ?? string.Empty).Trim();
        if (key.StartsWith("manual:", StringComparison.OrdinalIgnoreCase))
        {
            key = key["manual:".Length..];
        }

        key = key.Replace('\\', '/').TrimStart('/');

        var hash = key.IndexOf('#');
        string? section = null;
        if (hash >= 0)
        {
            section = key[(hash + 1)..].Trim();
            key = key[..hash];
        }

        if (key.Length == 0)
        {
            return;
        }

        // 窗口刚建、`Loaded`（→ RefreshList）还没跑时列表是空的：先填一次，
        // 否则 F1 首次打开窗口会走到下面的 IndexOfId < 0 分支，显示"找不到说明书"。
        if (ManualList.ItemsSource is null)
        {
            RefreshList();
        }

        var index = IndexOfId(key);
        if (index < 0)
        {
            _pendingSection = null;
            ShowMissing(key);
            return;
        }

        _pendingSection = section;
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

        UpdateFallbackHint();

        if (items.Count > 0)
        {
            ManualList.SelectedIndex = 0;
        }
        else
        {
            ManualTitle.Text = string.Empty;
            ManualSubTitle.Text = string.Empty;
            BackToFullButton.Visibility = Visibility.Collapsed;
            BodyPresenter.Markdown = Resolve("Manual.Search.Empty");
        }
    }

    /// <summary>界面语言没有对应手册时（内容回退到 zh-CN）在标题下方提示一行，不静默。</summary>
    private void UpdateFallbackHint()
    {
        var fallback = ManualService.IsFallback;
        FallbackHint.Visibility = fallback ? Visibility.Visible : Visibility.Collapsed;
        if (fallback)
        {
            FallbackHint.Text = string.Format(Resolve("Manual.FallbackToZh"), ManualService.SourceLanguage);
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => RefreshList();

    private void OnManualSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
        {
            return;
        }

        _pendingSection = null; // 手动点列表 = 看整篇
        Render(ManualList.SelectedItem as ItemModel);
    }

    /// <summary>「返回全文」：从"只渲染某一节"回到整篇。</summary>
    private void OnBackToFullClick(object sender, RoutedEventArgs e)
    {
        _pendingSection = null;
        Render(ManualList.SelectedItem as ItemModel);
    }

    /// <summary>「在助手中提问」：把当前这篇的标题带进助手（阶段 110 的 handoff，会新开一个对话）。</summary>
    private void OnAskAssistantClick(object sender, RoutedEventArgs e)
    {
        if (ManualList.SelectedItem is not ItemModel item)
        {
            return;
        }

        Navigation.NavigateToAssistant($"《{item.Title}》怎么用？");
    }

    /// <summary>「复制」：把当前展示的 Markdown 原文放进剪贴板（方便贴给助手或存到别处）。</summary>
    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (_currentMarkdown is not { Length: > 0 } markdown)
        {
            return;
        }

        try
        {
            Clipboard.SetText(markdown);
            PageNotifyService.Success(Resolve("Manual.Copied"));
        }
        catch (Exception ex)
        {
            // 剪贴板被别的进程占用时 WPF 会抛 COM 异常，别让它冒到全局处理器
            Core.Log.Warn($"复制说明书失败：{ex.Message}");
            PageNotifyService.Error(Resolve("Manual.CopyFailed"));
        }
    }

    /// <summary>「打开文件位置」：在资源管理器里定位这篇的 .md（用户可自己改，改完重启即生效）。</summary>
    private void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        if (ManualList.SelectedItem is not ItemModel item || ManualService.GetFilePath(item.Id) is not { } file)
        {
            return;
        }

        Helpers.ShellHelper.RevealInFolder(file);
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

        var sectionSlug = _pendingSection;
        _pendingSection = null;

        var subTitle = new List<string> { document.Id };
        if (document.Updated is { Length: > 0 } updated)
        {
            subTitle.Add(updated);
        }

        if (sectionSlug is { Length: > 0 } && document.FindSection(sectionSlug) is { } section)
        {
            // 只看一节：标题带上节名，正文只给这一节；顶部出现「返回全文」。
            ManualTitle.Text = $"{document.Title} · {section.Title}";
            subTitle.Insert(0, $"#{section.Slug} · {section.Content.Length} 字");
            ManualSubTitle.Text = string.Join(" · ", subTitle);
            BackToFullButton.Visibility = Visibility.Visible;
            _currentMarkdown = $"## {section.Title}\n\n{section.Content}";
            BodyPresenter.Markdown = _currentMarkdown;
            return;
        }

        ManualTitle.Text = document.Title;
        ManualSubTitle.Text = string.Join(" · ", subTitle);
        BackToFullButton.Visibility = Visibility.Collapsed;
        _currentMarkdown = document.Body;
        BodyPresenter.Markdown = _currentMarkdown;
    }

    private void ShowMissing(string id)
    {
        ManualTitle.Text = id;
        ManualSubTitle.Text = string.Empty;
        BackToFullButton.Visibility = Visibility.Collapsed;
        _currentMarkdown = null;
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
