using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.ViewModels;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 「攻略 · 异常」页：一张全宽对照表（异常 → 它升级成的站点）。
/// 点「可升级为」单元格或双击整行，都会跳「死亡远征」页并选中那个站点。
/// </summary>
public partial class AnomalyPage : Page
{
    private readonly AnomalyPageViewModel _viewModel = new();
    private bool _loaded;

    public AnomalyPage()
    {
        InitializeComponent();

        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 页面是常驻缓存的（NavigationCacheMode=Required），数据只查第一次。
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await _viewModel.InitAsync();
    }

    /// <summary>点异常自己的名字 → 打开这个异常的详细页。</summary>
    private void OnSelfClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AnomalyRow row })
        {
            AnomalyPageViewModel.OpenSelf(row);
        }
    }

    /// <summary>点「远征」列里的可点文字 → 打开它升级成的那个站点。</summary>
    private void OnTargetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AnomalyRow row })
        {
            AnomalyPageViewModel.OpenTarget(row);
        }
    }

    /// <summary>双击整行 → 与点异常名字等价（打开异常自己）。</summary>
    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { SelectedItem: AnomalyRow row })
        {
            AnomalyPageViewModel.OpenSelf(row);
        }
    }
}
