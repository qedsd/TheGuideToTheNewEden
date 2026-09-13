using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.ViewModels.Translation;

/// <summary>
/// 一个翻译对话（左侧会话列表里的一项）：标题 + 若干条翻译记录。
/// </summary>
public sealed class AiChatSessionViewModel : INotifyPropertyChanged
{
    private readonly Action? _onChanged;

    public AiChatSessionViewModel(AiTranslationSession model, Action? onChanged = null)
    {
        Model = model;
        _onChanged = onChanged;
        for (var i = 0; i < model.Turns.Count; i++)
        {
            // 只有最后一条默认展开译文（其余历史记录默认收起，长对话重新载入时不会铺满屏幕）
            Turns.Add(new AiChatTurnViewModel(model.Turns[i], isLatest: i == model.Turns.Count - 1));
        }
    }

    public AiTranslationSession Model { get; }

    public ObservableCollection<AiChatTurnViewModel> Turns { get; } = [];

    public Guid Id => Model.Id;

    /// <summary>
    /// 本对话是否"每次翻译都带上之前的记录当上下文"（**一个对话只有一个开关**，不是每条一个）。
    /// 打开后不做条数限制。
    /// </summary>
    public bool UseContext
    {
        get => Model.UseContext;
        set
        {
            if (Model.UseContext == value)
            {
                return;
            }

            Model.UseContext = value;
            OnPropertyChanged();
            _onChanged?.Invoke();
        }
    }

    /// <summary>可当上下文的记录数（成功的、有译文的）。</summary>
    public int ContextCandidateCount => Turns.Count(t => t.CanBeContext);

    /// <summary>上下文开关的状态说明（"每次都带上此前 N 条" / "未开启"）。</summary>
    public string ContextInfoText => Model.UseContext
        ? string.Format(FindString("AiTranslation_Chat_ContextOn"), ContextCandidateCount)
        : FindString("AiTranslation_Chat_ContextOff");

    /// <summary>
    /// 对话标题：用户改过就用用户的（<see cref="Model"/> 里的 <c>Title</c>），否则取第一条原文的首行。
    /// 可通过 <see cref="BeginRename"/>/<see cref="CommitRename"/> 重命名。
    /// </summary>
    public string Title
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Model.Title))
            {
                return Model.Title;
            }

            var first = Turns.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.OriginalPlain));
            if (first is null)
            {
                return FindString("AiTranslation_Chat_Untitled");
            }

            return Shorten(first.OriginalPlain, 24);
        }
    }

    private bool _isRenaming;

    /// <summary>是否正在改名（界面在标题位置显示输入框）。</summary>
    public bool IsRenaming
    {
        get => _isRenaming;
        private set => Set(ref _isRenaming, value);
    }

    private string _titleDraft = string.Empty;

    /// <summary>改名输入框绑的草稿（确认时才写回）。</summary>
    public string TitleDraft
    {
        get => _titleDraft;
        set => Set(ref _titleDraft, value);
    }

    /// <summary>开始改名。</summary>
    public void BeginRename()
    {
        TitleDraft = Title;
        IsRenaming = true;
    }

    /// <summary>确认改名（空字符串恢复成"按第一条原文自动取名"）。</summary>
    public void CommitRename()
    {
        var value = (TitleDraft ?? string.Empty).Trim();
        IsRenaming = false;
        if (Model.Title == value)
        {
            return;
        }

        Model.Title = value;
        OnPropertyChanged(nameof(Title));
        _onChanged?.Invoke();
    }

    /// <summary>放弃改名。</summary>
    public void CancelRename() => IsRenaming = false;

    /// <summary>列表副标题：最后一条原文/译文片段（纯文本）。</summary>
    public string Preview
    {
        get
        {
            var last = Turns.LastOrDefault();
            if (last is null)
            {
                return FindString("AiTranslation_Chat_EmptySession");
            }

            return Shorten(last.HasTranslation ? last.TranslationPlain : last.OriginalPlain, 40);
        }
    }

    /// <summary>压成单行并截断（列表里显示用）。</summary>
    private static string Shorten(string? text, int maxLength)
    {
        var line = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (line.Contains("  ", StringComparison.Ordinal))
        {
            line = line.Replace("  ", " ", StringComparison.Ordinal);
        }

        return line.Length <= maxLength ? line : line[..maxLength] + "…";
    }

    public string UpdatedText => Model.UpdatedAt.ToString("MM-dd HH:mm");

    public int TurnCount => Turns.Count;

    public bool IsEmpty => Turns.Count == 0;

    /// <summary>加一条记录（把模型包进 VM 并通知列表刷新）。</summary>
    public AiChatTurnViewModel AddTurn(AiTranslationTurn turn)
    {
        var vm = new AiChatTurnViewModel(turn, isLatest: true);
        Turns.Add(vm);
        Model.Turns.Add(turn);
        RefreshSummary();
        return vm;
    }

    /// <summary>
    /// 新的一条原文发出后：把**之前那些**超过五行、且使用者没手动干预过的译文自动收起来。
    /// 只自动收一次——被使用者点过展开/收起的记录（<see cref="AiChatTurnViewModel.TranslationExpandTouched"/>）会跳过。
    /// </summary>
    public void AutoCollapsePreviousTranslations(AiChatTurnViewModel? except = null)
    {
        foreach (var turn in Turns)
        {
            if (!ReferenceEquals(turn, except))
            {
                turn.AutoCollapseTranslation();
            }
        }
    }

    /// <summary>清空本对话的记录。</summary>
    public void ClearTurns()
    {
        Turns.Clear();
        Model.Turns.Clear();
        Model.UpdatedAt = DateTime.Now;
        RefreshSummary();
    }

    /// <summary>用第一条原文回填 <see cref="AiTranslationSession.Title"/>（只在用户没手动命名时用）。</summary>
    public void ApplyAutoTitle()
    {
        if (!string.IsNullOrWhiteSpace(Model.Title))
        {
            return;
        }

        var first = Turns.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.OriginalPlain));
        if (first is null)
        {
            return;
        }

        Model.Title = Shorten(first.OriginalPlain, 24);
    }

    public void RefreshSummary()
    {
        Model.UpdatedAt = DateTime.Now;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(UpdatedText));
        OnPropertyChanged(nameof(TurnCount));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ContextCandidateCount));
        OnPropertyChanged(nameof(ContextInfoText));
    }

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
