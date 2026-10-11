using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Models.Tools;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Tools;

namespace TheGuideToTheNewEden.WPF.ViewModels;

/// <summary>
/// 键盘跨度 VM：监听某个按键，按两种模式统计"通过次数"。
/// 对应 WinUI 版 <c>Views/Tools/KeyboardSpanPage</c>。
/// <list type="bullet">
/// <item>模式 0「时间间隔」：按一次该键开始计时（每 <c>间隔</c> 秒记一次通过），再按一次结束；攒够目标次数变红（WinUI 的语义）。</item>
/// <item>模式 1「按键次数」：每按一次记一次通过，到目标后从 1 重新计。</item>
/// </list>
/// <para>
/// 与 WinUI 版的差异：① 键盘钩子换成 WPF 侧移植版（扩展键/RAlt/Alt 组合键都能正确识别，见 <see cref="KeyboardHook"/>）；
/// ② 按键名比较改成**忽略大小写与首尾空格**（WinUI 是 <c>p.Name == _keyboardName</c>，手输 "f1" 就永远不命中）；
/// ③ 运行时长用秒表 + 1 秒定时器刷新（WinUI 用 <c>DateTime.Now - 开始时间</c>，跨夏令时/系统改时间会跳）；
/// ④ "定位游戏窗口"复用多开模块的客户端发现与激活（有 3 种激活策略，且会先还原最小化的窗口）；
/// ⑤ 窗口标题不再被改写成按键名（那是 WinUI 独立工具窗的用法，这里页面在主窗口里）。
/// </para>
/// </summary>
public sealed class KeyboardSpanPageViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _spanTimer;
    private readonly DispatcherTimer _durationTimer;
    private readonly System.Diagnostics.Stopwatch _duration = new();

    private string _keyboardName = "F1";
    private int _modeIndex;
    private double _targetCount = 10;
    private double _spanSeconds = 1;
    private string? _selectedCharacter;
    private bool _isRunning;
    private bool _counting;
    private int _passCount;
    private int _totalCount;
    private SpanState _passState = SpanState.Idle;

    public KeyboardSpanPageViewModel()
    {
        _spanTimer = new DispatcherTimer();
        _spanTimer.Tick += OnSpanTick;

        _durationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _durationTimer.Tick += (_, _) => OnPropertyChanged(nameof(DurationText));

        ThemeService.ThemeChanged += OnThemeChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // ---------- 设置项 ----------

    /// <summary>要监听的按键名（例如 F1；名称来自系统，与游戏内显示一致）。</summary>
    public string KeyboardName
    {
        get => _keyboardName;
        set => Set(ref _keyboardName, value ?? string.Empty);
    }

    /// <summary>模式：0 = 时间间隔，1 = 按键次数。</summary>
    public int ModeIndex
    {
        get => _modeIndex;
        set
        {
            if (Set(ref _modeIndex, value))
            {
                OnPropertyChanged(nameof(IsIntervalMode));
            }
        }
    }

    /// <summary>间隔模式（只有该模式下才显示"间隔（秒）"）。</summary>
    public bool IsIntervalMode => _modeIndex == 0;

    /// <summary>目标次数。</summary>
    public double TargetCount
    {
        get => _targetCount;
        set => Set(ref _targetCount, value);
    }

    /// <summary>间隔秒数（模式 0 用）。</summary>
    public double SpanSeconds
    {
        get => _spanSeconds;
        set => Set(ref _spanSeconds, value);
    }

    /// <summary>可定位的游戏窗口（EVE 客户端主窗口标题）。</summary>
    public ObservableCollection<string> Characters { get; } = new();

    public string? SelectedCharacter
    {
        get => _selectedCharacter;
        set => Set(ref _selectedCharacter, value);
    }

    // ---------- 运行状态 ----------

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (Set(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(IsIdle));
            }
        }
    }

    public bool IsIdle => !_isRunning;

    public int PassCount
    {
        get => _passCount;
        private set
        {
            if (Set(ref _passCount, value))
            {
                OnPropertyChanged(nameof(PassCountText));
            }
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set
        {
            if (Set(ref _totalCount, value))
            {
                OnPropertyChanged(nameof(TotalCountText));
            }
        }
    }

    /// <summary>通过次数显示颜色所依据的状态。</summary>
    public SpanState PassState
    {
        get => _passState;
        private set => Set(ref _passState, value);
    }

    public string PassCountText => _passCount.ToString();

    public string TotalCountText => _totalCount.ToString();

    public string TargetCountText => ((int)Math.Round(_targetCount)).ToString();

    public string DurationText => _duration.Elapsed.ToString(@"hh\:mm\:ss");

    // ---------- 控制 ----------

    public Task StartAsync()
    {
        if (_isRunning)
        {
            return Task.CompletedTask;
        }

        var keyboard = _keyboardName?.Trim() ?? string.Empty;
        if (keyboard.Length == 0)
        {
            PageNotifyService.Error(FindString("KeyboardSpanPage_NoKeyboard"));
            return Task.CompletedTask;
        }

        if (_targetCount < 1)
        {
            PageNotifyService.Error(FindString("KeyboardSpanPage_InvalidCount"));
            return Task.CompletedTask;
        }

        if (_modeIndex == 0 && _spanSeconds <= 0)
        {
            PageNotifyService.Error(FindString("KeyboardSpanPage_InvalidSpan"));
            return Task.CompletedTask;
        }

        PassCount = 0;
        TotalCount = 0;
        PassState = SpanState.Idle;
        _counting = false;
        _spanTimer.Stop();

        KeyboardHookService.Current.KeyPressed += OnKeyPressed;
        KeyboardHookService.Current.Start();

        _duration.Restart();
        _durationTimer.Start();
        IsRunning = true;

        OnPropertyChanged(nameof(TargetCountText));
        OnPropertyChanged(nameof(PassCountText));
        OnPropertyChanged(nameof(TotalCountText));
        OnPropertyChanged(nameof(DurationText));

        return Task.CompletedTask;
    }

    /// <summary>停止监听（保留当前计数，便于回看结果）。</summary>
    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }

        KeyboardHookService.Current.KeyPressed -= OnKeyPressed;
        KeyboardHookService.Current.Stop();

        _spanTimer.Stop();
        _durationTimer.Stop();
        _duration.Stop();
        _counting = false;
        IsRunning = false;
        PassState = SpanState.Idle;

        OnPropertyChanged(nameof(DurationText));
    }

    public async Task RefreshCharactersAsync()
    {
        try
        {
            var keywords = GamePreviewSettingService.Current.Value.ProcessKeywords;
            var titles = await Task.Run(() =>
            {
                var clients = GameClientService.FindClients(keywords);
                try
                {
                    return clients
                        .Select(c => c.WindowTitle)
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct()
                        .ToList();
                }
                finally
                {
                    foreach (var client in clients)
                    {
                        client.Process?.Dispose();
                    }
                }
            });

            var previous = _selectedCharacter;
            Characters.Clear();
            foreach (var title in titles)
            {
                Characters.Add(title);
            }

            SelectedCharacter = previous is not null && Characters.Contains(previous)
                ? previous
                : Characters.FirstOrDefault();

            if (Characters.Count == 0)
            {
                PageNotifyService.Info(FindString("KeyboardSpanPage_NoCharacter"));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
    }

    /// <summary>把选中的游戏窗口切到前台。</summary>
    public void BringCharacterToFront()
    {
        var title = _selectedCharacter;
        if (string.IsNullOrWhiteSpace(title))
        {
            PageNotifyService.Error(FindString("KeyboardSpanPage_NoCharacter"));
            return;
        }

        try
        {
            var keywords = GamePreviewSettingService.Current.Value.ProcessKeywords;
            var clients = GameClientService.FindClients(keywords);
            try
            {
                var target = clients.FirstOrDefault(c => c.WindowTitle?.Contains(title) == true);
                if (target is null || !GameClientService.Activate(target.MainWindowHandle, 0))
                {
                    PageNotifyService.Error(FindString("KeyboardSpanPage_FocusFailed"));
                }
            }
            finally
            {
                foreach (var client in clients)
                {
                    client.Process?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
    }

    public void Dispose()
    {
        Stop();
        ThemeService.ThemeChanged -= OnThemeChanged;
    }

    // ---------- 计数逻辑 ----------

    private void OnKeyPressed(KeyboardKeyInfo key)
    {
        if (!_isRunning || !NameMatches(key.Name))
        {
            return;
        }

        TotalCount++;

        if (_modeIndex == 0)
        {
            if (!_counting)
            {
                // 第一次按下：开一个间隔计时器，每 span 秒记一次通过。
                PassCount = 0;
                PassState = SpanState.Counting;
                _spanTimer.Interval = TimeSpan.FromSeconds(_spanSeconds);
                _spanTimer.Start();
                _counting = true;
            }
            else
            {
                // 再按一次：结束这一段计时。
                _spanTimer.Stop();
                _counting = false;
                PassState = SpanState.Idle;
            }
        }
        else
        {
            PassCount++;
            if (PassCount > (int)Math.Round(_targetCount))
            {
                PassCount = 1;
            }

            PassState = SpanState.Counting;
        }
    }

    private void OnSpanTick(object? sender, EventArgs e)
    {
        PassCount++;
        if (PassCount >= (int)Math.Round(_targetCount))
        {
            PassState = SpanState.Reached;
        }
    }

    /// <summary>按键名比较：忽略大小写与首尾空格（WinUI 是严格相等，手输 f1 永远不命中）。</summary>
    private bool NameMatches(string? name)
        => !string.IsNullOrEmpty(name)
           && string.Equals(name.Trim(), _keyboardName.Trim(), StringComparison.OrdinalIgnoreCase);

    private void OnThemeChanged()
    {
        // 颜色由转换器在求值时查主题画刷，重发通知让它立刻换色。
        OnPropertyChanged(nameof(PassState));
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
