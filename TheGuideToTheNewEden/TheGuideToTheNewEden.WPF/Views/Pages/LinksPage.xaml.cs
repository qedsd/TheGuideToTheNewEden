using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Links;
using TheGuideToTheNewEden.WPF.ViewModels.Links;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 快速链接页：工具栏（搜索 / 分类 / 只看收藏 / 新建 / 更多）+ 按分类分组的卡片网格。
/// 对应 WinUI 版 <c>LinksPage</c>，原有操作（打开/复制/编辑/删除/新建）都在卡片右键菜单里，功能有增无减。
/// </summary>
public partial class LinksPage : Page
{
    private readonly LinksPageViewModel _viewModel = new();
    private bool _loaded;

    /// <summary>重建分类下拉项时抑制 SelectionChanged 回写，避免把筛选条件冲掉。</summary>
    private bool _syncingCategory;

    public LinksPage()
    {
        InitializeComponent();

        DataContext = _viewModel;
        _viewModel.PropertyChanged += (_, _) =>
        {
            SyncCategoryCombo();
            UpdateEmptyStates();
        };

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 页面是常驻缓存的，只有第一次进来时读盘。
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        _viewModel.Load();
        SyncCategoryCombo();
        UpdateEmptyStates();
    }

    private void UpdateEmptyStates()
    {
        EmptySourceState.Visibility = _viewModel.IsSourceEmpty ? Visibility.Visible : Visibility.Collapsed;
        EmptyFilterState.Visibility = _viewModel.IsFilteredEmpty ? Visibility.Visible : Visibility.Collapsed;
        ContentScroll.Visibility = _viewModel.Groups.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        JumpStrip.Visibility = _viewModel.HasJumpTargets ? Visibility.Visible : Visibility.Collapsed;

        // 数据/筛选变了就把"跳转补白"收回，免得留下上一次跳转撑出来的空白。
        BottomSpacer.Height = 0;
    }

