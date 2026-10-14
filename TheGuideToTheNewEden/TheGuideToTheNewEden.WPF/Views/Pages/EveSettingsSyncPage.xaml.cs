using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Models.EveSettings;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.EveSettings;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 「配置同步」：EVE 客户端设置文件（<c>core_char_*.dat</c> / <c>core_user_*.dat</c>）的管理与同步。
/// <para>
/// 页面负责全部界面交互（确认框、"同步到…"目标选择窗、文件夹选择器、写操作前的游戏运行拦截），
/// 数据与文件操作在 <see cref="EveSettingsSyncViewModel"/> 与其服务里。
/// </para>
/// <para>
/// 参考实现：<c>mintnick/eve-settings-manager</c>（Electron 版）的 profile / settings / backup 三个 IPC 模块；
/// 有意差异见 REFACTORING 阶段 103。
/// </para>
/// </summary>
public partial class EveSettingsSyncPage : Page
{
    private readonly EveSettingsSyncViewModel _viewModel = new();

    private bool _initialized;

    /// <summary>上次"检测到游戏在运行"的结果；只用于判断要不要发全局通知（null = 还没检测过）。</summary>
    private bool? _lastGameRunning;

    /// <summary>程序化设置列表选中项时抑制 SelectionChanged（否则会触发一次多余的重新加载）。</summary>
    private bool _suppressSelection;

    public EveSettingsSyncPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _viewModel.RefreshGameState();
            NotifyGameRunning();

            if (!_initialized)
            {
                _initialized = true;
                await _viewModel.InitializeAsync();
            }

            SyncSelection();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>
    /// 「游戏正在运行」不再单独占一条警告条，改走软件全局通知（右下角）。
    /// <para>
    /// 只在<b>状态变化</b>时提示一次：页面是常驻实例（<c>NavigationCacheMode=Required</c>），
    /// 每次切回本页都会触发 <c>Loaded</c>，不做这个判断就会反复弹同一条通知。
    /// 写操作前的拦截与确认在 <see cref="ConfirmWrite"/> 里（那里会重新检测一次）。
    /// </para>
    /// </summary>
    private void NotifyGameRunning()
    {
        if (_lastGameRunning == _viewModel.IsGameRunning)
        {
            return;
        }

        _lastGameRunning = _viewModel.IsGameRunning;

        if (_viewModel.IsGameRunning)
        {
            PageNotifyService.Warning(_viewModel.RunningGameText);
        }
    }

