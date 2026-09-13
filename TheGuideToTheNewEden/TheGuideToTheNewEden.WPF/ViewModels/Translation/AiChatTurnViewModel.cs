using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.ViewModels.Translation;

/// <summary>
/// 对话里的一条翻译记录（界面上一问一答两个气泡）。
/// <para>
/// 直接包装持久化模型 <see cref="AiTranslationTurn"/>（可写属性直接写回模型，界面改完就能落盘）；
/// <see cref="IsPending"/> 只属于界面（"正在翻译"的气泡），不写进历史文件。
/// </para>
/// <para>
/// 折叠：**原文默认只显示三行**、**译文默认只显示五行**（超出部分省略），各自带「展开 / 收起」按钮。
/// 行数是按"气泡宽度 + 字号"折算的预算（<see cref="UnitsPerLine"/> / <see cref="TranslationUnitsPerLine"/>），
/// 不是按换行符数。新来的译文默认展开（方便看它流式长出来），**下一条原文发出时会自动把上一条译文收起来**
/// ——但如果使用者自己点过展开/收起（<see cref="TranslationExpandTouched"/>），就再也不自动收。
/// </para>
/// </summary>
public sealed class AiChatTurnViewModel : INotifyPropertyChanged
{
    /// <summary>原文气泡每行大约能放多少个"半角字符宽"（约 560px 宽、14px 字号）。</summary>
    private const double UnitsPerLine = 74;

    /// <summary>折叠时显示几行（原文）。</summary>
    private const int PreviewLines = 3;

    /// <summary>译文气泡每行大约能放多少个"半角字符宽"（译文气泡是整行宽度，比原文气泡宽）。</summary>
    private const double TranslationUnitsPerLine = 100;

    /// <summary>折叠时显示几行（译文）。</summary>
    private const int TranslationPreviewLines = 5;

    /// <param name="model">持久化模型。</param>
    /// <param name="isLatest">是不是对话里最新的一条（最新的一条默认展开译文，历史记录默认收起）。</param>
    public AiChatTurnViewModel(AiTranslationTurn model, bool isLatest = true)
    {
        Model = model;
        OriginalPlain = ChatMarkupProtector.ToPlainText(model.Original);
        _translationPlain = ChatMarkupProtector.ToPlainText(model.Translation);
        _isPending = model.Translation.Length == 0 && !model.Success && string.IsNullOrEmpty(model.Error);
        CanExpandOriginal = TextCollapse.CountLines(OriginalPlain, UnitsPerLine) > PreviewLines;
        _preview = CanExpandOriginal ? TextCollapse.BuildPreview(OriginalPlain, PreviewLines, UnitsPerLine) : OriginalPlain;
        RefreshTranslationPreview();
        _isTranslationExpanded = isLatest;
    }

    public AiTranslationTurn Model { get; }

    /// <summary>存盘的原文（可能带游戏内标记，如 <c>&lt;font&gt;</c>/<c>&lt;br&gt;</c>）。</summary>
    public string Original => Model.Original;

    /// <summary>
    /// 原文的**纯文本**形式（走 <see cref="ChatMarkupProtector.ToPlainText"/>：<c>&lt;br&gt;</c> 转换行、其余标记删除）：
    /// 气泡显示、三行折叠判定与"作为上下文发给模型"都用它——存盘仍保留原始文本。
    /// </summary>
    public string OriginalPlain { get; }

    /// <summary>原文气泡要显示的文本：折叠时是"前三行 + …"，展开后是全文（都是纯文本）。</summary>
    public string OriginalDisplay => IsOriginalExpanded || !CanExpandOriginal ? OriginalPlain : _preview;

    private readonly string _preview;

    /// <summary>原文是否超过三行（超过才显示展开按钮）。</summary>
    public bool CanExpandOriginal { get; }

    private bool _isOriginalExpanded;

    /// <summary>原文是否已展开（点击"展开/收起"切换）。</summary>
    public bool IsOriginalExpanded
    {
        get => _isOriginalExpanded;
        set
        {
            if (!Set(ref _isOriginalExpanded, value))
            {
                return;
            }

            OnPropertyChanged(nameof(OriginalDisplay));
            OnPropertyChanged(nameof(ExpandLabel));
        }
    }

    /// <summary>展开按钮的文字（展开/收起）。</summary>
    public string ExpandLabel => FindString(IsOriginalExpanded
        ? "AiTranslation_Chat_Collapse"
        : "AiTranslation_Chat_Expand");

    // ---------- 译文折叠（默认五行） ----------

    private string _translationPreview = string.Empty;
    private bool _canExpandTranslation;
    private bool _isTranslationExpanded;
    private bool _translationExpandTouched;

    /// <summary>译文是否超过五行（超过才显示收起/展开按钮）。</summary>
    public bool CanExpandTranslation => _canExpandTranslation;

    /// <summary>译文是否已展开。</summary>
    public bool IsTranslationExpanded
    {
        get => _isTranslationExpanded;
        set
        {
            if (!Set(ref _isTranslationExpanded, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TranslationDisplay));
            OnPropertyChanged(nameof(TranslationExpandLabel));
        }
    }

