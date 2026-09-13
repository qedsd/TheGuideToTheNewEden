using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Translation;
using TheGuideToTheNewEden.WPF.Services.Translation.Llm;
using TheGuideToTheNewEden.WPF.Views.Pages;
using TheGuideToTheNewEden.WPF.Views.Pages.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels.Translation;

/// <summary>
/// 「AI 翻译」页的 VM：像 AI 桌面端那样的**对话记录**界面。
/// <list type="bullet">
///   <item>左侧是**多个对话**（可新建 / 删除 / 切换），历史落盘到 <c>Configs/AiTranslationHistory.json</c>；</item>
///   <item>右侧是当前对话的完整记录（原文气泡靠右、译文气泡靠左，文字用富文本可任意拖选复制）；</item>
///   <item>**一个对话只有一个「带上上下文」开关**：勾上后本对话每次翻译都会带上此前所有成功的记录（**不限条数**）；</item>
///   <item>**不占用全局等待遮罩**：每条译文在自己的位置显示行内转圈；等待期间可以继续发下一条，多条并发翻译各等各的；</item>
///   <item>AI 按次计费 → 不自动翻译，只在回车 / 「翻译」时请求；必须先配置好端点。</item>
/// </list>
/// </summary>
public sealed class AiChatTranslationViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ITranslationProvider _provider = TranslationService.Ai;
    private readonly List<PendingTranslation> _pending = [];
    private string _sourceLanguage = TranslationLanguages.Auto;
    private string _targetLanguage = TranslationLanguages.Chinese;

    /// <summary>一条正在翻译中的记录（各自的取消令牌，互不影响）。</summary>
    private sealed record PendingTranslation(AiChatTurnViewModel Turn, CancellationTokenSource Source);

    public AiChatTranslationViewModel()
    {
        _sourceLanguage = TranslationSettingService.AiFrom;
        _targetLanguage = TranslationSettingService.AiTo;
        _pairA = TranslationSettingService.AiPairA;
        _pairB = TranslationSettingService.AiPairB;
        RefreshLanguageOptions();
        foreach (var session in AiTranslationHistoryService.Load())
        {
            Sessions.Add(new AiChatSessionViewModel(session, OnSessionEdited));
        }

        SelectedSession = Sessions.FirstOrDefault();
        RefreshConfiguration();
    }

    /// <summary>界面上需要"滚到底部"时触发（新记录、翻译完成）。</summary>
    public event EventHandler? ScrollToEndRequested;

    // ---------- 配置状态 ----------

    private bool _isConfigured;

    /// <summary>是否已配置到可用的程度（地址 + Key + 模型）。</summary>
    public bool IsConfigured
    {
        get => _isConfigured;
        private set
        {
            if (Set(ref _isConfigured, value))
            {
                OnPropertyChanged(nameof(IsNotConfigured));
            }
        }
    }

    public bool IsNotConfigured => !IsConfigured;

    private string _endpointSummary = string.Empty;

    /// <summary>当前端点摘要（如「OpenAI 兼容 · deepseek-chat」）。</summary>
    public string EndpointSummary
    {
        get => _endpointSummary;
        private set => Set(ref _endpointSummary, value);
    }

    public string NotConfiguredText => FindString("TranslationPage_AiUnavailable");

    // ---------- 对话列表 ----------

    /// <summary>全部对话（最近更新的在前）。</summary>
    public ObservableCollection<AiChatSessionViewModel> Sessions { get; } = [];

    private AiChatSessionViewModel? _selectedSession;

    public AiChatSessionViewModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (ReferenceEquals(_selectedSession, value))
            {
                return;
            }

            // 订阅选中对话的属性变化：标题在会话里改名后，界面头部绑的是本 VM 的 SessionTitle，
            // 不转发通知的话就会出现"左边列表更新了、头部还是旧名字"（用户实测反馈）
            if (_selectedSession is not null)
            {
                _selectedSession.PropertyChanged -= OnSelectedSessionPropertyChanged;
            }

            _selectedSession = value;

            if (_selectedSession is not null)
            {
                _selectedSession.PropertyChanged += OnSelectedSessionPropertyChanged;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSession));
            OnPropertyChanged(nameof(SessionTitle));
            OnPropertyChanged(nameof(HasTurns));
            OnPropertyChanged(nameof(ShowSessionEmptyTip));
            OnPropertyChanged(nameof(ContextInfoText));
            ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>选中对话自身的属性变化 → 把标题等转发给界面（头部绑的是本 VM 的属性）。</summary>
    private void OnSelectedSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(AiChatSessionViewModel.Title))
        {
            OnPropertyChanged(nameof(SessionTitle));
        }
    }

    /// <summary>开始给当前对话改名（界面上的铅笔按钮 / 双击标题 / 右键菜单）。</summary>
    public void BeginRenameSelectedSession() => SelectedSession?.BeginRename();

    public bool HasSession => SelectedSession is not null;

    public bool HasTurns => SelectedSession?.Turns.Count > 0;

    /// <summary>当前对话还没有任何记录时的引导（"输入内容点翻译"）。</summary>
    public bool ShowSessionEmptyTip => IsConfigured && !HasTurns && !HasPending;

    public string SessionTitle => SelectedSession?.Title ?? FindString("AiTranslation_Chat_NewSession");

    /// <summary>上下文开关的状态说明（当前对话）。</summary>
    public string ContextInfoText => SelectedSession?.ContextInfoText ?? string.Empty;

    /// <summary>新建一个对话并选中（不落盘，等真的翻译出一条记录再存）。</summary>
    public void NewSession()
    {
        var session = new AiChatSessionViewModel(new AiTranslationSession(), OnSessionEdited);
        Sessions.Insert(0, session);
        SelectedSession = session;
        NotifySessionList();
    }

    /// <summary>删除一个对话。</summary>
    public void DeleteSession(AiChatSessionViewModel? session)
    {
        if (session is null)
        {
            return;
        }

        var index = Sessions.IndexOf(session);
        Sessions.Remove(session);
        AiTranslationHistoryService.Delete(session.Id);

        if (ReferenceEquals(SelectedSession, session))
        {
            SelectedSession = Sessions.Count > 0
                ? Sessions[Math.Clamp(index, 0, Sessions.Count - 1)]
                : null;
        }

        NotifySessionList();
    }

    /// <summary>清空当前对话的记录（对话本身保留）。</summary>
    public void ClearCurrentSession()
    {
        var session = SelectedSession;
        if (session is null)
        {
            return;
        }

        session.ClearTurns();
        AiTranslationHistoryService.Delete(session.Id);
        NotifySessionList();
    }

    private void NotifySessionList()
    {
        OnPropertyChanged(nameof(HasSession));
        OnPropertyChanged(nameof(HasTurns));
        OnPropertyChanged(nameof(ShowSessionEmptyTip));
        OnPropertyChanged(nameof(SessionTitle));
        OnPropertyChanged(nameof(ContextInfoText));
    }

    // ---------- 输入与方向 ----------

    private string _inputText = string.Empty;

    /// <summary>待翻译的原文（多行；回车=翻译、Shift+回车=换行）。等待期间可以继续输入并发送。</summary>
    public string InputText
    {
        get => _inputText;
        set
        {
            if (Set(ref _inputText, value))
            {
                OnPropertyChanged(nameof(HasInput));
            }
        }
    }

    public bool HasInput => !string.IsNullOrWhiteSpace(InputText);

    /// <summary>语言下拉项（代码 + 本地化名字，界面用 SelectedValuePath="Code" 绑定）。</summary>
    public sealed record LanguageOption(string Code, string Name);

    private IReadOnlyList<LanguageOption> _sourceOptions = [];

    /// <summary>源语言下拉的可选项（含"自动"）。</summary>
    public IReadOnlyList<LanguageOption> SourceLanguageOptions
    {
        get => _sourceOptions;
        private set => Set(ref _sourceOptions, value);
    }

    private IReadOnlyList<LanguageOption> _targetOptions = [];

    /// <summary>目标语言下拉的可选项。</summary>
    public IReadOnlyList<LanguageOption> TargetLanguageOptions
    {
        get => _targetOptions;
        private set => Set(ref _targetOptions, value);
    }

    /// <summary>按当前界面语言重建语言下拉项。</summary>
    private void RefreshLanguageOptions()
    {
        SourceLanguageOptions = TranslationLanguages.Sources
            .Select(code => new LanguageOption(code, FindString(TranslationLanguages.DisplayKey(code))))
            .ToList();
        TargetLanguageOptions = TranslationLanguages.TargetOptions
            .Select(code => new LanguageOption(code, FindString(TranslationLanguages.TargetDisplayKey(code))))
            .ToList();
        PairOptions = TranslationLanguages.Targets
            .Select(code => new LanguageOption(code, FindString(TranslationLanguages.DisplayKey(code))))
            .ToList();
        OnPropertyChanged(nameof(PairOptions));
    }

    /// <summary>源语言代码（auto = 按正文脚本自动判定）。</summary>
    public string SourceLanguage
    {
        get => _sourceLanguage;
        set
        {
            var normalized = TranslationLanguages.IsSource(value) ? TranslationLanguages.Normalize(value) : TranslationLanguages.Auto;
            if (Set(ref _sourceLanguage, normalized))
            {
                TranslationSettingService.SetAiDirection(_sourceLanguage, _targetLanguage);
            }
        }
    }

    /// <summary>目标语言代码（<c>auto</c> = 自动译成另一种语言：由"互译语言对"决定）。</summary>
    public string TargetLanguage
    {
        get => _targetLanguage;
        set
        {
            var normalized = TranslationLanguages.IsTargetOption(value) ? TranslationLanguages.Normalize(value) : TranslationLanguages.Auto;
            if (Set(ref _targetLanguage, normalized))
            {
                TranslationSettingService.SetAiDirection(_sourceLanguage, _targetLanguage);
                OnPropertyChanged(nameof(IsSameLanguage));
                OnPropertyChanged(nameof(IsAutoTarget));
            }
        }
    }

    /// <summary>目标是不是「自动检测」（目标位置上的 auto，语义是"译成另一种语言"）——是的话才显示互译语言对（界面据此显隐）。</summary>
    public bool IsAutoTarget => TranslationLanguages.Normalize(_targetLanguage) == TranslationLanguages.Auto;

    /// <summary>互译语言对的两种语言下拉项（都是具体语言）。</summary>
    public IReadOnlyList<LanguageOption> PairOptions { get; private set; } = [];

    private string _pairA = TranslationLanguages.Chinese;

    /// <summary>自动互译语言对的**第一种**语言（其它语言统一译成它，通常设成自己的母语）。</summary>
    public string PairA
    {
        get => _pairA;
        set
        {
            var normalized = TranslationLanguages.IsTarget(value) ? TranslationLanguages.Normalize(value) : TranslationLanguages.Chinese;
            if (Set(ref _pairA, normalized))
            {
                TranslationSettingService.SetAiPair(_pairA, _pairB);
            }
        }
    }

    private string _pairB = TranslationLanguages.English;

    /// <summary>自动互译语言对的**第二种**语言。</summary>
    public string PairB
    {
        get => _pairB;
        set
        {
            var normalized = TranslationLanguages.IsTarget(value) ? TranslationLanguages.Normalize(value) : TranslationLanguages.English;
            if (Set(ref _pairB, normalized))
            {
                TranslationSettingService.SetAiPair(_pairA, _pairB);
            }
        }
    }

    /// <summary>对调互译语言对（只影响"其它语言译成哪一种"，以及 A/B 的先后）。</summary>
    public void SwapPair()
    {
        var a = _pairA;
        PairA = _pairB;
        PairB = a;
    }

    /// <summary>源语言与目标语言是否相同（相同时给个提示：自动方向会改成中英）。</summary>
    public bool IsSameLanguage => TranslationLanguages.Normalize(_sourceLanguage) == TranslationLanguages.Normalize(_targetLanguage);

    /// <summary>把源/目标语言对调（界面上的 ⇄ 按钮）。源语言是"自动"时只把目标切成中↔英。</summary>
    public void SwapLanguages()
    {
        var source = TranslationLanguages.Normalize(_sourceLanguage);
        if (source == TranslationLanguages.Auto)
        {
            TargetLanguage = TranslationLanguages.Normalize(_targetLanguage) == TranslationLanguages.Chinese
                ? TranslationLanguages.English
                : TranslationLanguages.Chinese;
            return;
        }

        var target = _targetLanguage;
        SourceLanguage = target;
        TargetLanguage = source;
    }

    /// <summary>是否开着流式输出。</summary>
    public bool IsStreaming => TranslationSettingService.AiStream;

    // ---------- 运行状态 ----------

    /// <summary>是否有正在翻译的记录（可以继续发新的，这里只用于"停止"按钮与状态文字）。</summary>
    public bool HasPending => _pending.Count > 0;

    public bool IsIdle => !HasPending;

    /// <summary>形如「正在翻译 2 条…」。</summary>
    public string PendingText => HasPending
        ? string.Format(FindString("AiTranslation_Chat_PendingCount"), _pending.Count)
        : string.Empty;

    private bool _canPopWindow = true;

    /// <summary>是否允许"弹窗"（由弹窗承载的实例置 false）。</summary>
    public bool CanPopWindow
    {
        get => _canPopWindow;
        set => Set(ref _canPopWindow, value);
    }

    // ---------- 生命周期 ----------

    /// <summary>页面每次进入时调用（设置可能刚被"去配置"改过）。</summary>
    public void Init()
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;

        if (_sourceLanguage != TranslationSettingService.AiFrom)
        {
            _sourceLanguage = TranslationSettingService.AiFrom;
            OnPropertyChanged(nameof(SourceLanguage));
        }

        if (_targetLanguage != TranslationSettingService.AiTo)
        {
            _targetLanguage = TranslationSettingService.AiTo;
            OnPropertyChanged(nameof(TargetLanguage));
            OnPropertyChanged(nameof(IsAutoTarget));
        }

        if (_pairA != TranslationSettingService.AiPairA)
        {
            _pairA = TranslationSettingService.AiPairA;
            OnPropertyChanged(nameof(PairA));
        }

        if (_pairB != TranslationSettingService.AiPairB)
        {
            _pairB = TranslationSettingService.AiPairB;
            OnPropertyChanged(nameof(PairB));
        }

        RefreshConfiguration();
        OnPropertyChanged(nameof(SessionTitle));
        OnPropertyChanged(nameof(ShowSessionEmptyTip));
        OnPropertyChanged(nameof(ContextInfoText));
        ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 离开页面时调用（页面/视图实例仍然常驻）：**只退订语言事件，不取消正在跑的翻译**。
    /// <para>
    /// 这里以前是 <c>StopAll()</c>，导致"翻译途中切了一下页面/导航重新承载了页面"就把请求掐掉、
    /// 气泡上留下"已停止"（实测复现：视图 Unloaded 一次 → 正在翻的那条立即变已停止、译文只写了一半）。
    /// 页面是缓存的，回到页面时结果应当还在；真要中止请点界面上的「停止」。
    /// </para>
    /// </summary>
    public void Dispose() => LanguageService.LanguageChanged -= OnLanguageChanged;

    /// <summary>重新读一次配置。</summary>
    public void RefreshConfiguration()
    {
        var endpoint = TranslationSettingService.AiEndpoint;
        IsConfigured = endpoint.IsConfigured;
        EndpointSummary = IsConfigured
            ? $"{FindString(ChatProtocolFactory.Get(endpoint.Protocol).DisplayNameKey)} · {endpoint.Model}"
            : string.Empty;

        OnPropertyChanged(nameof(NotConfiguredText));
        OnPropertyChanged(nameof(IsStreaming));
        OnPropertyChanged(nameof(ShowSessionEmptyTip));
    }

    /// <summary>打开「设置 → AI 翻译」。</summary>
    public void OpenSettings()
    {
        Navigation.Navigate(typeof(SettingsPage));
        SettingsPage.RequestCategory(typeof(AiTranslationSettingPage));
        Navigation.Activate();
    }

    // ---------- 翻译 ----------

    /// <summary>
    /// 发起一次翻译（回车 / 「翻译」按钮）。**不等前一条结束**：每条各自一个请求、各自一个转圈，
    /// 用户可以连着发多条，译文就按发送顺序逐个出现在各自的位置上。
    /// </summary>
    public async Task TranslateAsync()
    {
        RefreshConfiguration();
        if (!IsConfigured)
        {
            PageNotifyService.Warning(NotConfiguredText);
            return;
        }

        var text = InputText?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            PageNotifyService.Warning(FindString("TranslationPage_Ai_NoInput"));
            return;
        }

        // 没有对话时先建一个（用户直接开翻就不用手动点"新建对话"）
        var session = SelectedSession ?? CreateSession();
        var turn = session.AddTurn(new AiTranslationTurn
        {
            Time = DateTime.Now,
            From = _sourceLanguage,
            To = _targetLanguage,
            Original = text,
            Success = false,
        });
        turn.IsPending = true;
        session.ApplyAutoTitle();

        // 新的一条发出去了：把之前那些"没被人工干预过"的超行译文自动收起来（只自动收一次）
        session.AutoCollapsePreviousTranslations(turn);

        // 立刻清空输入框：下一条可以马上接着发
        InputText = string.Empty;
        NotifySessionList();
        ScrollToEndRequested?.Invoke(this, EventArgs.Empty);

        var context = BuildContext(session, turn);
        var cancellationTokenSource = new CancellationTokenSource();
        var pending = new PendingTranslation(turn, cancellationTokenSource);
        _pending.Add(pending);
        NotifyPending();

        var request = new TranslationRequest(text, _sourceLanguage, _targetLanguage);
        if (context.Count > 0)
        {
            request = request with { Context = context };
        }

        if (TranslationSettingService.AiStream)
        {
            request = request with { Progress = new Progress<string>(partial => turn.Translation = partial) };
        }

        try
        {
            var outcome = await _provider.TranslateAsync(request, cancellationTokenSource.Token);

            if (cancellationTokenSource.IsCancellationRequested)
            {
                turn.Error = FindString("TranslationPage_Ai_Stopped");
                return;
            }

            if (!outcome.Success)
            {
                turn.Error = IsNotConfigured ? NotConfiguredText : outcome.ErrorMessage;
                if (IsNotConfigured)
                {
                    PageNotifyService.Warning(NotConfiguredText);
                }

                return;
            }

            var translated = outcome.Items.FirstOrDefault()?.Translation ?? string.Empty;
            turn.Translation = translated.Trim();
            turn.Meta = outcome.Meta ?? string.Empty;
            turn.GlossaryText = BuildGlossaryText(outcome);
            turn.Success = turn.Translation.Length > 0;
            turn.Error = turn.Success ? null : FindString("TranslationPage_NoResultTip_Ai");
        }
        catch (OperationCanceledException)
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                // 用户点了「停止」
                turn.Error = FindString("TranslationPage_Ai_Stopped");
                Core.Log.Info("AI 翻译：本次请求被用户取消");
            }
            else
            {
                // 不是用户取消 → ChatClient 的"太久没有新内容"空闲超时
                turn.Error = FindString("TranslationPage_Timeout");
                Core.Log.Info($"AI 翻译：{turn.Error}（超时设置 {TranslationSettingService.AiEndpoint.TimeoutSeconds} 秒）");
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            turn.Error = ex.Message;
        }
        finally
        {
            turn.IsPending = false;
            _pending.Remove(pending);
            cancellationTokenSource.Dispose();
            NotifyPending();
            SaveSession(session);
            PromoteSession(session);
            session.RefreshSummary();
            OnPropertyChanged(nameof(ContextInfoText));
            ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>停止所有正在翻译的请求（界面上的「停止」）。</summary>
    public void StopAll()
    {
        var pending = _pending.ToList();
        if (pending.Count == 0)
        {
            return;
        }

        Core.Log.Info($"AI 翻译：用户点了停止，取消 {pending.Count} 条在跑的请求");
        foreach (var item in pending)
        {
            item.Source.Cancel();
        }
    }

    /// <summary>清空输入框。</summary>
    public void ClearInput() => InputText = string.Empty;

    private void NotifyPending()
    {
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(PendingText));
        OnPropertyChanged(nameof(ShowSessionEmptyTip));
    }

    private AiChatSessionViewModel CreateSession()
    {
        var session = new AiChatSessionViewModel(new AiTranslationSession(), OnSessionEdited);
        Sessions.Insert(0, session);
        SelectedSession = session;
        return session;
    }

    /// <summary>
    /// 组装上下文：**对话级开关**打开时，把此前所有"翻译成功"的记录按顺序全部带上（不限条数）。
    /// 参数里那条（正在翻的）以及还没出译文的（并发中的）都会被排除。
    /// </summary>
    private static List<string> BuildContext(AiChatSessionViewModel session, AiChatTurnViewModel current)
    {
        if (!session.UseContext)
        {
            return [];
        }

        return session.Turns
            .Where(t => !ReferenceEquals(t, current) && t.CanBeContext)
            .Select(t => $"原文：{t.OriginalPlain}\n译文：{t.TranslationPlain}")
            .ToList();
    }

    private void SaveSession(AiChatSessionViewModel session)
    {
        if (session.Turns.Count == 0)
        {
            return;
        }

        AiTranslationHistoryService.Save(session.Model);
    }

    /// <summary>把刚更新的对话移到列表最前面（"最近的在上"）。</summary>
    private void PromoteSession(AiChatSessionViewModel session)
    {
        var index = Sessions.IndexOf(session);
        if (index > 0)
        {
            Sessions.Move(index, 0);
        }
    }

    /// <summary>对话本身被界面改动（如切换"带上上下文"）时落盘一次。</summary>
    private void OnSessionEdited()
    {
        if (SelectedSession is { } session)
        {
            SaveSession(session);
            OnPropertyChanged(nameof(ContextInfoText));
        }
    }

    private static string BuildGlossaryText(TranslationOutcome outcome)
    {
        var lines = new List<string>();
        if (outcome.GlossaryHits.Count > 0)
        {
            lines.AddRange(outcome.GlossaryHits.Take(30));
            if (outcome.GlossaryHits.Count > 30)
            {
                lines.Add($"…（另有 {outcome.GlossaryHits.Count - 30} 条）");
            }
        }

        if (!string.IsNullOrWhiteSpace(outcome.GlossaryWarning))
        {
            lines.Add(outcome.GlossaryWarning);
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>复制一段文字（界面上每条记录都有自己的复制按钮）。</summary>
    public static void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            PageNotifyService.Warning(FindString("TranslationPage_NoResultTip_Ai"));
            return;
        }

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

    private void OnLanguageChanged(object? sender, string language)
    {
        RefreshLanguageOptions();
        RefreshConfiguration();
        OnPropertyChanged(nameof(NotConfiguredText));
        OnPropertyChanged(nameof(SessionTitle));
        OnPropertyChanged(nameof(ContextInfoText));
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
