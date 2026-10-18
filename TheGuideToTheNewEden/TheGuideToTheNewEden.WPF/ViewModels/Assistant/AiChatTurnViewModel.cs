using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.ViewModels.Assistant;

/// <summary>一条工具执行记录（界面"工具步骤区"的一行）。</summary>
public sealed class AiToolStepViewModel
{
    public AiToolStepViewModel(ToolStep step)
    {
        ToolName = step.Name;
        ParamSummary = step.ParamSummary;
        ResultSummary = step.ResultSummary;
        Success = step.Success;
        ElapsedSeconds = step.ElapsedSeconds;
    }

    public AiToolStepViewModel(AiAssistantStep step)
    {
        ToolName = step.ToolName;
        ParamSummary = step.ParamSummary;
        ResultSummary = step.ResultSummary;
        Success = step.Success;
    }

    /// <summary>英文工具名（本地化键是 AssistantTool_&lt;name&gt;）。</summary>
    public string ToolName { get; }

    /// <summary>界面显示的工具中文名（缺本地化键时退回英文名）。</summary>
    public string DisplayName
    {
        get
        {
            var key = $"AssistantTool_{ToolName}";
            var value = Application.Current?.TryFindResource(key) as string;
            return string.IsNullOrWhiteSpace(value) ? ToolName : value;
        }
    }

    /// <summary>人话参数摘要（如"巨神兵级 @ 伏尔戈"）。</summary>
    public string ParamSummary { get; }

    /// <summary>结果摘要（成功是内容片段，失败是原因）。</summary>
    public string ResultSummary { get; }

    public bool Success { get; }

    public double ElapsedSeconds { get; }

    public bool HasElapsed => ElapsedSeconds > 0;

    public string ElapsedText => $"{ElapsedSeconds:F1}s";

    /// <summary>列表行右侧的状态文字（完成 / 失败）。</summary>
    public string StatusText => Success
        ? (Application.Current?.TryFindResource("AssistantStep_Done") as string ?? "完成")
        : (Application.Current?.TryFindResource("AssistantStep_Failed") as string ?? "失败");
}

/// <summary>
/// 对话里的一条问答记录（请求气泡靠右、回答气泡靠左）。
/// <para>
/// 折叠：请求默认三行、回答默认五行，各自带「展开 / 收起」；新回答默认展开，下一条请求发出时自动收起上一条
/// （被人工点过的记录不再自动收起）。工具执行记录以"步骤区"呈现在回答气泡上方。
/// </para>
/// </summary>
public sealed class AiChatTurnViewModel : INotifyPropertyChanged
{
    /// <summary>请求气泡每行约多少个半角字符宽。</summary>
    private const double UnitsPerLine = 74;

    private const int PreviewLines = 3;

    /// <summary>回答气泡每行约多少个半角字符宽（回答气泡是整行宽度）。</summary>
    private const double AnswerUnitsPerLine = 100;

    private const int AnswerPreviewLines = 5;

    public AiChatTurnViewModel(AiAssistantTurn model, bool isLatest = true)
    {
        Model = model;
        _answerPlain = model.AnswerText ?? string.Empty;
        _isPending = model.AnswerText.Length == 0 && !model.Success && string.IsNullOrEmpty(model.Error);
        foreach (var step in model.Steps)
        {
            Steps.Add(new AiToolStepViewModel(step));
        }

        CanExpandUser = TextCollapse.CountLines(UserText, UnitsPerLine) > PreviewLines;
        _userPreview = CanExpandUser ? TextCollapse.BuildPreview(UserText, PreviewLines, UnitsPerLine) : UserText;
        RefreshAnswerState();
        _isAnswerExpanded = isLatest;
    }

    public AiAssistantTurn Model { get; }

    /// <summary>用户请求原文。</summary>
    public string UserText => Model.UserText;

    /// <summary>请求气泡显示的文本（折叠时前三行 + …）。</summary>
    public string UserDisplay => IsUserExpanded || !CanExpandUser ? UserText : _userPreview;

    private readonly string _userPreview;

    public bool CanExpandUser { get; }

    private bool _isUserExpanded;

    public bool IsUserExpanded
    {
        get => _isUserExpanded;
        set
        {
            if (!Set(ref _isUserExpanded, value))
            {
                return;
            }

            OnPropertyChanged(nameof(UserDisplay));
            OnPropertyChanged(nameof(UserExpandLabel));
        }
    }

