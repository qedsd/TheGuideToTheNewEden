using System.IO;
using System.Windows;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.EveSettings;
using TheGuideToTheNewEden.WPF.ViewModels.EveSettings;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 「配置同步」的设置弹窗：游戏设置目录 + 同步/还原前自动备份 + 游戏运行时警告。
/// <para>
/// 这三项原来常驻在页面顶部与底部，按用户要求收进一个设置按钮里；弹窗直接改页面 VM
/// （两个开关是 <c>Mode=TwoWay</c>，改完即落盘），关掉后由页面自己 <c>SyncSelection()</c>
/// 把服务器/配置文件列表的选中项对齐——因为换根目录会重建这两个列表。
/// </para>
/// </summary>
public partial class EveSettingsOptionWindow : FluentWindow
{
    private readonly EveSettingsSyncViewModel _viewModel;

    public EveSettingsOptionWindow(EveSettingsSyncViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnChangeFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = FindString("EveSettingsSyncPage_SelectRootTitle"),
                Multiselect = false,
            };

            if (Directory.Exists(_viewModel.RootPath))
            {
                dialog.InitialDirectory = _viewModel.RootPath;
            }

            if (dialog.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                return;
            }

            // 允许选到服务器目录或配置文件目录，这里统一上溯到根目录。
            await _viewModel.SetRootAsync(EveSettingsFolderService.ResolveRootFromSelection(dialog.FolderName));

            if (!_viewModel.HasRoot)
            {
                PageNotifyService.Warning(FindString("EveSettingsSyncPage_NoRoot"));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
    }

    private void OnOpenRootFolderClick(object sender, RoutedEventArgs e)
        => ShellHelper.OpenFolder(_viewModel.RootPath);

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}
