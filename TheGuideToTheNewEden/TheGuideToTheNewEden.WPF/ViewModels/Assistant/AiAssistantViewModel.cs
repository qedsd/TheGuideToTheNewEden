using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Translation.Llm;
using TheGuideToTheNewEden.WPF.Views.Pages;
using TheGuideToTheNewEden.WPF.Views.Pages.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels.Assistant;

/// <summary>
/// 「AI 助手」页的 VM：像 AI 桌面端那样的对话记录界面（左侧多个对话、右侧完整记录 + 底部输入）。
/// <list type="bullet">
///   <item>用户用自然语言提问，助手通过 <see cref="AiAssistantEngine"/> 调用软件功能取真实数据；</item>
///   <item>工具执行以"步骤区"实时呈现在回答气泡上方；<b>写操作</b>（跳桥增删等）会挂起等用户确认；</item>
///   <item>每条请求各自一个取消令牌，等待期间可以继续发下一条（各等各的）；</item>
///   <item>对话历史落盘 <c>Configs/AiAssistantHistory.json</c>；端点复用「AI 翻译」的设置。</item>
/// </list>
/// </summary>
public sealed class AiAssistantViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly List<PendingTurn> _pending = [];
    private TaskCompletionSource<bool>? _confirmation;

    /// <summary>一条正在处理的请求（各自的取消令牌，互不影响）。</summary>
    private sealed record PendingTurn(AiChatTurnViewModel Turn, CancellationTokenSource Source);

    public AiAssistantViewModel()
    {
        foreach (var session in AiAssistantHistoryService.Load())
        {
            Sessions.Add(new AiChatSessionViewModel(session, OnSessionEdited));
        }

        SelectedSession = Sessions.FirstOrDefault();
        RefreshConfiguration();
    }

    /// <summary>界面上需要"滚到底部"时触发（新记录、增量、完成）。</summary>
    public event EventHandler? ScrollToEndRequested;

    // ---------- 配置状态 ----------

    private bool _isConfigured;

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

    public string NotConfiguredText => FindString("Assistant_AiUnavailable");

    // ---------- 对话列表 ----------

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

    /// <summary>选中对话改名后，界面头部绑的是本 VM 的 SessionTitle，需要转发通知。</summary>
    private void OnSelectedSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(AiChatSessionViewModel.Title))
        {
            OnPropertyChanged(nameof(SessionTitle));
        }
    }

    public void BeginRenameSelectedSession() => SelectedSession?.BeginRename();

    public bool HasSession => SelectedSession is not null;

    public bool HasTurns => SelectedSession?.Turns.Count > 0;

    /// <summary>当前对话还没有任何记录时的引导。</summary>
    public bool ShowSessionEmptyTip => IsConfigured && !HasTurns && !HasPending;

    public string SessionTitle => SelectedSession?.Title ?? FindString("Assistant_Chat_NewSession");

    public string ContextInfoText => SelectedSession?.ContextInfoText ?? string.Empty;

    /// <summary>新建一个对话并选中（不落盘，等真的问出一条记录再存）。</summary>
    public void NewSession()
    {
        var session = new AiChatSessionViewModel(new AiAssistantSession(), OnSessionEdited);
        Sessions.Insert(0, session);
        SelectedSession = session;
        NotifySessionList();
    }

    public void DeleteSession(AiChatSessionViewModel? session)
    {
        if (session is null)
        {
            return;
        }

        var index = Sessions.IndexOf(session);
        Sessions.Remove(session);
        AiAssistantHistoryService.Delete(session.Id);

        if (ReferenceEquals(SelectedSession, session))
        {
            SelectedSession = Sessions.Count > 0 ? Sessions[Math.Clamp(index, 0, Sessions.Count - 1)] : null;
        }

        NotifySessionList();
    }

    public void ClearCurrentSession()
    {
        var session = SelectedSession;
        if (session is null)
        {
            return;
        }

        session.ClearTurns();
        AiAssistantHistoryService.Delete(session.Id);
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

    // ---------- 输入与运行状态 ----------

    private string _inputText = string.Empty;

    /// <summary>用户输入（多行；回车=发送、Shift+回车=换行）。等待期间可以继续输入并发送。</summary>
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

    public bool HasPending => _pending.Count > 0;

    public bool IsIdle => !HasPending;

    public string PendingText => HasPending
        ? string.Format(FindString("Assistant_Chat_PendingCount"), _pending.Count)
        : string.Empty;

    // ---------- 生命周期 ----------

    /// <summary>页面每次进入时调用（设置可能刚被改过；也会消费主页快捷输入转来的请求）。</summary>
    public void Init()
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
        Navigation.AssistantQueryRequested -= OnAssistantQueryRequested;
        Navigation.AssistantQueryRequested += OnAssistantQueryRequested;
        RefreshConfiguration();
        OnPropertyChanged(nameof(SessionTitle));
        OnPropertyChanged(nameof(ShowSessionEmptyTip));
        OnPropertyChanged(nameof(ContextInfoText));
        ScrollToEndRequested?.Invoke(this, EventArgs.Empty);

        // 主页快捷输入转来的请求：页面已就绪时走事件路径（上面已处理），这里兜住"页面刚创建"的情况
        if (Navigation.TakePendingAssistantQuery() is { Length: > 0 } pending)
        {
            SubmitQuery(pending);
        }
    }

    /// <summary>离开页面时调用：只退订事件，**不取消正在跑的请求**（页面是缓存的，回来时结果还在）。</summary>
    public void Dispose()
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
        Navigation.AssistantQueryRequested -= OnAssistantQueryRequested;
    }

    /// <summary>
    /// 主页快捷输入转来的请求（助手页已就绪时立即处理）。
    /// 与 <see cref="Init"/> 的待办消费**互斥**：谁先取到待办谁处理，避免同一条请求发两次。
    /// </summary>
    private void OnAssistantQueryRequested(string query)
    {
        if (Navigation.TakePendingAssistantQuery() is not { Length: > 0 } pending)
        {
            return;
        }

        SubmitQuery(pending);
    }

    public void RefreshConfiguration()
    {
        var endpoint = TranslationSettingService.AiEndpoint;
        IsConfigured = endpoint.IsConfigured;
        EndpointSummary = IsConfigured
            ? $"{FindString(ChatProtocolFactory.Get(endpoint.Protocol).DisplayNameKey)} · {endpoint.Model}"
            : string.Empty;

        OnPropertyChanged(nameof(NotConfiguredText));
        OnPropertyChanged(nameof(ShowSessionEmptyTip));
    }

    /// <summary>打开「设置 → AI 翻译」（助手复用同一份端点设置）。</summary>
    public void OpenSettings()
    {
        Navigation.Navigate(typeof(SettingsPage));
        SettingsPage.RequestCategory(typeof(AiTranslationSettingPage));
        Navigation.Activate();
    }

    // ---------- 发送 ----------

    /// <summary>
    /// 发起一次请求（回车 / 「发送」）。**不等前一条结束**：每条各自一个请求、各自一个转圈；
    /// 工具轮非流式执行（步骤区实时追加），最终回答流式长进气泡。
    /// </summary>
    public async Task SendAsync()
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
            PageNotifyService.Warning(FindString("Assistant_Chat_NoInput"));
            return;
        }

        var session = SelectedSession ?? CreateSession();
        var turn = session.AddTurn(new AiAssistantTurn
        {
            Time = DateTime.Now,
            UserText = text,
            Success = false,
        });
        turn.IsPending = true;
        session.ApplyAutoTitle();
        session.AutoCollapsePreviousAnswers(turn);

        InputText = string.Empty;
        NotifySessionList();
        ScrollToEndRequested?.Invoke(this, EventArgs.Empty);

        var cancellationTokenSource = new CancellationTokenSource();
        var pending = new PendingTurn(turn, cancellationTokenSource);
        _pending.Add(pending);
        NotifyPending();

        var started = DateTime.UtcNow;
        AiAssistantEngine.Turn? engineTurn = null;
        try
        {
            // 每次发送按当前设置建引擎（工具轮数改了不必重启）
            var engine = new AiAssistantEngine(AiAssistantSettingService.MaxToolRounds);
            var context = new AssistantContext
            {
                Context = BuildContext(session, turn),
                SystemPrompt = string.IsNullOrWhiteSpace(AiAssistantSettingService.SystemPrompt)
                    ? null
                    : AiAssistantSettingService.SystemPrompt,
                Tools = new ToolContext
                {
                    Character = ResolveDefaultCharacter(),
                    Characters = CharacterStore.Characters.ToList(),
                    OnUiThread = action => Application.Current?.Dispatcher.Invoke(action),
                    Progress = progressText => turn.ProgressText = progressText,
                },
            };

            engineTurn = await engine.SendAsync(text, context, cancellationTokenSource.Token, turn.AddStep);

            if (engineTurn.UserStopped)
            {
                turn.Stopped = true;
                return;
            }

            if (engineTurn.Error is not null)
            {
                turn.Error = engineTurn.Error;
                return;
            }

            // 写操作挂起：默认显示确认卡片等用户点「应用 / 拒绝」；
            // 设置里关掉"确认写操作"时**不是免确认**，而是直接拒绝并说明（写操作永远需要显式确认）。
            if (engineTurn.HasPendingConfirmation)
            {
                if (!AiAssistantSettingService.ConfirmExpensive)
                {
                    PageNotifyService.Warning(FindString("Assistant_Chat_ConfirmDisabled"));
                    await engineTurn.ResumeAsync(false, cancellationTokenSource.Token);
                }
                else
                {
                    turn.SetPending(engineTurn.PendingConfirmations);
                    ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
                    var apply = await WaitForConfirmationAsync(cancellationTokenSource.Token);
                    turn.SetPending(null);

                    await engineTurn.ResumeAsync(apply, cancellationTokenSource.Token);
                }

                if (engineTurn.UserStopped)
                {
                    turn.Stopped = true;
                    return;
                }

                if (engineTurn.Error is not null)
                {
                    turn.Error = engineTurn.Error;
                    return;
                }
            }

            // 流式回答：按 ~120ms 批量刷新（回答是 Markdown，每个 token 都重建 FlowDocument 太吃力），并跟随滚动
            var buffer = new System.Text.StringBuilder();
            var lastFlush = DateTime.UtcNow;
            await foreach (var delta in engineTurn.ReadFinalAsync(cancellationTokenSource.Token))
            {
                buffer.Append(delta);
                if ((DateTime.UtcNow - lastFlush).TotalMilliseconds < 120)
                {
                    continue;
                }

                turn.AppendAnswer(buffer.ToString());
                buffer.Clear();
                lastFlush = DateTime.UtcNow;
                ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
            }

            if (buffer.Length > 0)
            {
                turn.AppendAnswer(buffer.ToString());
            }

            turn.Success = turn.AnswerText.Length > 0;
            if (!turn.Success)
            {
                turn.Error = FindString("Assistant_Chat_AnswerEmpty");
            }
        }
        catch (OperationCanceledException)
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                turn.Stopped = true;
                Core.Log.Info("AI 助手：本次请求被用户取消");
            }
            else
            {
                turn.Error = FindString("Assistant_Chat_Timeout");
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
            turn.ProgressText = string.Empty;
            turn.SetPending(null);
            _pending.Remove(pending);
            cancellationTokenSource.Dispose();
            NotifyPending();

            if (engineTurn is not null)
            {
                turn.Meta = BuildMeta(engineTurn, started);
            }

            SaveSession(session);
            PromoteSession(session);
            session.RefreshSummary();
            OnPropertyChanged(nameof(ContextInfoText));
            ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>外部注入一条请求并立即发送（主页快捷输入的 handoff 用）。</summary>
    public void SubmitQuery(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        InputText = text;
        _ = SendAsync();
    }

    /// <summary>停止所有正在处理的请求。</summary>
    public void StopAll()
    {
        var pending = _pending.ToList();
        if (pending.Count == 0)
        {
            return;
        }

        Core.Log.Info($"AI 助手：用户点了停止，取消 {pending.Count} 条在跑的请求");
        foreach (var item in pending)
        {
            item.Source.Cancel();
        }

        _confirmation?.TrySetCanceled();
    }

    /// <summary>界面确认卡片：应用 / 拒绝挂起的写操作。</summary>
    public void ResolveConfirmation(bool apply) => _confirmation?.TrySetResult(apply);

    public void ClearInput() => InputText = string.Empty;

    private async Task<bool> WaitForConfirmationAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _confirmation = tcs;
        using var registration = ct.Register(() => tcs.TrySetCanceled(ct));
        try
        {
            return await tcs.Task;
        }
        finally
        {
            _confirmation = null;
        }
    }

    private void NotifyPending()
    {
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(PendingText));
        OnPropertyChanged(nameof(ShowSessionEmptyTip));
    }

    private AiChatSessionViewModel CreateSession()
    {
        var session = new AiChatSessionViewModel(new AiAssistantSession(), OnSessionEdited);
        Sessions.Insert(0, session);
        SelectedSession = session;
        return session;
    }

    /// <summary>按设置解析默认角色：设置里指定了角色名就用它，否则用第一个角色。</summary>
    private static AuthorizedCharacterData? ResolveDefaultCharacter()
    {
        var configured = AiAssistantSettingService.DefaultCharacter;
        if (!string.IsNullOrWhiteSpace(configured)
            && ToolNameResolver.ResolveCharacter(configured) is { } matched)
        {
            return matched;
        }

        return CharacterStore.Characters.FirstOrDefault();
    }

    /// <summary>组装上下文（对话级开关打开时带上此前所有成功问答）。</summary>
    private static List<(string Question, string Answer)> BuildContext(AiChatSessionViewModel session, AiChatTurnViewModel current)
    {
        if (!session.UseContext)
        {
            return [];
        }

        return session.Turns
            .Where(t => !ReferenceEquals(t, current) && t.CanBeContext)
            .Select(t => (t.UserText, t.AnswerText))
            .ToList();
    }

    private static string BuildMeta(AiAssistantEngine.Turn turn, DateTime started)
    {
        var elapsed = (DateTime.UtcNow - started).TotalSeconds;
        var usage = turn.TotalUsage.IsEmpty
            ? "未返回用量"
            : $"输入 {turn.TotalUsage.InputTokens} / 输出 {turn.TotalUsage.OutputTokens} token";
        return $"{TranslationSettingService.AiEndpoint.Model} · {elapsed:F1}s · {usage} · 工具 {turn.Steps.Count} 次";
    }

    private void SaveSession(AiChatSessionViewModel session)
    {
        if (session.Turns.Count == 0)
        {
            return;
        }

        AiAssistantHistoryService.Save(session.Model);
    }

    private void PromoteSession(AiChatSessionViewModel session)
    {
        var index = Sessions.IndexOf(session);
        if (index > 0)
        {
            Sessions.Move(index, 0);
        }
    }

    private void OnSessionEdited()
    {
        if (SelectedSession is { } session)
        {
            SaveSession(session);
            OnPropertyChanged(nameof(ContextInfoText));
        }
    }

    /// <summary>复制一段文字。</summary>
    public static void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            PageNotifyService.Warning(FindString("Assistant_Chat_AnswerEmpty"));
            return;
        }

        try
        {
            Clipboard.SetText(text);
            PageNotifyService.Success(FindString("Assistant_Chat_CopySuccess"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private void OnLanguageChanged(object? sender, string language)
    {
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