    public string UserExpandLabel => FindString(IsUserExpanded ? "Assistant_Chat_Collapse" : "Assistant_Chat_Expand");

    // ---------- 回答折叠 ----------

    private string _answerPlain;
    private bool _canExpandAnswer;
    private bool _isAnswerExpanded;
    private bool _answerExpandTouched;

    public bool CanExpandAnswer => _canExpandAnswer;

    public bool IsAnswerExpanded
    {
        get => _isAnswerExpanded;
        set
        {
            if (!Set(ref _isAnswerExpanded, value))
            {
                return;
            }

            OnPropertyChanged(nameof(AnswerDisplay));
            OnPropertyChanged(nameof(AnswerExpandLabel));
        }
    }

    /// <summary>使用者是否亲手点过回答的展开/收起（点过就不再自动收起）。</summary>
    public bool AnswerExpandTouched => _answerExpandTouched;

    /// <summary>
    /// 回答气泡要显示的 **Markdown 全文**。
    /// <para>
    /// 折叠是**视觉裁剪**（控件 <c>MaxHeight</c> 限制约五行高度）而不是文本截断——回答是 Markdown
    /// （表格/列表/加粗），按字符截断会把表格切断、渲染出错乱的结构。展开即去掉高度限制。
    /// </para>
    /// </summary>
    public string AnswerDisplay => _answerPlain;

    public string AnswerExpandLabel => FindString(IsAnswerExpanded ? "Assistant_Chat_Collapse" : "Assistant_Chat_Expand");

    public string AnswerText => _answerPlain;

    /// <summary>回答正文（流式增量会反复赋值）。</summary>
    public void AppendAnswer(string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }

