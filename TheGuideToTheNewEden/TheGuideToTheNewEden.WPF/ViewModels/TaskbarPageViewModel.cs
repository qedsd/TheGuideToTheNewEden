using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels;

/// <summary>
/// 任务栏工具 VM：把 EVE 客户端窗口的任务栏按钮**拆分**（每个角色一个独立按钮）或**合并**（并成一个）。
/// 对应 WinUI 版 <c>Views/Tools/TaskbarPage</c> 的两个按钮，但补了状态行与失败提示（WinUI 点完没有任何反馈）。
/// <para>
/// 客户端枚举复用多开模块的 <see cref="GameClientService.FindClients"/>，关键词取用户在多开页配置的
/// <c>ProcessKeywords</c>（默认 <c>exefile</c>）——不再像 WinUI 那样在页面里另写一遍 <c>GetProcessesByName("exefile")</c>。
/// </para>
/// </summary>
public sealed class TaskbarPageViewModel : INotifyPropertyChanged
{
    private string _statusText = string.Empty;
    private bool _isBusy;
    private int _lastTotal;
    private int _lastUpdated;

    public TaskbarPageViewModel()
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
        ResetStatus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>状态行：上一次操作的结果 / 当前等待态。</summary>
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsIdle));
            }
        }
    }

    /// <summary>按钮可用性（执行期间禁用，避免连点）。</summary>
    public bool IsIdle => !_isBusy;

    /// <summary>拆分：每个客户端窗口各给一个唯一 AppUserModelID。</summary>
    public Task SeparateAsync() => ApplyAsync(separate: true);

    /// <summary>合并：所有客户端窗口共用一个 AppUserModelID。</summary>
    public Task MergeAsync() => ApplyAsync(separate: false);

    private async Task ApplyAsync(bool separate)
    {
        if (_isBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = FindString("TaskbarPage_Working");

        try
        {
            var keywords = GamePreviewSettingService.Current.Value.ProcessKeywords;

            // 进程枚举 + COM 属性写入都放线程池，别占 UI 线程。
            var result = await Task.Run(() =>
            {
                var clients = GameClientService.FindClients(keywords);
                var sharedId = Guid.NewGuid().ToString();
                var updated = 0;

                try
                {
                    foreach (var client in clients)
                    {
                        var appId = separate ? Guid.NewGuid().ToString() : sharedId;
                        if (TaskbarHelper.SetAppUserModelId(client.MainWindowHandle, appId))
                        {
                            updated++;
                        }
                    }
                }
                finally
                {
                    foreach (var client in clients)
                    {
                        client.Process?.Dispose();
                    }
                }

                return (Total: clients.Count, Updated: updated);
            });

            _lastTotal = result.Total;
            _lastUpdated = result.Updated;

            if (result.Total == 0)
            {
                StatusText = string.Format(FindString("TaskbarPage_NoClient"), keywords);
                PageNotifyService.Error(StatusText);
                return;
            }

            StatusText = BuildDoneText();
            PageNotifyService.Success(StatusText);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            StatusText = string.Format(FindString("TaskbarPage_Failed"), ex.Message);
            PageNotifyService.Error(StatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string BuildDoneText()
        => string.Format(FindString("TaskbarPage_Done"), _lastUpdated, _lastTotal);

    private void ResetStatus()
        => StatusText = FindString("TaskbarPage_Idle");

    private void OnLanguageChanged(object? sender, string language)
    {
        // 状态行是"求值时查资源"的文案，换语言后重新求值（§9 第 63 条）。
        if (_lastTotal > 0)
        {
            StatusText = BuildDoneText();
        }
        else if (!_isBusy)
        {
            ResetStatus();
        }
    }

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
