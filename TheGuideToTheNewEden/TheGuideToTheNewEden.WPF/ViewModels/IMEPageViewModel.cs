using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Tools;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.ViewModels;

/// <summary>
/// 输入助手 VM（对应 WinUI 版 <c>Views/Tools/IMEPage</c>）：
/// 在输入框里按分隔符取"当前正在输入的词"，实时给出游戏名称建议（角色/军团/联盟/物品/分组/星系/星域），
/// 点建议即用名称替换该词；另有「复制 / 搜索 / 翻译」三个动作，结果显示在详情区。
/// <para>
/// 与 WinUI 版的差异：① 取词逻辑收进 <see cref="InputTextHelper"/> 并修掉光标在第 0 位时的越界崩溃；
/// ② 搜索收进 <see cref="GameNameSearchService"/>（逐类容错）；③ **翻译不再走有道**——本项目已废弃它，
/// 改为"AI 源可用就用 AI（支持整句），否则用本地词库源（只认 SDE 专有名词）"，并在标题里标明来源；
/// ④ 详情区用只读富文本（可拖选复制）而不是一整块纯文本；
/// ⑤ 建议列表为空时不显示空容器。
/// </para>
/// </summary>
public sealed class IMEPageViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<IdName> _suggestions = new();

    private string _inputText = string.Empty;
    private bool _suppressSuggestions;
    private bool _isDetailVisible;
    private string _detailTitle = string.Empty;
    private string _detailText = string.Empty;
    private int _wordStart = -1;
    private int _wordEnd = -1;
    private CancellationTokenSource? _suggestionCts;

    public IMEPageViewModel()
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>输入内容（多行）。</summary>
    public string InputText
    {
        get => _inputText;
        set
        {
            if (Set(ref _inputText, value ?? string.Empty))
            {
                OnInputChanged();
            }
        }
    }

    public ObservableCollection<IdName> Suggestions => _suggestions;

    public bool HasSuggestions => _suggestions.Count > 0;

    /// <summary>是否在显示详情（详情模式下隐藏输入区）。</summary>
    public bool IsDetailVisible
    {
        get => _isDetailVisible;
        private set
        {
            if (Set(ref _isDetailVisible, value))
            {
                OnPropertyChanged(nameof(IsInputVisible));
            }
        }
    }

    public bool IsInputVisible => !_isDetailVisible;

    public string DetailTitle
    {
        get => _detailTitle;
        private set => Set(ref _detailTitle, value ?? string.Empty);
    }

    /// <summary>详情正文（纯文本，交给只读富文本控件展示，可拖选复制）。</summary>
    public string DetailText
    {
        get => _detailText;
        private set => Set(ref _detailText, value ?? string.Empty);
    }

    /// <summary>点击建议：用它替换当前正在输入的那个词。</summary>
    public void ApplySuggestion(IdName? item)
    {
        if (item is null || string.IsNullOrEmpty(item.Name) || _wordStart < 0 || _wordEnd < _wordStart)
        {
            return;
        }

        if (_wordEnd >= _inputText.Length)
        {
            return;
        }

        var start = _inputText[.._wordStart];
        var end = _inputText[(_wordEnd + 1)..];

        _suppressSuggestions = true;
        try
        {
            InputText = start + item.Name + end;
        }
        finally
        {
            _suppressSuggestions = false;
        }

        ClearSuggestions();
    }

    /// <summary>复制整段输入到剪贴板。</summary>
    public void Copy()
    {
        try
        {
            if (string.IsNullOrEmpty(_inputText))
            {
                return;
            }

            Clipboard.SetDataObject(_inputText, true);
            PageNotifyService.Success(FindString("IMEPage_Copied"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
    }

    /// <summary>搜索（有选中内容就只搜选中部分）。</summary>
    public async Task SearchAsync(string? selection)
    {
        var text = PickText(selection);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var items = await Task.Run(() => GameNameSearchService.Search(text));

        DetailTitle = $"{FindString("IMEPage_Serach")}: {text}";
        DetailText = items.Count == 0
            ? FindString("IMEPage_NoResult")
            : string.Join(Environment.NewLine,
                items.Take(GameNameSearchService.ResultLimit)
                    .Select(p => $"{p.Name}    ({CategoryText(p)} {p.Id})"));

        IsDetailVisible = true;
    }

    /// <summary>翻译（有选中内容就只翻选中部分）。</summary>
    public async Task TranslateAsync(string? selection)
    {
        var text = PickText(selection);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // 源选择与翻译页同一套规则：AI 可用就用 AI（支持整句），否则退回本地词库源（只认专有名词）。
        var aiProvider = TranslationService.Ai;
        var provider = aiProvider.IsAvailable ? aiProvider : TranslationService.LocalDatabase;
        var target = InputTextHelper.IsChinese(text) ? TranslationLanguages.English : TranslationLanguages.Chinese;

        DetailTitle = $"{FindString("IMEPage_Translate")}: {text}";

        try
        {
            var outcome = await provider.TranslateAsync(new TranslationRequest(text, TranslationLanguages.Auto, target));

            if (!outcome.Success)
            {
                DetailText = string.Format(FindString("IMEPage_TranslateFailed"), outcome.ErrorMessage ?? string.Empty);
            }
            else if (outcome.Items.Count == 0)
            {
                DetailText = provider.SupportsFreeText
                    ? FindString("IMEPage_NoTranslation")
                    : FindString("IMEPage_NoResult");
            }
            else if (provider.SupportsFreeText)
            {
                // AI 源：一条整句译文（可能带模型/缓存等补充信息）。
                var item = outcome.Items[0];
                DetailText = string.IsNullOrEmpty(outcome.Meta)
                    ? item.Translation
                    : $"{item.Translation}{Environment.NewLine}{Environment.NewLine}{outcome.Meta}";
            }
            else
            {
                // 本地词库源：若干"原文 → 译文"。
                DetailText = string.Join(Environment.NewLine,
                    outcome.Items.Select(p => $"{p.Query} → {p.Translation}"));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            DetailText = string.Format(FindString("IMEPage_TranslateFailed"), ex.Message);
        }

        IsDetailVisible = true;
    }

    /// <summary>从详情返回输入区。</summary>
    public void CloseDetail()
    {
        IsDetailVisible = false;
        DetailText = string.Empty;
        DetailTitle = string.Empty;
    }

    // ---------- 内部 ----------

    private void OnInputChanged()
    {
        if (_suppressSuggestions)
        {
            return;
        }

        _suggestionCts?.Cancel();

        if (!InputTextHelper.TryGetWordAt(_inputText, _inputText.Length, out var word, out var start, out var end))
        {
            _wordStart = -1;
            _wordEnd = -1;
            ClearSuggestions();
            return;
        }

        _wordStart = start;
        _wordEnd = end;

        var cts = new CancellationTokenSource();
        _suggestionCts = cts;
        _ = LoadSuggestionsAsync(word, cts);
    }

    private async Task LoadSuggestionsAsync(string word, CancellationTokenSource cts)
    {
        try
        {
            var items = await Task.Run(() => GameNameSearchService.SearchSuggestions(word, cts.Token), cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            _suggestions.Clear();
            foreach (var item in items)
            {
                _suggestions.Add(item);
            }

            OnPropertyChanged(nameof(HasSuggestions));
        }
        catch (OperationCanceledException)
        {
            // 输入又变了，丢弃这一轮。
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private void ClearSuggestions()
    {
        if (_suggestions.Count == 0)
        {
            return;
        }

        _suggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
    }

    /// <summary>要处理/翻译的文本：优先选中内容，否则整段输入。</summary>
    private string PickText(string? selection)
        => !string.IsNullOrWhiteSpace(selection) ? selection!.Trim() : _inputText.Trim();

    private static string CategoryText(IdName item) => item.GetCategory().ToString();

    private void OnLanguageChanged(object? sender, string language)
    {
        // 详情标题里带着"搜索/翻译"这些本地化词，换语言后按需重算。
        if (IsDetailVisible && !string.IsNullOrEmpty(_detailTitle))
        {
            var separator = _detailTitle.IndexOf(':');
            if (separator > 0)
            {
                var current = FindString("IMEPage_Serach");
                var translate = FindString("IMEPage_Translate");
                var rest = _detailTitle[(separator + 1)..].Trim();
                DetailTitle = _detailTitle.StartsWith(translate, StringComparison.Ordinal)
                    ? $"{translate}: {rest}"
                    : $"{current}: {rest}";
            }
        }
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