    /// <summary>使用者是否亲手点过译文的展开/收起（点过之后就不再自动收起）。</summary>
    public bool TranslationExpandTouched => _translationExpandTouched;

    /// <summary>译文气泡要显示的文本：收起时是"前五行 + …"，展开后是全文（都是纯文本）。</summary>
    public string TranslationDisplay => IsTranslationExpanded || !CanExpandTranslation ? TranslationPlain : _translationPreview;

    /// <summary>译文折叠按钮的文字。</summary>
    public string TranslationExpandLabel => FindString(IsTranslationExpanded
        ? "AiTranslation_Chat_Collapse"
        : "AiTranslation_Chat_Expand");

    /// <summary>点击译文的「展开 / 收起」：记下"人工干预过"，之后不再自动收起。</summary>
    public void ToggleTranslationExpansion()
    {
        _translationExpandTouched = true;
        IsTranslationExpanded = !IsTranslationExpanded;
    }

    /// <summary>
    /// 自动收起译文（下一条原文发出时调用）：**只处理没被人工干预过的、且确实超行的**，
    /// 所以"自动收起"对同一条只会发生一次。
    /// </summary>
    public void AutoCollapseTranslation()
    {
        if (_translationExpandTouched || !CanExpandTranslation || !IsTranslationExpanded)
        {
            return;
        }

        IsTranslationExpanded = false;
    }

    /// <summary>译文换了（含流式增量）就重算预览与"是否需要收起按钮"。</summary>
    private void RefreshTranslationPreview()
    {
        var canExpand = TextCollapse.CountLines(TranslationPlain, TranslationUnitsPerLine) > TranslationPreviewLines;
        _translationPreview = canExpand
            ? TextCollapse.BuildPreview(TranslationPlain, TranslationPreviewLines, TranslationUnitsPerLine)
            : TranslationPlain;

        if (canExpand == _canExpandTranslation)
        {
            return;
        }

        _canExpandTranslation = canExpand;
        OnPropertyChanged(nameof(CanExpandTranslation));
        OnPropertyChanged(nameof(TranslationDisplay));
    }

    public string TimeText => Model.Time.ToString("HH:mm:ss");

    /// <summary>正在翻译（界面在译文位置显示行内加载效果）。</summary>
    private bool _isPending;

    public bool IsPending
    {
        get => _isPending;
        set
        {
            if (!Set(ref _isPending, value))
            {
                return;
            }

            NotifyState();
        }
    }

    /// <summary>译文（流式输出时会被反复赋值；这里存的是模型原始返回）。</summary>
    public string Translation
    {
        get => Model.Translation;
        set
        {
            if (Model.Translation == value)
            {
                return;
            }

            Model.Translation = value;
            _translationPlain = ChatMarkupProtector.ToPlainText(Model.Translation);
            // 流式增量：每次都要重算"是否超过五行"，所以收起按钮会在长出来的过程中出现
            RefreshTranslationPreview();
            OnPropertyChanged();
            OnPropertyChanged(nameof(TranslationPlain));
            OnPropertyChanged(nameof(TranslationDisplay));
            NotifyState();
        }
    }

    private string _translationPlain;

    /// <summary>
    /// 译文的**纯文本**形式（同样走 <see cref="ChatMarkupProtector.ToPlainText"/>）：气泡显示、"复制译文"与
    /// 上下文都用它——模型偶尔会把原文里的标记照抄回来，这里统一挡掉。
    /// </summary>
    public string TranslationPlain => _translationPlain;

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

    /// <summary>失败原因（成功时为 null）。</summary>
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

    /// <summary>模型 / 耗时 / token。</summary>
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

    /// <summary>本次命中的术语（只用于显示，不落盘）。</summary>
    private string _glossaryText = string.Empty;

    public string GlossaryText
    {
        get => _glossaryText;
        set
        {
            if (Set(ref _glossaryText, value))
            {
                OnPropertyChanged(nameof(HasGlossary));
            }
        }
    }

    /// <summary>译文内容非空（流式过程中就会开始为 true）。</summary>
    public bool HasTranslation => TranslationPlain.Length > 0;

    /// <summary>译文气泡是否显示译文文本（生成中或成功）。</summary>
    public bool ShowTranslationBubble => IsPending || Success;

    /// <summary>生成中且还没有任何内容 → 显示"正在翻译…"。</summary>
    public bool ShowPendingHint => IsPending && TranslationPlain.Length == 0;

    /// <summary>生成中（译文标题行旁边的转圈图标 + 小字）。</summary>
    public bool ShowPendingBadge => IsPending;

    public bool ShowError => !IsPending && !Success;

    public bool HasMeta => !string.IsNullOrEmpty(Meta);

    public bool HasGlossary => !string.IsNullOrEmpty(GlossaryText);

    /// <summary>这条记录能否作为后续翻译的上下文（成功的、有译文的）。</summary>
    public bool CanBeContext => Success && TranslationPlain.Length > 0;

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasTranslation));
        OnPropertyChanged(nameof(ShowTranslationBubble));
        OnPropertyChanged(nameof(ShowPendingHint));
        OnPropertyChanged(nameof(ShowPendingBadge));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(CanBeContext));
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