    // ---------- 根目录 ----------

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.ReloadAsync();
            SyncSelection();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
    }

    /// <summary>
    /// 打开设置弹窗（游戏设置目录 / 同步前自动备份 / 游戏运行时警告）。
    /// <para>
    /// 弹窗直接改页面 VM；换根目录会重建服务器与配置文件列表，而这两个列表的选中项是
    /// <b>命令式</b>设置的（没有走绑定），所以关窗后必须 <see cref="SyncSelection"/> 同步回来。
    /// </para>
    /// </summary>
    private void OnOpenOptionsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new EveSettingsOptionWindow(_viewModel)
            {
                Owner = Window.GetWindow(this),
            };

            window.ShowDialog();
            SyncSelection();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
    }

    private void OnOpenProfileFolderClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedProfile is { } profile)
        {
            ShellHelper.OpenFolder(profile.Path);
        }
    }

    // ---------- 选择 ----------

    private async void OnServerSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
        {
            return;
        }

        try
        {
            await _viewModel.SelectServerAsync(ServerList.SelectedItem as EveSettingsServer);
            SyncSelection();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private async void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
        {
            return;
        }

        try
        {
            await _viewModel.SelectProfileAsync(ProfileBox.SelectedItem as EveSettingsProfile);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private void SyncSelection()
    {
        _suppressSelection = true;
        try
        {
            ServerList.SelectedItem = _viewModel.SelectedServer;
            ProfileBox.SelectedItem = _viewModel.SelectedProfile;
        }
        finally
        {
            _suppressSelection = false;
        }
    }

    // ---------- 配置文件管理 ----------

    private async void OnNewProfileClick(object sender, RoutedEventArgs e)
    {
        var name = AskProfileName(
            FindString("EveSettingsSyncPage_ProfileAddTitle"),
            FindString("EveSettingsSyncPage_ProfileNameLabel"),
            string.Empty,
            unchangedValue: null);

        if (name is null)
        {
            return;
        }

        await RunAsync(() => _viewModel.CreateProfileAsync(name));
        SyncSelection();
    }

    private async void OnDuplicateProfileClick(object sender, RoutedEventArgs e)
    {
        var current = _viewModel.SelectedProfile?.Name;
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var name = AskProfileName(
            FindString("EveSettingsSyncPage_ProfileDuplicateTitle"),
            FindString("EveSettingsSyncPage_ProfileNameLabel"),
            current + FindString("EveSettingsSyncPage_ProfileCopySuffix"),
            unchangedValue: null);

        if (name is null)
        {
            return;
        }

        await RunAsync(() => _viewModel.DuplicateProfileAsync(name));
        SyncSelection();
    }

    private async void OnRenameProfileClick(object sender, RoutedEventArgs e)
    {
        var current = _viewModel.SelectedProfile?.Name;
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        var name = AskProfileName(
            FindString("EveSettingsSyncPage_ProfileRenameTitle"),
            FindString("EveSettingsSyncPage_ProfileNameLabel"),
            current,
            unchangedValue: current);

        if (name is null || string.Equals(name, current, StringComparison.Ordinal))
        {
            return;
        }

        await RunAsync(() => _viewModel.RenameProfileAsync(name));
        SyncSelection();
    }

    private async void OnDeleteProfileClick(object sender, RoutedEventArgs e)
    {
        var profile = _viewModel.SelectedProfile;
        if (profile is null)
        {
            return;
        }

        if (_viewModel.Profiles.Count <= 1)
        {
            PageNotifyService.Warning(FindString("EveSettingsSyncPage_ProfileOnlyOne"));
            return;
        }

        var confirm = MessageBox.Show(
            string.Format(FindString("EveSettingsSyncPage_ProfileDeleteConfirmDetail"), profile.Name),
            FindString("EveSettingsSyncPage_ProfileDeleteConfirmTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes || !ConfirmWrite())
        {
            return;
        }

        await RunAsync(() => _viewModel.DeleteProfileAsync());
        SyncSelection();
    }

    /// <summary>弹出名称输入框；返回 null 表示取消。<paramref name="unchangedValue"/> 是"允许沿用"的旧名字。</summary>
    private string? AskProfileName(string title, string label, string initial, string? unchangedValue)
    {
        var window = new EveSettingsTextInputWindow(
            title,
            label,
            initial,
            FindString("EveSettingsSyncPage_ProfileNamePlaceholder"),
            value => string.Equals(value, unchangedValue, StringComparison.OrdinalIgnoreCase)
                ? null
                : DescribeNameError(_viewModel.ValidateProfileName(value)))
        {
            Owner = Window.GetWindow(this),
        };

        return window.ShowDialog() == true ? window.Value : null;
    }

    private static string? DescribeNameError(EveSettingsProfileNameError error) => error switch
    {
        EveSettingsProfileNameError.Empty => FindString("EveSettingsSyncPage_ProfileNameEmpty"),
        EveSettingsProfileNameError.InvalidChars => FindString("EveSettingsSyncPage_ProfileNameInvalid"),
        EveSettingsProfileNameError.Exists => FindString("EveSettingsSyncPage_ProfileNameDuplicate"),
        _ => null,
    };

    // ---------- 同步 ----------

    private async void OnSyncFileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: EveSettingsFileItem source })
            {
                return;
            }

            var targets = _viewModel.GetSyncTargets(source);
            if (targets.Count == 0)
            {
                PageNotifyService.Info(FindString("EveSettingsSyncPage_SyncNoTargets"));
                return;
            }

            var window = new EveSettingsSyncTargetWindow(source.DisplayName, targets)
            {
                Owner = Window.GetWindow(this),
            };

            if (window.ShowDialog() != true)
            {
                return;
            }

            var paths = window.SelectedPaths;
            if (paths.Count == 0)
            {
                PageNotifyService.Info(FindString("EveSettingsSyncPage_SyncNoTargets"));
                return;
            }

            if (!ConfirmWrite())
            {
                return;
            }

            var detail = _viewModel.AutoBackupBeforeSync
                ? string.Format(FindString("EveSettingsSyncPage_SyncConfirmDetailBackup"), paths.Count)
                : string.Format(FindString("EveSettingsSyncPage_SyncConfirmDetail"), paths.Count);

            var confirm = MessageBox.Show(
                detail + Environment.NewLine + Environment.NewLine + FindString("EveSettingsSyncPage_SyncConfirmBackupSuggest"),
                FindString("EveSettingsSyncPage_SyncConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            var result = await _viewModel.SyncAsync(source, paths);
            ShowFailures(result.Failures);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
    }

    // ---------- 备份 / 还原 ----------

    private async void OnBackupSelectedFilesClick(object sender, RoutedEventArgs e)
    {
        var grid = Tabs.SelectedIndex switch
        {
            0 => CharacterGrid,
            1 => AccountGrid,
            _ => UnrecognizedGrid,
        };

        var files = grid.SelectedItems.OfType<EveSettingsFileItem>().ToList();
        if (files.Count == 0)
        {
            PageNotifyService.Info(FindString("EveSettingsSyncPage_BackupSelectNothing"));
            return;
        }

        await _viewModel.BackupFilesAsync(files);
    }

    private async void OnBackupProfileClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedProfile is null || !ConfirmWrite())
        {
            return;
        }

        await _viewModel.BackupProfileAsync();
    }

    private void OnBackupSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = BackupGrid.SelectedItem is EveSettingsBackupItem;
        RestoreBackupButton.IsEnabled = hasSelection;
        DeleteBackupButton.IsEnabled = hasSelection;
        RevealBackupButton.IsEnabled = hasSelection;
    }

    private void OnRevealBackupClick(object sender, RoutedEventArgs e)
    {
        if (BackupGrid.SelectedItem is EveSettingsBackupItem backup)
        {
            ShellHelper.RevealInFolder(backup.Path);
        }
    }

    private async void OnRestoreBackupClick(object sender, RoutedEventArgs e)
    {
        if (BackupGrid.SelectedItem is not EveSettingsBackupItem backup)
        {
            return;
        }

        if (_viewModel.SelectedProfile is null)
        {
            PageNotifyService.Warning(FindString("EveSettingsSyncPage_NoProfiles"));
            return;
        }

        var detail = backup.IsFolder
            ? string.Format(FindString("EveSettingsSyncPage_RestoreFolderDetail"), backup.Name)
            : string.Format(FindString("EveSettingsSyncPage_RestoreFileDetail"), backup.SourceText);

        if (MessageBox.Show(
                detail,
                FindString("EveSettingsSyncPage_RestoreConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        if (!ConfirmWrite())
        {
            return;
        }

        await _viewModel.RestoreBackupAsync(backup);
    }

    private async void OnDeleteBackupClick(object sender, RoutedEventArgs e)
    {
        if (BackupGrid.SelectedItem is not EveSettingsBackupItem backup)
        {
            return;
        }

        if (MessageBox.Show(
                string.Format(FindString("EveSettingsSyncPage_DeleteBackupConfirmDetail"), backup.Name),
                FindString("EveSettingsSyncPage_DeleteBackupConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        await _viewModel.DeleteBackupAsync(backup);
    }

    // ---------- 写操作前的统一把关 ----------

    /// <summary>
    /// 任何"写游戏设置目录"的动作之前调用：检测游戏是否在运行。
    /// <para>
    /// EVE 客户端退出时会把内存里的设置写回 <c>.dat</c>，所以游戏运行期间写进去的东西会在退出时被覆盖；
    /// 开启「游戏运行时禁止写入」时默认拦截，用户仍可显式确认继续。
    /// </para>
    /// </summary>
    private bool ConfirmWrite()
    {
        _viewModel.RefreshGameState();
        if (!_viewModel.IsGameRunning)
        {
            return true;
        }

        if (!_viewModel.BlockWhenGameRunning)
        {
            MessageBox.Show(
                _viewModel.RunningGameText,
                FindString("EveSettingsSyncPage_GameRunningTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return true;
        }

        var text = _viewModel.RunningGameText + Environment.NewLine + Environment.NewLine
                   + FindString("EveSettingsSyncPage_GameRunningAsk");

        return MessageBox.Show(
            text,
            FindString("EveSettingsSyncPage_GameRunningTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    private static void ShowFailures(List<(string FileName, string Error)> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        var lines = failures.Take(12).Select(p => $"{p.FileName}: {p.Error}").ToList();
        if (failures.Count > lines.Count)
        {
            lines.Add("…");
        }

        MessageBox.Show(
            string.Join(Environment.NewLine, lines),
            FindString("EveSettingsSyncPage_SyncFailuresTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static async Task RunAsync(Func<Task<bool>> action)
    {
        try
        {
            await action();
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
