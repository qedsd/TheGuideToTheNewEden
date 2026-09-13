using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.ViewModels.Translation;

/// <summary>
/// 翻译页 VM：输入 → 防抖查询 → 左侧匹配列表 → 右侧译文详情。
/// <para>
/// 查询本身交给可插拔的翻译源（<see cref="TranslationService"/>）；当前只有「本地数据库」源
/// （离线、SDE 中英对照），因此界面上的来源下拉暂时只有一项，接入在线翻译后会自动多出选项。
/// 输入变化只做防抖查询（本地库查询在 UI 线程外执行），回车/按钮则立即查询。
/// </para>
/// </summary>
public sealed class TranslationPageViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>输入停顿多久后才查询（避免每敲一个字符就打一遍数据库）。</summary>
    private const int DebounceMilliseconds = 350;

    private readonly DispatcherTimer _debounceTimer;
    private readonly List<TranslationItem> _lastItems = [];

    private ITranslationProvider _provider;
    private CancellationTokenSource? _cancellationTokenSource;

    public TranslationPageViewModel()
    {
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMilliseconds) };
        _debounceTimer.Tick += OnDebounceTick;

        _provider = TranslationService.GetProvider(TranslationSettingService.Provider);
        _directionIndex = (int)TranslationSettingService.Direction;
        RefreshSourceNames();
    }

    // ---------- 输入与设置 ----------

    private string _inputText = string.Empty;

    /// <summary>输入的名词。变化后 350ms 自动查询（清空则直接清结果）。</summary>
    public string InputText
    {
        get => _inputText;
        set
        {
            if (!Set(ref _inputText, value))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                _debounceTimer.Stop();
                ClearResults();
                return;
            }

            RestartDebounce();
            NotifyState();
        }
    }

    private int _directionIndex;

    /// <summary>翻译方向下拉索引（0 自动 / 1 英→中 / 2 中→英）。变更即持久化并重新查询。</summary>
    public int DirectionIndex
    {
        get => _directionIndex;
        set
        {
            if (!Enum.IsDefined(typeof(TranslationDirection), value))
            {
                return;
            }

            if (!Set(ref _directionIndex, value))
            {
                return;
            }

            TranslationSettingService.SetDirection((TranslationDirection)value);
            RestartDebounce();
        }
    }

    private int _selectedSourceIndex;

    /// <summary>翻译来源下拉索引。变更即持久化并重新查询。</summary>
    public int SelectedSourceIndex
    {
        get => _selectedSourceIndex;
        set
        {
            // 重新填充 ItemsSource 时 ComboBox 会回写 -1，忽略它（由 RefreshSourceNames 统一校正）
            if (value < 0 || value >= TranslationService.Providers.Count)
            {
                return;
            }

            if (!Set(ref _selectedSourceIndex, value))
            {
                return;
            }

            _provider = TranslationService.Providers[value];
            TranslationSettingService.SetProvider(_provider.Key);
            RefreshSourceState();
            RestartDebounce();
        }
    }

    /// <summary>来源显示名（目前只有「本地数据库」一项，超过一项时下拉才可交互）。</summary>
    public ObservableCollection<string> Sources { get; } = [];

    /// <summary>可选来源多于一个时才允许展开下拉（只有一项时当只读标签用）。</summary>
    public bool HasMultipleSources => Sources.Count > 1;

    /// <summary>当前来源不可用的原因（本地化数据库缺失等），可用时为空。</summary>
    public string SourceUnavailableText { get; private set; } = string.Empty;

    public bool IsSourceUnavailable => !string.IsNullOrEmpty(SourceUnavailableText);

    // ---------- 结果 ----------

    /// <summary>匹配到的名词（每条自带另一语言的译名）。</summary>
    public ObservableCollection<TranslationMatchViewModel> Matches { get; } = [];

    private TranslationMatchViewModel? _selectedMatch;

    public TranslationMatchViewModel? SelectedMatch
    {
        get => _selectedMatch;
        set
        {
            if (!Set(ref _selectedMatch, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelection));
            // 只有详情区显示图标：只在选中时下载一次（内部有按类型 ID 的缓存）
            if (value is not null)
            {
                _ = value.LoadIconAsync();
            }
        }
    }

    public bool HasSelection => SelectedMatch is not null;

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                NotifyState();
            }
        }
    }

    private string _statusText = string.Empty;

    /// <summary>结果条数与方向（如「12 条 · 英文 → 中文」）。</summary>
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (Set(ref _statusText, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(StatusText);

    private bool _canPopWindow = true;

    /// <summary>是否允许"弹窗"（由弹窗承载的实例置 false，避免层层弹窗）。</summary>
    public bool CanPopWindow
    {
        get => _canPopWindow;
        set => Set(ref _canPopWindow, value);
    }

    /// <summary>输入了内容但一条都没匹配到（且不在查询中）。</summary>
    public bool ShowEmptyTip => !IsBusy && !string.IsNullOrWhiteSpace(InputText) && Matches.Count == 0;

    // ---------- 生命周期 ----------

    /// <summary>
    /// 页面每次进入时调用（页面实例常驻，设置可能已被别的窗口改过）。
    /// 页面切走时会 <see cref="Dispose"/>，因此这里先退订再订阅，避免重复订阅。
    /// </summary>
    public void Init()
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;

        _provider = TranslationService.GetProvider(TranslationSettingService.Provider);
        _directionIndex = (int)TranslationSettingService.Direction;
        OnPropertyChanged(nameof(DirectionIndex));
        RefreshSourceNames();

        if (!string.IsNullOrWhiteSpace(InputText))
        {
            RestartDebounce();
        }
    }

    /// <summary>页面切走时调用（页面实例仍常驻，再次进入会 <see cref="Init"/>）。</summary>
    public void Dispose()
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
        _debounceTimer.Stop();
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        IsBusy = false;
    }

    // ---------- 查询 ----------

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        _ = SearchAsync();
    }

    /// <summary>重新开始防抖计时（输入/设置变化时调用）。</summary>
    private void RestartDebounce()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    /// <summary>立即查询（回车、翻译按钮）。</summary>
    public async Task SearchAsync()
    {
        _debounceTimer.Stop();
        var text = InputText;
        if (string.IsNullOrWhiteSpace(text))
        {
            ClearResults();
            return;
        }

        // 来源不可用（本地化数据库缺失）时直接提示，不去打一遍注定失败的查询
        RefreshSourceState();
        if (!_provider.IsAvailable)
        {
            ClearResults();
            PageNotifyService.Warning(SourceUnavailableText);
            return;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _cancellationTokenSource, cancellationTokenSource);
        previous?.Cancel();
        previous?.Dispose();

        IsBusy = true;
        try
        {
            var outcome = await _provider.TranslateAsync(
                new TranslationRequest(text.Trim(), (TranslationDirection)DirectionIndex),
                cancellationTokenSource.Token);

            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (!outcome.Success)
            {
                ClearResults();
                // 来源不可用时优先给出本地化的原因（服务层的失败文案是给日志看的）
                var reason = IsSourceUnavailable ? SourceUnavailableText : outcome.ErrorMessage;
                PageNotifyService.Error($"{FindString("TranslationPage_Failed")}：{reason}");
                return;
            }

            ShowItems(outcome.Items, outcome.Direction);
        }
        catch (OperationCanceledException)
        {
            // 新的查询已开始，丢弃本次结果
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            ClearResults();
            PageNotifyService.Error($"{FindString("TranslationPage_Failed")}：{ex.Message}");
        }
        finally
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                IsBusy = false;
            }
        }
    }

    private void ShowItems(IReadOnlyList<TranslationItem> items, TranslationDirection direction)
    {
        _lastItems.Clear();
        _lastItems.AddRange(items);

        BuildMatches();

        StatusText = items.Count == 0
            ? string.Empty
            : $"{string.Format(FindString("TranslationPage_ResultCount"), items.Count)} · {FindString(TranslationLanguageHelper.DirectionKey(direction))}";
        NotifyState();
    }

    /// <summary>按当前语言（重新）构建匹配项包装。</summary>
    private void BuildMatches()
    {
        var selectedId = SelectedMatch?.Id;
        Matches.Clear();
        foreach (var item in _lastItems)
        {
            Matches.Add(new TranslationMatchViewModel(item));
        }

        SelectedMatch = Matches.Count == 0
            ? null
            : Matches.FirstOrDefault(p => p.Id == selectedId) ?? Matches[0];
    }

    private void ClearResults()
    {
        _lastItems.Clear();
        Matches.Clear();
        SelectedMatch = null;
        StatusText = string.Empty;
        NotifyState();
    }

    // ---------- 命令 ----------

    /// <summary>清空输入与结果。</summary>
    public void Clear()
    {
        InputText = string.Empty;
        ClearResults();
    }

    /// <summary>复制当前条目的译名（该条无译文时复制原文；无选中时给出提示）。</summary>
    public void CopyTranslation()
    {
        var match = SelectedMatch;
        if (match is null)
        {
            PageNotifyService.Warning(FindString("TranslationPage_NoSelection"));
            return;
        }

        CopyToClipboard(match.HasTranslation ? match.Item.Translation! : match.Query);
    }

    /// <summary>复制当前条目的原文。</summary>
    public void CopyQuery()
    {
        var match = SelectedMatch;
        if (match is null)
        {
            PageNotifyService.Warning(FindString("TranslationPage_NoSelection"));
            return;
        }

        CopyToClipboard(match.Query);
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            PageNotifyService.Success(FindString("TranslationPage_CopySuccess"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    // ---------- 语言与来源 ----------

    private void OnLanguageChanged(object? sender, string language)
    {
        RefreshSourceNames();
        // 名称/类型标签在包装项里已本地化，切语言后重建一次
        BuildMatches();
        NotifyState();
    }

    private void RefreshSourceNames()
    {
        Sources.Clear();
        foreach (var provider in TranslationService.Providers)
        {
            Sources.Add(FindString(provider.DisplayNameKey));
        }

        var index = TranslationService.Providers
            .Select((provider, i) => (provider, i))
            .FirstOrDefault(p => string.Equals(p.provider.Key, _provider.Key, StringComparison.Ordinal))
            .i;
        // SelectedSourceIndex 的 setter 会写设置并触发重新查询，这里直接改字段避免多余动作
        _selectedSourceIndex = index;
        OnPropertyChanged(nameof(SelectedSourceIndex));
        OnPropertyChanged(nameof(HasMultipleSources));
        RefreshSourceState();
    }

    private void RefreshSourceState()
    {
        SourceUnavailableText = _provider.IsAvailable || _provider.UnavailableReasonKey is null
            ? string.Empty
            : FindString(_provider.UnavailableReasonKey);
        OnPropertyChanged(nameof(SourceUnavailableText));
        OnPropertyChanged(nameof(IsSourceUnavailable));
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(ShowEmptyTip));
        OnPropertyChanged(nameof(HasSelection));
    }

    // ---------- INotifyPropertyChanged ----------

    public event PropertyChangedEventHandler? PropertyChanged;

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

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