        _answerPlain += delta;
        Model.AnswerText = _answerPlain;
        RefreshAnswerState();
        OnPropertyChanged(nameof(AnswerText));
        OnPropertyChanged(nameof(AnswerDisplay));
        NotifyState();
    }

    /// <summary>设置回答全文（非流式路径用）。</summary>
    public void SetAnswer(string text)
    {
        if (_answerPlain == text)
        {
            return;
        }

        _answerPlain = text ?? string.Empty;
        Model.AnswerText = _answerPlain;
        RefreshAnswerState();
        OnPropertyChanged(nameof(AnswerText));
        OnPropertyChanged(nameof(AnswerDisplay));
        NotifyState();
    }

    public void ToggleAnswerExpansion()
    {
        _answerExpandTouched = true;
        IsAnswerExpanded = !IsAnswerExpanded;
    }

    /// <summary>自动收起回答（下一条请求发出时调用）：只处理没被人工干预过的、且确实超行的。</summary>
    public void AutoCollapseAnswer()
    {
        if (_answerExpandTouched || !CanExpandAnswer || !IsAnswerExpanded)
        {
            return;
        }

        IsAnswerExpanded = false;
    }

    /// <summary>
    /// 是否超过"约五行"（决定是否显示展开按钮）。按 Markdown 原文估算：表格的每个数据行本身算一行，
    /// 所以表格类回答会正常显示展开按钮；估算偏差由控件的 MaxHeight 裁剪兜底。
    /// </summary>
    private void RefreshAnswerState()
    {
        var canExpand = TextCollapse.CountLines(_answerPlain, AnswerUnitsPerLine) > AnswerPreviewLines;
        if (canExpand == _canExpandAnswer)
        {
            return;
        }

        _canExpandAnswer = canExpand;
        OnPropertyChanged(nameof(CanExpandAnswer));
        OnPropertyChanged(nameof(AnswerDisplay));
    }

    // ---------- 工具步骤 ----------

    /// <summary>本次执行过的工具（按顺序，实时追加）。</summary>
    public ObservableCollection<AiToolStepViewModel> Steps { get; } = [];

    public bool HasSteps => Steps.Count > 0;

    public void AddStep(ToolStep step)
    {
        Steps.Add(new AiToolStepViewModel(step));

        // 阶段 132：步骤**同时**写进落盘模型（此前只进界面，历史文件里一个步骤都没有——
        // 于是重新打开应用后既看不到旧步骤，也没法把工具往返重放给模型）。
        Model.Steps.Add(new AiAssistantStep
        {
            CallId = step.CallId ?? string.Empty,
            Round = step.Round,
            ToolName = step.Name,
            ParamSummary = step.ParamSummary,
            ArgumentsJson = Truncate(step.ArgumentsJson, AssistantContextBuilder.MaxStoredArgumentsChars),
            ResultSummary = step.ResultSummary,
            ResultText = Truncate(step.ResultText, AssistantContextBuilder.MaxStoredResultChars),
            Success = step.Success,
        });

        ProgressText = string.Empty;
        OnPropertyChanged(nameof(HasSteps));
        NotifyState();
    }

    private static string Truncate(string? text, int maxLength)
        => string.IsNullOrEmpty(text) || text.Length <= maxLength ? text ?? string.Empty : text[..maxLength];

    private string _progressText = string.Empty;

    /// <summary>重型工具的实时进度（如"获取源市场订单中 217/411"）。</summary>
    public string ProgressText
    {
        get => _progressText;
        set
        {
            if (Set(ref _progressText, value))
            {
                OnPropertyChanged(nameof(HasProgress));
            }
        }
    }

    public bool HasProgress => !string.IsNullOrEmpty(ProgressText);

    // ---------- 确认挂起 ----------

    private IReadOnlyList<PendingToolAction>? _pendingActions;

    /// <summary>待用户确认的动作（非空时回答气泡里出现确认卡片）。</summary>
    public IReadOnlyList<PendingToolAction>? PendingActions => _pendingActions;

    public bool HasPendingConfirmation => _pendingActions is { Count: > 0 };

    /// <summary>确认卡片里逐条动作的文字。</summary>
    public IReadOnlyList<string> PendingLines => _pendingActions is null
        ? []
        : _pendingActions.Select(a => $"{ToolDisplayName(a.ToolName)}：{a.ParamSummary}").ToList();

    public void SetPending(IReadOnlyList<PendingToolAction>? actions)
    {
        _pendingActions = actions;
        OnPropertyChanged(nameof(PendingActions));
        OnPropertyChanged(nameof(HasPendingConfirmation));
        OnPropertyChanged(nameof(PendingLines));
        NotifyState();
    }

    // ---------- 状态 ----------

    private bool _isPending;

    public bool IsPending
    {
        get => _isPending;
        set
        {
            if (Set(ref _isPending, value))
            {
                NotifyState();
            }
        }
    }

    public bool Success
    {
        get => Model.Success;
        set
        {
            if (Model.Success == value)
            {
                return;
            }

            Model.Success = value;
            OnPropertyChanged();
            NotifyState();
        }
    }

    public bool Stopped
    {
        get => Model.Stopped;
        set
        {
            if (Model.Stopped == value)
            {
                return;
            }

            Model.Stopped = value;
            OnPropertyChanged();
            NotifyState();
        }
    }

    public string? Error
    {
        get => Model.Error;
        set
        {
            if (Model.Error == value)
            {
                return;
            }

            Model.Error = value;
            OnPropertyChanged();
            NotifyState();
        }
    }

    public string Meta
    {
        get => Model.Meta;
        set
        {
            if (Model.Meta == value)
            {
                return;
            }

            Model.Meta = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasMeta));
        }
    }

    public bool HasAnswer => _answerPlain.Length > 0;

    /// <summary>回答气泡是否显示正文（处理中或成功）。</summary>
    public bool ShowAnswerBubble => IsPending || Success;

    /// <summary>处理中且还没有任何内容 → 显示"正在处理…"。</summary>
    public bool ShowPendingHint => IsPending && _answerPlain.Length == 0 && !HasPendingConfirmation;

    /// <summary>处理中（标题行转圈）。</summary>
    public bool ShowPendingBadge => IsPending;

    public bool ShowError => !IsPending && !Success && !Stopped;

    public bool ShowStopped => Stopped;

    public bool HasMeta => !string.IsNullOrEmpty(Meta);

    /// <summary>这条记录能否作为后续问答的上下文（成功的、有回答的）。</summary>
    public bool CanBeContext => Success && _answerPlain.Length > 0;

    public string TimeText => Model.Time.ToString("HH:mm:ss");

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasAnswer));
        OnPropertyChanged(nameof(ShowAnswerBubble));
        OnPropertyChanged(nameof(ShowPendingHint));
        OnPropertyChanged(nameof(ShowPendingBadge));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(ShowStopped));
        OnPropertyChanged(nameof(CanBeContext));
        OnPropertyChanged(nameof(HasPendingConfirmation));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string ToolDisplayName(string toolName)
    {
        var value = Application.Current?.TryFindResource($"AssistantTool_{toolName}") as string;
        return string.IsNullOrWhiteSpace(value) ? toolName : value;
    }

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
