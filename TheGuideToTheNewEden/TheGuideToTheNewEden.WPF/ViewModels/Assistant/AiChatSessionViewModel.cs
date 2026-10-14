using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.WPF.Services.Assistant;

namespace TheGuideToTheNewEden.WPF.ViewModels.Assistant;

/// <summary>一个助手对话（左侧会话列表里的一项）：标题 + 若干条问答。</summary>
public sealed class AiChatSessionViewModel : INotifyPropertyChanged
{
    private readonly Action? _onChanged;

    public AiChatSessionViewModel(AiAssistantSession model, Action? onChanged = null)
    {
        Model = model;
        _onChanged = onChanged;
        for (var i = 0; i < model.Turns.Count; i++)
        {
            // 只有最后一条默认展开回答
            Turns.Add(new AiChatTurnViewModel(model.Turns[i], isLatest: i == model.Turns.Count - 1));
        }
    }

    public AiAssistantSession Model { get; }

    public ObservableCollection<AiChatTurnViewModel> Turns { get; } = [];

    public Guid Id => Model.Id;

    /// <summary>本对话是否"每次请求都带上此前的问答当上下文"（对话级开关，不限条数）。</summary>
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
            OnPropertyChanged(nameof(ContextInfoText));
            _onChanged?.Invoke();
        }
    }

    /// <summary>可当上下文的记录数（成功的、有回答的）。</summary>
    public int ContextCandidateCount => Turns.Count(t => t.CanBeContext);

    public string ContextInfoText => Model.UseContext
        ? string.Format(FindString("Assistant_Chat_ContextOn"), ContextCandidateCount)
        : FindString("Assistant_Chat_ContextOff");

    /// <summary>对话标题：用户改过就用用户的，否则取第一条请求的首行。</summary>
    public string Title
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Model.Title))
            {
                return Model.Title;
            }

            var first = Turns.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.UserText));
            return first is null ? FindString("Assistant_Chat_Untitled") : Shorten(first.UserText, 24);
        }
    }

    private bool _isRenaming;

    public bool IsRenaming
    {
        get => _isRenaming;
        private set => Set(ref _isRenaming, value);
    }

    private string _titleDraft = string.Empty;

    public string TitleDraft
    {
        get => _titleDraft;
        set => Set(ref _titleDraft, value);
    }

    public void BeginRename()
    {
        TitleDraft = Title;
        IsRenaming = true;
    }

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

    public void CancelRename() => IsRenaming = false;

    /// <summary>列表副标题：最后一条的请求/回答片段。</summary>
    public string Preview
    {
        get
        {
            var last = Turns.LastOrDefault();
            if (last is null)
            {
                return FindString("Assistant_Chat_EmptySession");
            }

            return Shorten(last.HasAnswer ? last.AnswerText : last.UserText, 40);
        }
    }

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

    /// <summary>加一条记录（包成 VM 并通知列表刷新）。</summary>
    public AiChatTurnViewModel AddTurn(AiAssistantTurn turn)
    {
        var vm = new AiChatTurnViewModel(turn, isLatest: true);
        Turns.Add(vm);
        Model.Turns.Add(turn);
        RefreshSummary();
        return vm;
    }

    /// <summary>新请求发出后：把此前超行、且没被人工干预过的回答自动收起来。</summary>
    public void AutoCollapsePreviousAnswers(AiChatTurnViewModel? except = null)
    {
        foreach (var turn in Turns)
        {
            if (!ReferenceEquals(turn, except))
            {
                turn.AutoCollapseAnswer();
            }
        }
    }

    public void ClearTurns()
    {
        Turns.Clear();
        Model.Turns.Clear();
        Model.UpdatedAt = DateTime.Now;
        RefreshSummary();
    }

    /// <summary>用第一条请求回填标题（只在用户没手动命名时用）。</summary>
    public void ApplyAutoTitle()
    {
        if (!string.IsNullOrWhiteSpace(Model.Title))
        {
            return;
        }

        var first = Turns.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.UserText));
        if (first is not null)
        {
            Model.Title = Shorten(first.UserText, 24);
        }
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
