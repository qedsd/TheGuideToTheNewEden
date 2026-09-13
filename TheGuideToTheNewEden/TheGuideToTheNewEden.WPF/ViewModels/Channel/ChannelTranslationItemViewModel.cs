using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.ViewModels.Channel;

/// <summary>
/// 频道翻译列表里的一条（包一层 <see cref="ChatTranslationItem"/>）。
/// <para>
/// 与 AI 翻译页的气泡同一套展示思路：原文/译文都过 <see cref="ChatMarkupProtector.ToPlainText"/> 取纯文本、
/// 译文超过五行折叠起来并提供「展开 / 收起」、meta（模型·耗时·token）与术语命中同款展示。
/// 频道列表很长（上限 300 条）且每条都是独立的实时消息，所以原文只用单行预览 + 悬停看全文，
/// 不展开成气泡。
/// </para>
/// </summary>
public sealed class ChannelTranslationItemViewModel : INotifyPropertyChanged
{
    /// <summary>译文气泡每行大约能放多少个"半角字符宽"（列表是整行宽度）。</summary>
    private const double TranslationUnitsPerLine = 100;

    /// <summary>译文折叠时显示几行。</summary>
    private const int TranslationPreviewLines = 5;

    public ChannelTranslationItemViewModel(ChatTranslationItem item)
    {
        Item = item;
        _translationPlain = ChatMarkupProtector.ToPlainText(item.Translation);
        RefreshTranslationPreview();
    }

    public ChatTranslationItem Item { get; }

    public DateTime LocalTime => Item.LocalTime;

    public string Listener => Item.Listener;

    public string Speaker => Item.Speaker;

    public string ChannelName => Item.ChannelName;

    public bool Success => Item.Success;

    public string? Error => Item.Error;

    /// <summary>原文的完整纯文本（悬停提示用）。</summary>
    public string OriginalFull => Item.OriginalFull;

    /// <summary>列表里显示的原文单行预览（去标记、压缩空白、超长截断）。</summary>
    public string OriginalPreview => Item.OriginalPreview;

    /// <summary>译文的纯文本（"复制译文"复制它）。</summary>
    public string Translation => _translationPlain;

    private readonly string _translationPlain;

    private string _translationPreview = string.Empty;
    private bool _canExpandTranslation;
    private bool _isTranslationExpanded;

    /// <summary>译文是否超过五行（超过才显示展开/收起按钮）。</summary>
    public bool CanExpandTranslation => _canExpandTranslation;

    /// <summary>译文是否展开（默认收起：频道列表保持紧凑）。</summary>
    public bool IsTranslationExpanded
    {
        get => _isTranslationExpanded;
        set
        {
            if (_isTranslationExpanded == value)
            {
                return;
            }

            _isTranslationExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TranslationDisplay));
            OnPropertyChanged(nameof(TranslationExpandLabel));
        }
    }

    /// <summary>译文气泡要显示的文本：收起时是"前五行 + …"。</summary>
    public string TranslationDisplay => IsTranslationExpanded || !CanExpandTranslation ? _translationPlain : _translationPreview;

    /// <summary>展开/收起按钮的文字。</summary>
    public string TranslationExpandLabel => FindString(IsTranslationExpanded
        ? "AiTranslation_Chat_Collapse"
        : "AiTranslation_Chat_Expand");

    /// <summary>点击「展开 / 收起」。</summary>
    public void ToggleTranslationExpansion() => IsTranslationExpanded = !IsTranslationExpanded;

    /// <summary>模型/耗时/token（与 AI 翻译页同款）。</summary>
    public string MetaText => Item.Meta;

    public bool HasMeta => !string.IsNullOrEmpty(MetaText);

    /// <summary>本次命中的术语与术语后校验提醒。</summary>
    public string GlossaryText => Item.GlossaryText;

    public bool HasGlossary => !string.IsNullOrEmpty(GlossaryText);

    /// <summary>清洗掉的游戏内标记个数（&gt;0 时界面提示一行）。</summary>
    public string RemovedMarkupText =>
        Item.RemovedMarkup > 0 ? string.Format(FindString("ChannelTranslationPage_RemovedMarkup"), Item.RemovedMarkup) : string.Empty;

    public bool HasRemovedMarkup => Item.RemovedMarkup > 0;

    private void RefreshTranslationPreview()
    {
        var canExpand = TextCollapse.CountLines(_translationPlain, TranslationUnitsPerLine) > TranslationPreviewLines;
        _translationPreview = canExpand
            ? TextCollapse.BuildPreview(_translationPlain, TranslationPreviewLines, TranslationUnitsPerLine)
            : _translationPlain;
        _canExpandTranslation = canExpand;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
