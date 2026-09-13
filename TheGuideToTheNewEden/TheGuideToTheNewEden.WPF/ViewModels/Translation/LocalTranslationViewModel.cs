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
/// 「本地词库」页签的 VM：输入 → 本地 SDE 中英词库查询 → 左侧匹配列表 → 右侧译文详情。
/// <para>
/// 与 AI 页签（<see cref="AiTranslationViewModel"/>）**完全分开**：这里只认离线的
/// <see cref="LocalDbTranslationProvider"/>，所以没有"来源下拉"，也不会有计费问题——
/// 输入停顿 350ms 自动查询（<b>实时</b>反馈），回车只是"立刻查"。
/// </para>
/// </summary>
public sealed class LocalTranslationViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>输入停顿多久后才查询（避免每敲一个字符就打一遍数据库）。</summary>
    private const int DebounceMilliseconds = 350;

    private readonly DispatcherTimer _debounceTimer;
    private readonly List<TranslationItem> _lastItems = [];
    private readonly ITranslationProvider _provider = TranslationService.LocalDatabase;

    private CancellationTokenSource? _cancellationTokenSource;

    public LocalTranslationViewModel()
    {
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMilliseconds) };
        _debounceTimer.Tick += OnDebounceTick;
        _directionIndex = ToIndex(TranslationSettingService.LocalFrom, TranslationSettingService.LocalTo);
    }

    // ---------- 输入与设置 ----------

    private string _inputText = string.Empty;

    /// <summary>输入的名词；变化后 350ms 自动查询（清空则直接清结果）。</summary>
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

    /// <summary>
    /// 翻译方向下拉索引（0 自动 / 1 英→中 / 2 中→英）。本地词库只有 SDE 的中英对照，
    /// 所以这里的选项就是"中英三选一"；别的语言请用「AI 翻译」页。
    /// </summary>
    public int DirectionIndex
    {
        get => _directionIndex;
        set
        {
            if (value is < 0 or > 2)
            {
                return;
            }

            if (!Set(ref _directionIndex, value))
            {
                return;
            }

            var (from, to) = FromIndex(value);
            TranslationSettingService.SetLocalDirection(from, to);
            RestartDebounce();
        }
    }

    /// <summary>下拉索引 → (源语言, 目标语言)。</summary>
    private static (string From, string To) FromIndex(int index) => index switch
    {
        1 => (TranslationLanguages.English, TranslationLanguages.Chinese),
        2 => (TranslationLanguages.Chinese, TranslationLanguages.English),
        _ => (TranslationLanguages.Auto, TranslationLanguages.Chinese),
    };

    /// <summary>(源语言, 目标语言) → 下拉索引。</summary>
    private static int ToIndex(string from, string to)
    {
        var source = TranslationLanguages.Normalize(from);
        var target = TranslationLanguages.Normalize(to);
        if (source == TranslationLanguages.English && target == TranslationLanguages.Chinese)
        {
            return 1;
        }

        if (source == TranslationLanguages.Chinese && target == TranslationLanguages.English)
        {
            return 2;
        }

        return 0;
    }

    /// <summary>本地化数据库不可用时的原因（可用时为空）。</summary>
    public string UnavailableText { get; private set; } = string.Empty;

    public bool IsUnavailable => !string.IsNullOrEmpty(UnavailableText);

    // ---------- 结果 ----------

    /// <summary>结果条目（本地源是若干名词对照）。</summary>
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

    /// <summary>输入了内容但一条都没结果（且不在查询中）。</summary>
    public bool ShowEmptyTip => !IsBusy && !string.IsNullOrWhiteSpace(InputText) && Matches.Count == 0;

    /// <summary>空结果提示文案。</summary>
    public string EmptyTipText => FindString("TranslationPage_NoResultTip");

    // ---------- 生命周期 ----------

    /// <summary>
    /// 页面每次进入时调用（页面实例常驻，设置可能已被别的窗口改过）。
    /// 页面切走时会 <see cref="Dispose"/>，因此这里先退订再订阅，避免重复订阅。
    /// </summary>
    public void Init()
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;

        var direction = ToIndex(TranslationSettingService.LocalFrom, TranslationSettingService.LocalTo);
        if (_directionIndex != direction)
        {
            _directionIndex = direction;
            OnPropertyChanged(nameof(DirectionIndex));
        }

        RefreshUnavailableState();

        if (!string.IsNullOrWhiteSpace(InputText))
        {
            RestartDebounce();
        }
    }

    /// <summary>离开页面时调用（页面实例仍常驻，再次进入会 <see cref="Init"/>）：停查询、退订语言事件。</summary>
    public void Dispose()
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
        _debounceTimer.Stop();
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        PageNotifyService.HideWaiting();
        IsBusy = false;
    }

    // ---------- 查询 ----------

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        _ = SearchAsync();
    }

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

        // 本地化库缺失时直接提示，不去打一遍注定失败的查询
        RefreshUnavailableState();
        if (!_provider.IsAvailable)
        {
            ClearResults();
            PageNotifyService.Warning(UnavailableText);
            return;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _cancellationTokenSource, cancellationTokenSource);
        previous?.Cancel();
        previous?.Dispose();

        IsBusy = true;
        try
        {
            var (from, to) = FromIndex(DirectionIndex);
            var outcome = await _provider.TranslateAsync(
                new TranslationRequest(text.Trim(), from, to),
                cancellationTokenSource.Token);

            if (cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (!outcome.Success)
            {
                ClearResults();
                var reason = IsUnavailable ? UnavailableText : outcome.ErrorMessage;
                PageNotifyService.Error($"{FindString("TranslationPage_Failed")}：{reason}");
                return;
            }

            ShowItems(outcome);
        }
        catch (OperationCanceledException)
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                PageNotifyService.Error(FindString("TranslationPage_Timeout"));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            ClearResults();
            PageNotifyService.Error($"{FindString("TranslationPage_Failed")}：{ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_cancellationTokenSource, cancellationTokenSource))
            {
                IsBusy = false;
            }
        }
    }

    public void Cancel() => _cancellationTokenSource?.Cancel();

    private void ShowItems(TranslationOutcome outcome)
    {
        _lastItems.Clear();
        _lastItems.AddRange(outcome.Items);

        BuildMatches();

        StatusText = outcome.Items.Count == 0
            ? string.Empty
            : $"{string.Format(FindString("TranslationPage_ResultCount"), outcome.Items.Count)} · {Describe(outcome.From, outcome.To)}";
        NotifyState();
    }

    /// <summary>按当前语言（重新）构建结果包装。</summary>
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

    /// <summary>复制当前条目的译名/译文（该条无译文时复制原文；无选中时给出提示）。</summary>
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

    // ---------- 语言状态 ----------

    private void OnLanguageChanged(object? sender, string language)
    {
        // 名称/类型标签在包装项里已本地化，切语言后重建一次
        BuildMatches();
        RefreshUnavailableState();
        NotifyState();
    }

    private void RefreshUnavailableState()
    {
        var text = _provider.IsAvailable || _provider.UnavailableReasonKey is null
            ? string.Empty
            : FindString(_provider.UnavailableReasonKey);

        if (UnavailableText == text)
        {
            return;
        }

        UnavailableText = text;
        OnPropertyChanged(nameof(UnavailableText));
        OnPropertyChanged(nameof(IsUnavailable));
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(ShowEmptyTip));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(EmptyTipText));
    }

    /// <summary>形如「英语 → 中文」（语言名走本地化键）。</summary>
    private static string Describe(string from, string to)
        => $"{FindString(TranslationLanguageHelper.LanguageKey(from))} → {FindString(TranslationLanguageHelper.LanguageKey(to))}";

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
