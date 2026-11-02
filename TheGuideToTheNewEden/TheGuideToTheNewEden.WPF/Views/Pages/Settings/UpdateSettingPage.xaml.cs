using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Update;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>
/// 软件更新（绿色版单文件 exe 自更新，阶段 262）。
/// <para>
/// 旧的“下载安装包 → 启动外部 Updater”链路已废弃：单文件发布没有安装器，也不该为了更新重新引入安装。
/// 本页负责检查版本、展示发布说明、下载（可续传、可取消）并在用户确认后替换当前 exe 并重启。
/// </para>
/// </summary>
public partial class UpdateSettingPage : Page
{
    private UpdateCheckResult? _result;
    private UpdateInfo? _latest;
    private string? _downloadedPath;
    private CancellationTokenSource? _checkCts;
    private CancellationTokenSource? _downloadCts;
    private bool _initialized;
    private bool _busy;

    public UpdateSettingPage()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        CheckButton.Click += async (_, _) => await CheckAsync(force: true);
        DownloadButton.Click += async (_, _) => await DownloadAsync();
        InstallButton.Click += async (_, _) => await InstallAsync();
        CancelButton.Click += (_, _) => _downloadCts?.Cancel();
        ReleasePageButton.Click += (_, _) => OpenReleasePage();
        AutoCheckToggle.Click += (_, _) => AutoUpdateService.Set(AutoCheckToggle.IsChecked == true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        CurrentVersionText.Text = AppVersion.Current;
        AutoCheckToggle.IsChecked = AutoUpdateService.Value;

        var cached = UpdateService.Cached;
        if (cached is not null)
        {
            Render(cached);
        }

        // 缓存过期（或首次打开）时静默检查一次；失败只更新状态文字，不弹窗。
        _ = CheckAsync(force: false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _checkCts?.Cancel();
        _downloadCts?.Cancel();
    }

    // ==================================================================
    //  检查
    // ==================================================================

    private async Task CheckAsync(bool force)
    {
        if (_busy)
        {
            return;
        }

        _checkCts?.Cancel();
        _checkCts?.Dispose();
        _checkCts = new CancellationTokenSource();
        var token = _checkCts.Token;

        SetBusy(true, downloading: false);
        StatusText.Text = FindString("Settings.Update.Checking");
        ProgressPanel.Visibility = Visibility.Collapsed;
        DetailText.Visibility = Visibility.Collapsed;

        try
        {
            var result = await UpdateService.CheckAsync(force, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            Render(result);
        }
        catch (OperationCanceledException)
        {
            // 页面已离开：保留上一次状态即可。
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            StatusText.Text = $"{FindString("Settings.Update.Failed")}: {ex.Message}";
        }
        finally
        {
            SetBusy(false, downloading: false);
        }
    }

    private void Render(UpdateCheckResult result)
    {
        _result = result;
        _latest = result.Info;

        switch (result.Status)
        {
            case UpdateCheckStatus.UpToDate:
                UpdatePanel.Visibility = Visibility.Collapsed;
                StatusText.Text = FindString("Settings.Update.UpToDate");
                break;

            case UpdateCheckStatus.UpdateAvailable when result.Info is not null:
                StatusText.Text = string.Format(
                    FindString("Settings.Update.NewAvailableVersion"),
                    result.Info.Version);
                ShowDetails(result.Info);
                DownloadButton.IsEnabled = !_busy;
                InstallButton.IsEnabled = false;
                TryReuseDownloaded(result.Info);
                break;

            case UpdateCheckStatus.NoAsset when result.Info is not null:
                StatusText.Text = string.Format(
                    FindString("Settings.Update.NoAsset"),
                    result.Info.Version);
                ShowDetails(result.Info);
                DownloadButton.IsEnabled = false;
                InstallButton.IsEnabled = false;
                break;

            case UpdateCheckStatus.Failed:
                UpdatePanel.Visibility = Visibility.Collapsed;
                StatusText.Text = DescribeError(result.Error, "Settings.Update.Failed");
                if (!string.IsNullOrWhiteSpace(result.Error?.Detail))
                {
                    DetailText.Text = result.Error!.Detail;
                    DetailText.Visibility = Visibility.Visible;
                }
                break;
        }

        UpdatePanel.Visibility = result.HasUpdate && result.Info is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ShowDetails(UpdateInfo info)
    {
        LatestVersionText.Text = info.Version;
        PublishedText.Text = info.PublishedAt is { } published
            ? string.Format(FindString("Settings.Update.PublishedAt"), published.ToLocalTime().ToString("yyyy-MM-dd"))
            : string.Empty;

        SizeText.Text = info.Size > 0
            ? string.Format(FindString("Settings.Update.FileSize"), UpdateService.FormatBytes(info.Size))
            : string.Empty;

        var notes = UpdateService.FormatNotes(info.Notes);
        NotesHeader.Visibility = notes.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        NotesText.Visibility = notes.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        NotesText.Text = notes;
    }

    private void TryReuseDownloaded(UpdateInfo info)
    {
        _downloadedPath = null;
        if (!UpdateService.TryGetDownloaded(info, out var path) || path is null)
        {
            return;
        }

        // 大小已对上就先把「安装」点亮；真正校验（SHA-256 / PE 头）在安装前由服务再跑一次。
        _downloadedPath = path;
        InstallButton.IsEnabled = !_busy;
    }

    // ==================================================================
    //  下载
    // ==================================================================

    private async Task DownloadAsync()
    {
        if (_busy || _latest is not { HasAsset: true })
        {
            return;
        }

        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();
        var token = _downloadCts.Token;

        SetBusy(true, downloading: true);
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressBar.Ratio = 0;
        ProgressText.Text = FindString("Settings.Update.Downloading");
        SpeedText.Text = string.Empty;
        StatusText.Text = FindString("Settings.Update.Downloading");
        DetailText.Visibility = Visibility.Collapsed;

        var progress = new Progress<UpdateDownloadProgress>(OnDownloadProgress);

        UpdateService.SourceFailed += OnDownloadSourceFailed;
        try
        {
            _downloadedPath = await UpdateService.DownloadAsync(_latest, progress, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            ProgressBar.Ratio = 100;
            ProgressText.Text = FindString("Settings.Update.DownloadedVerified");
            SpeedText.Text = string.Empty;
            StatusText.Text = FindString("Settings.Update.DownloadedVerified");
            InstallButton.IsEnabled = true;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = FindString("Settings.Update.Cancelled");
        }
        catch (UpdateException ex)
        {
            Core.Log.Error(ex);
            StatusText.Text = DescribeError(new UpdateError(ex.Kind, ex.Message), "Settings.Update.DownloadFailed");
            DetailText.Text = ex.Message;
            DetailText.Visibility = Visibility.Visible;
            PageNotifyService.Error(FindString("Settings.Update.AllSourcesFailed"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            StatusText.Text = DescribeError(new UpdateError(UpdateErrorKind.DownloadFailed, ex.Message), "Settings.Update.DownloadFailed");
            DetailText.Text = ex.Message;
            DetailText.Visibility = Visibility.Visible;
            PageNotifyService.Error(FindString("Settings.Update.AllSourcesFailed"));
        }
        finally
        {
            UpdateService.SourceFailed -= OnDownloadSourceFailed;
            _downloadCts?.Dispose();
            _downloadCts = null;
            SetBusy(false, downloading: false);
            CancelButton.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>某个源失败（含 SHA 校验失败）时，右下角提示，再自动换下一个源。</summary>
    private void OnDownloadSourceFailed(object? sender, UpdateSourceFailedEventArgs e)
    {
        // 事件可能来自线程池；本地化资源查找/通知都放回 UI 线程。
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var key = e.IsChecksumFailure
                ? "Settings.Update.SourceChecksumFailed"
                : "Settings.Update.SourceFailed";
            var text = string.Format(FindString(key), e.SourceName);
            if (e.HasNextSource)
            {
                text += FindString("Settings.Update.TryingNextSource");
            }

            if (e.IsChecksumFailure)
            {
                PageNotifyService.Error(text);
            }
            else
            {
                PageNotifyService.Warning(text);
            }

            StatusText.Text = text;
            DetailText.Text = e.Message;
            DetailText.Visibility = Visibility.Visible;
        }));
    }

    private void OnDownloadProgress(UpdateDownloadProgress progress)
    {
        ProgressPanel.Visibility = Visibility.Visible;
        var formatKey = string.IsNullOrWhiteSpace(progress.SourceName)
            ? "Settings.Update.DownloadingPercent"
            : "Settings.Update.DownloadingFromPercent";
        if (progress.Percent >= 0)
        {
            ProgressBar.Ratio = progress.Percent;
            ProgressText.Text = string.IsNullOrWhiteSpace(progress.SourceName)
                ? string.Format(FindString(formatKey), progress.Percent)
                : string.Format(FindString(formatKey), progress.SourceName, progress.Percent);
            StatusText.Text = ProgressText.Text;
        }
        else
        {
            ProgressText.Text = string.IsNullOrWhiteSpace(progress.SourceName)
                ? FindString("Settings.Update.Downloading")
                : string.Format(FindString("Settings.Update.DownloadingFrom"), progress.SourceName);
        }

        SpeedText.Text = progress.BytesPerSecond > 0
            ? string.Format(
                FindString("Settings.Update.DownloadSpeed"),
                UpdateService.FormatSpeed(progress.BytesPerSecond))
            : string.Empty;
    }

    // ==================================================================
    //  安装
    // ==================================================================

    private async Task InstallAsync()
    {
        if (_busy || _latest is null || string.IsNullOrWhiteSpace(_downloadedPath))
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var answer = owner is null
            ? MessageBox.Show(
                string.Format(FindString("Settings.Update.InstallConfirmMessage"), _latest.Version),
                FindString("Settings.Update.InstallConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question)
            : MessageBox.Show(
                owner,
                string.Format(FindString("Settings.Update.InstallConfirmMessage"), _latest.Version),
                FindString("Settings.Update.InstallConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        SetBusy(true, downloading: false);
        StatusText.Text = FindString("Settings.Update.Installing");
        DetailText.Visibility = Visibility.Collapsed;

        try
        {
            var install = await UpdateService.InstallAsync(_latest, _downloadedPath);
            StatusText.Text = FindString("Settings.Update.Restarting");
            Core.Log.Info($"[更新] 安装完成（{install.Mode}），准备重启");

            if (install.Mode == UpdateRelaunchMode.ByApp)
            {
                // 文件已就地替换；等本进程真正退出后再拉起新版本（避免与单实例机制抢跑）。
                App.RequestRestartAfterUpdate();
            }
            else
            {
                // 辅助脚本会在本进程退出后替换并拉起新版本，这里直接退出。
                Application.Current.Shutdown();
            }
        }
        catch (UpdateException ex)
        {
            Core.Log.Error(ex);
            StatusText.Text = DescribeError(new UpdateError(ex.Kind, ex.Message), "Settings.Update.InstallFailed");
            DetailText.Text = ex.Message;
            DetailText.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            StatusText.Text = DescribeError(new UpdateError(UpdateErrorKind.ReplaceFailed, ex.Message), "Settings.Update.InstallFailed");
            DetailText.Text = ex.Message;
            DetailText.Visibility = Visibility.Visible;
        }
        finally
        {
            SetBusy(false, downloading: false);
        }
    }

    private void OpenReleasePage()
    {
        // 国内可能打不开 GitHub，给两个入口让用户自己选。
        var menu = new ContextMenu();
        menu.Items.Add(CreateReleaseMenuItem("Settings.Update.OpenGitee", $"{UpdateService.GiteeRepositoryUrl}/releases"));
        menu.Items.Add(CreateReleaseMenuItem("Settings.Update.OpenGitHub", UpdateService.ReleasePageUrl));
        menu.PlacementTarget = ReleasePageButton;
        menu.IsOpen = true;
    }

    private MenuItem CreateReleaseMenuItem(string languageKey, string url)
    {
        var item = new MenuItem { Header = FindString(languageKey) };
        item.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                MessageBox.Show(
                    Window.GetWindow(this),
                    ex.Message,
                    FindString(languageKey),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        };
        return item;
    }

    // ==================================================================
    //  状态
    // ==================================================================

    private void SetBusy(bool busy, bool downloading)
    {
        _busy = busy;
        CheckButton.IsEnabled = !busy;
        ReleasePageButton.IsEnabled = !busy;
        AutoCheckToggle.IsEnabled = !busy;

        // 下载中用「停止」替换「下载 / 安装」，避免用户以为还能再点一次下载。
        CancelButton.Visibility = downloading ? Visibility.Visible : Visibility.Collapsed;
        DownloadButton.Visibility = downloading ? Visibility.Collapsed : Visibility.Visible;
        InstallButton.Visibility = downloading ? Visibility.Collapsed : Visibility.Visible;

        if (busy)
        {
            DownloadButton.IsEnabled = false;
            InstallButton.IsEnabled = false;
            return;
        }

        var hasAsset = _latest is { HasAsset: true } && _result?.Status == UpdateCheckStatus.UpdateAvailable;
        DownloadButton.IsEnabled = hasAsset;
        InstallButton.IsEnabled = hasAsset && !string.IsNullOrWhiteSpace(_downloadedPath) && System.IO.File.Exists(_downloadedPath);
    }

    private string DescribeError(UpdateError? error, string fallbackKey)
    {
        if (error is null)
        {
            return FindString(fallbackKey);
        }

        var key = $"Settings.Update.Error.{error.Kind}";
        var text = FindString(key);
        if (text == key)
        {
            text = FindString("Settings.Update.Error.Unknown");
        }

        return text;
    }

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}