    /// <summary>
    /// 「分类快捷跳转」：展开该分组，并把它对齐到**滚动区顶部**。
    /// </summary>
    /// <remarks>
    /// 不能用 <c>BringIntoView()</c>：它按"最小滚动量"工作——目标在视口下方时只把它的**底边**露出来，
    /// 观感就是"点了跳转，分组却跑到最下面"（用户实测反馈）。这里直接算出目标在内容坐标系里的位置再
    /// <c>ScrollToVerticalOffset</c>，正负两个方向都对齐到顶。
    /// 另外末尾几个分组下面没有足够内容、滚不到顶部，所以跳转前先按视口高度补一段高度（<see cref="BottomSpacer"/>）。
    /// 展开只推动目标**下面**的内容，目标自己的位置不受影响，因此不必等布局，先展开再算坐标即可。
    /// </remarks>
    private void OnJumpClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not LinkGroupViewModel group)
        {
            return;
        }

        group.IsExpanded = true;

        if (GroupList.ItemContainerGenerator.ContainerFromItem(group) is not FrameworkElement container)
        {
            return;
        }

        // 补白：让"目标标题贴顶"在靠后的分组上也做得到。
        BottomSpacer.Height = Math.Max(0, ContentScroll.ViewportHeight - container.ActualHeight - 8);
        ContentScroll.UpdateLayout();

        // 容器坐标 → 内容坐标（视口坐标 + 当前偏移），再滚到该位置即为"标题贴顶"。
        var topInContent = container.TransformToAncestor(ContentScroll).Transform(new Point(0, 0)).Y
                           + ContentScroll.VerticalOffset;
        ContentScroll.ScrollToVerticalOffset(topInContent);
    }

    private void SyncCategoryCombo()
    {
        var index = _viewModel.SelectedCategoryIndex;
        if (CategoryCombo.SelectedIndex == index)
        {
            return;
        }

        _syncingCategory = true;
        try
        {
            CategoryCombo.SelectedIndex = index;
        }
        finally
        {
            _syncingCategory = false;
        }
    }

    // ---------- 工具栏 ----------

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        => _viewModel.SearchText = SearchBox.Text;

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncingCategory)
        {
            _viewModel.SelectedCategoryIndex = CategoryCombo.SelectedIndex;
        }
    }

    private void OnFavoriteFilterClick(object sender, RoutedEventArgs e)
    {
        _viewModel.FavoritesOnly = !_viewModel.FavoritesOnly;
        ApplyFavoriteFilterVisual();
        UpdateEmptyStates();
    }

    /// <summary>
    /// 用 <c>Appearance</c> 表达"筛选已开启"。WPF-UI 4.3 没有可用的 <c>ui:ToggleButton</c>，
    /// 这里沿用 ToolWindow 置顶按钮的写法（图标 + 外观切换）。
    /// </summary>
    private void ApplyFavoriteFilterVisual()
    {
        // 不用 using Wpf.Ui.Controls：它也有 MessageBox/MessageBoxButton，会和 System.Windows 撞名。
        FavoriteFilterButton.Appearance = _viewModel.FavoritesOnly
            ? Wpf.Ui.Controls.ControlAppearance.Secondary
            : Wpf.Ui.Controls.ControlAppearance.Transparent;
        FavoriteFilterIcon.Symbol = _viewModel.FavoritesOnly
            ? Wpf.Ui.Controls.SymbolRegular.StarEmphasis24
            : Wpf.Ui.Controls.SymbolRegular.Star24;
    }

    private void OnClearFilterClick(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        _viewModel.SearchText = string.Empty;
        _viewModel.FavoritesOnly = false;
        _viewModel.SelectedCategoryIndex = 0;
        ApplyFavoriteFilterVisual();
        SyncCategoryCombo();
        UpdateEmptyStates();
    }

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { ContextMenu: { } menu } element)
        {
            menu.PlacementTarget = element;
            menu.IsOpen = true;
        }
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var window = new LinkEditWindow(null, KnownCategories())
        {
            Owner = Window.GetWindow(this),
        };

        if (window.ShowDialog() == true && window.Result is not null)
        {
            _viewModel.Add(window.Result);
            PageNotifyService.Success(FindString("General_SaveSuccess"));
        }
    }

    // ---------- 更多菜单 ----------

    private void OnReloadClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ReloadFromDisk();
        SyncCategoryCombo();
        UpdateEmptyStates();
        PageNotifyService.Success(FindString("LinksPage_ReloadDone"));
    }

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = FindString("LinksPage_Import"),
            Filter = FindString("LinksPage_JsonFilter"),
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        try
        {
            var imported = LinkStore.ImportFile(dialog.FileName);
            if (imported.Count == 0)
            {
                PageNotifyService.Error(FindString("LinksPage_ImportEmpty"));
                return;
            }

            // 导入按 URL 合并：用户自己的收藏与手工链接不会被覆盖。
            var added = _viewModel.Import(imported);
            SyncCategoryCombo();
            PageNotifyService.Success(string.Format(FindString("LinksPage_ImportDone"), added));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(string.Format(FindString("LinksPage_ImportFailed"), ex.Message));
        }
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = FindString("LinksPage_Export"),
            FileName = "Links.json",
            Filter = FindString("LinksPage_JsonFilter"),
            AddExtension = true,
            DefaultExt = ".json",
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        try
        {
            LinkStore.ExportFile(dialog.FileName, _viewModel.CurrentModels());
            PageNotifyService.Success(FindString("LinksPage_ExportDone"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(string.Format(FindString("LinksPage_ExportFailed"), ex.Message));
        }
    }

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            FindString("LinksPage_RestoreDefaultsConfirm"),
            FindString("LinksPage_RestoreDefaults"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK)
        {
            return;
        }

        var added = _viewModel.RestoreDefaults();
        SyncCategoryCombo();
        PageNotifyService.Success(string.Format(FindString("LinksPage_RestoreDefaultsDone"), added));
    }

    private void OnOpenConfigFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.GetDirectoryName(LinkStore.FilePath);
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    // ---------- 卡片：打开 / 复制 / 收藏 / 编辑 / 删除 ----------

    private void OnCardClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is { } item)
        {
            OpenBrowser(item.Url);
        }
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is { } item)
        {
            OpenBrowser(item.Url);
        }
    }

    private void OnCopyUrlClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item)
        {
            return;
        }

        try
        {
            Clipboard.SetDataObject(item.Url, copy: true);
            PageNotifyService.Success(FindString("LinksPage_CopyDone"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(FindString("LinksPage_CopyFailed"));
        }
    }

    private void OnFavoriteClick(object sender, RoutedEventArgs e)
    {
        // 阻止冒泡到整卡（否则点收藏会顺带打开链接）
        e.Handled = true;

        if (GetItem(sender) is { } item)
        {
            _viewModel.ToggleFavorite(item);
        }
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item)
        {
            return;
        }

        var window = new LinkEditWindow(item.Model, KnownCategories())
        {
            Owner = Window.GetWindow(this),
        };

        if (window.ShowDialog() == true)
        {
            _viewModel.NotifyEdited(item);
            PageNotifyService.Success(FindString("General_SaveSuccess"));
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item)
        {
            return;
        }

        var confirm = MessageBox.Show(
            string.Format(FindString("LinksPage_RemoveConfirm"), item.Name),
            FindString("LinksPage_Remove"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.OK)
        {
            return;
        }

        _viewModel.Remove(item);
        PageNotifyService.Success(FindString("General_RemoveSuccess"));
    }

    /// <summary>右键菜单项的 DataContext 来自放置目标（卡片），卡片自身的 Tag 也指向同一条数据。</summary>
    private static LinkItemViewModel? GetItem(object sender)
    {
        if (sender is not FrameworkElement element)
        {
            return null;
        }

        return element.DataContext as LinkItemViewModel ?? element.Tag as LinkItemViewModel;
    }

    /// <summary>编辑对话框里"已有分类"胶囊的数据源（库里真实存在的分类名，不含"未分类"这个兜底显示名）。</summary>
    private IEnumerable<string> KnownCategories() => _viewModel.KnownCategories;

    // ---------- 打开链接 ----------

    /// <summary>
    /// 直接用保存的地址在浏览器里打开。
    /// 这里**不做任何地址加工**（曾经有过一版"运行时占位符"机制，已按用户要求移除：
    /// 链接库存的是**站点**地址，站点内部的深度链接是站点自己的事，应用不该替它拼参数）。
    /// </summary>
    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
    }

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}
