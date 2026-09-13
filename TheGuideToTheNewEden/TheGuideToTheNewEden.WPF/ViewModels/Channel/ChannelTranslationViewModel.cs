using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.ViewModels.Channel;

/// <summary>
/// 频道翻译页：左侧角色列表（每角色一个 <see cref="ChannelTranslationSession"/>），
/// 中间勾选要翻译的频道与参数（跳过自己 / 只译非中文 / 最短长度 / 触发关键词），
/// 右侧实时译文列表（新消息在最上）。
/// <para>
/// 翻译由共享的 <see cref="ChatTranslationEngine"/> 串行执行（AI 源按次计费且易限流），
/// 页面与"弹窗"共用同一份译文集合，因此弹窗里看到的与页面完全一致。
/// </para>
/// </summary>
public sealed class ChannelTranslationViewModel : INotifyPropertyChanged
{
    /// <summary>译文列表上限（超出丢弃最旧的）。</summary>
    private const int MaxItems = 300;

    private readonly string _logPath;
    private readonly Dictionary<string, List<ChatChanelInfoItem>> _listenerChannelDic = [];
    private readonly Dictionary<string, ChannelTranslationSession> _sessions = [];

    public ObservableCollection<ChannelIntelListener> Characters { get; } = [];

    /// <summary>实时译文（新→旧）。条目是 <see cref="ChannelTranslationItemViewModel"/>（带折叠/meta 的展示包装）。</summary>
    public ObservableCollection<ChannelTranslationItemViewModel> Items { get; } = [];

    public ChatTranslationEngine Engine => ChatTranslationEngine.Current;

    private ChannelIntelListener? _selectedCharacter;

    public ChannelIntelListener? SelectedCharacter
    {
        get => _selectedCharacter;
        set
        {
            if (Set(ref _selectedCharacter, value))
            {
                UpdateSelectedCharacter();
                OnPropertyChanged(nameof(HasSession));
                NotifyRunningState();
            }
        }
    }

    public bool HasSession => SelectedCharacter is not null;

    /// <summary>选中角色正在翻译。</summary>
    public bool SelectedRunning => SelectedCharacter?.Running ?? false;

    /// <summary>选中角色未在翻译（开始按钮显隐）。必须与 <see cref="SelectedRunning"/> 成对通知。</summary>
    public bool SelectedNotRunning => !SelectedRunning;

    /// <summary>任意角色在跑（"停止全部"显隐）。</summary>
    public bool AnyRunning => Characters.Any(p => p.Running);

    private List<ChatChanelInfoItem> _chatChanelInfos = [];

    public List<ChatChanelInfoItem> ChatChanelInfos
    {
        get => _chatChanelInfos;
        private set => Set(ref _chatChanelInfos, value);
    }

    private ChannelTranslationSession? _session;

    public ChannelTranslationSession? Session
    {
        get => _session;
        private set
        {
            if (!Set(ref _session, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSession));
            OnPropertyChanged(nameof(SkipMyself));
            OnPropertyChanged(nameof(OnlyNonChinese));
            OnPropertyChanged(nameof(MinLength));
            OnPropertyChanged(nameof(Keyword));
        }
    }

    /// <summary>AI 源是否已配置（未配置时页面顶部给黄条提示）。</summary>
    public bool IsAiUnavailable => !TranslationService.Ai.IsAvailable;

    public string AiUnavailableText => FindString("TranslationPage_AiUnavailable");

    // ---------- 参数（改完即落到当前角色的设置） ----------

    public bool SkipMyself
    {
        get => Session?.Setting.SkipMyself ?? true;
        set
        {
            if (Session is null || Session.Setting.SkipMyself == value)
            {
                return;
            }

            Session.Setting.SkipMyself = value;
            Session.Engine.SkipMyself = value;
            Session.Save();
            OnPropertyChanged();
        }
    }

    public bool OnlyNonChinese
    {
        get => Session?.Setting.AutoTranslateToZhOnly ?? true;
        set
        {
            if (Session is null || Session.Setting.AutoTranslateToZhOnly == value)
            {
                return;
            }

            Session.Setting.AutoTranslateToZhOnly = value;
            Session.Engine.OnlyNonChinese = value;
            Session.Save();
            OnPropertyChanged();
        }
    }

    public double? MinLength
    {
        get => Session?.Setting.MinLength ?? 2;
        set
        {
            if (Session is null)
            {
                return;
            }

            var clamped = (int)Math.Clamp(value ?? 2, 1, 200);
            if (Session.Setting.MinLength == clamped)
            {
                return;
            }

            Session.Setting.MinLength = clamped;
            Session.Engine.MinLength = clamped;
            Session.Save();
            OnPropertyChanged();
        }
    }

    public string Keyword
    {
        get => Session?.Setting.Keyword ?? string.Empty;
        set
        {
            if (Session is null || Session.Setting.Keyword == value)
            {
                return;
            }

            Session.Setting.Keyword = value;
            Session.Save();
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 是否把同一频道里此前已翻译过的几条一起发给模型当上下文（跟 AI 翻译页的"带上上下文"同一个思路，
    /// 频道场景默认开：一句话经常依赖前文）。
    /// </summary>
    public bool UseContext
    {
        get => Session?.Setting.UseContext ?? true;
        set
        {
            if (Session is null || Session.Setting.UseContext == value)
            {
                return;
            }

            Session.Setting.UseContext = value;
            Session.Engine.UseContext = value;
            Session.Save();
            OnPropertyChanged();
        }
    }

    /// <summary>上下文条数上限。</summary>
    public double? ContextLimit
    {
        get => Session?.Setting.ContextLimit ?? 4;
        set
        {
            if (Session is null)
            {
                return;
            }

            var clamped = (int)Math.Clamp(value ?? 4, 1, 20);
            if (Session.Setting.ContextLimit == clamped)
            {
                return;
            }

            Session.Setting.ContextLimit = clamped;
            Session.Engine.ContextLimit = clamped;
            Session.Save();
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 当前翻译方向（只读展示）：跟着「AI 翻译」页的源/目标语言设置走——一个地方配，两处生效。
    /// </summary>
    public string DirectionText
    {
        get
        {
            var target = TranslationLanguages.Normalize(TranslationSettingService.AiTo);
            if (target == TranslationLanguages.Auto)
            {
                return string.Format(
                    FindString("ChannelTranslationPage_DirectionAuto"),
                    FindString(TranslationLanguages.DisplayKey(TranslationSettingService.AiPairA)),
                    FindString(TranslationLanguages.DisplayKey(TranslationSettingService.AiPairB)));
            }

            return $"{FindString(TranslationLanguages.DisplayKey(TranslationSettingService.AiFrom))} → {FindString(TranslationLanguages.DisplayKey(target))}";
        }
    }

    // ---------- AI 未配置：整页引导（与「AI 翻译」页一致） ----------

    /// <summary>AI 源是否已配置（未配置时整页只显示引导）。</summary>
    public bool IsConfigured => TranslationService.Ai.IsAvailable;

    public bool IsNotConfigured => !IsConfigured;

    /// <summary>重新检测配置（从设置页回来、或改了 settings.json 时点一下）。</summary>
    public void RecheckConfiguration()
    {
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(IsNotConfigured));
        OnPropertyChanged(nameof(IsAiUnavailable));
        OnPropertyChanged(nameof(DirectionText));
    }

    /// <summary>打开「设置 → AI 翻译」。</summary>
    public void OpenAiSettings()
    {
        Navigation.Navigate(typeof(Views.Pages.SettingsPage));
        Views.Pages.SettingsPage.RequestCategory(typeof(Views.Pages.Settings.AiTranslationSettingPage));
        Navigation.Activate();
    }

    private string _statusText = string.Empty;

    /// <summary>状态行（已翻译条数 / 队列长度）。</summary>
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    /// <summary>还没有任何译文（结果区显示引导文案）。</summary>
    public bool IsEmpty => Items.Count == 0;

    public ChannelTranslationViewModel()
    {
        _logPath = GameLogsSettingService.GetChatlogsPath();
        ChatTranslationEngine.Current.Translated += OnTranslated;
        _ = InitDicAsync();
    }

    private async Task InitDicAsync()
    {
        var dic = new Dictionary<string, List<ChatChanelInfoItem>>();
        if (Directory.Exists(_logPath))
        {
            await Task.Run(() =>
            {
                var result = GameLogHelper.GetChatChanelInfos(_logPath, Math.Max(1, GameLogsSettingService.EVELogsChannelDurationValue));
                if (result is not null)
                {
                    foreach (var item in result)
                    {
                        dic[item.Key] = item.Value.Select(p => new ChatChanelInfoItem(p)).ToList();
                    }
                }
            });
        }

        Characters.Clear();
        foreach (var key in dic.Keys)
        {
            Characters.Add(new ChannelIntelListener(key));
        }

        _listenerChannelDic.Clear();
        foreach (var pair in dic)
        {
            _listenerChannelDic[pair.Key] = pair.Value;
        }
    }

    public async Task RefreshAsync()
    {
        await InitDicAsync();
        NotifyRunningState();
    }

    private void UpdateSelectedCharacter()
    {
        if (SelectedCharacter is null)
        {
            Session = null;
            ChatChanelInfos = [];
            return;
        }

        Session = GetSession(SelectedCharacter.Name);
        ChatChanelInfos = _listenerChannelDic.TryGetValue(SelectedCharacter.Name, out var list) ? list : [];

        if (Session.Setting.Channels is { Count: > 0 })
        {
            foreach (var info in ChatChanelInfos)
            {
                info.IsChecked = Session.Setting.Channels.Contains(info.Info.FilePath);
            }
        }
        else
        {
            ChatChanelInfos.ForEach(p => p.IsChecked = false);
        }

        // 引擎开关按当前角色的设置同步
        Session.Engine.SkipMyself = Session.Setting.SkipMyself;
        Session.Engine.OnlyNonChinese = Session.Setting.AutoTranslateToZhOnly;
        Session.Engine.MinLength = Session.Setting.MinLength;
        Session.Engine.UseContext = Session.Setting.UseContext;
        Session.Engine.ContextLimit = Session.Setting.ContextLimit;

        OnPropertyChanged(nameof(UseContext));
        OnPropertyChanged(nameof(ContextLimit));
        OnPropertyChanged(nameof(DirectionText));
        RecheckConfiguration();
    }

    private ChannelTranslationSession GetSession(string name)
    {
        if (_sessions.TryGetValue(name, out var session))
        {
            return session;
        }

        var created = new ChannelTranslationSession(name);
        _sessions.Add(name, created);
        return created;
    }

    public void StartSelected()
    {
        if (Session is null || SelectedCharacter is null)
        {
            PageNotifyService.Error(FindString("General_CharacterUnselected"));
            return;
        }

        if (!TranslationService.Ai.IsAvailable)
        {
            PageNotifyService.Warning(FindString("TranslationPage_AiUnavailable"));
            return;
        }

        Session.SetSelectedChannels(ChatChanelInfos.Where(p => p.IsChecked).Select(p => p.Info.FilePath));
        if (Session.Setting.Channels is not { Count: > 0 })
        {
            PageNotifyService.Error(FindString("ChannelTranslationPage_NoChannel"));
            return;
        }

        Session.Start();
        SelectedCharacter.Running = true;
        NotifyRunningState();
        PageNotifyService.Success(FindString("ChannelTranslationPage_Started"));
    }

    public void StartAll()
    {
        var previous = SelectedCharacter;
        foreach (var character in Characters)
        {
            SelectedCharacter = character;
            StartSelected();
        }

        SelectedCharacter = previous;
    }

    public void StopSelected()
    {
        if (Session is null || SelectedCharacter is null)
        {
            return;
        }

        Session.Stop();
        SelectedCharacter.Running = false;
        NotifyRunningState();
        PageNotifyService.Info(FindString("ChannelTranslationPage_Stopped"));
    }

    public void StopAll()
    {
        foreach (var session in _sessions.Values)
        {
            session.Stop();
        }

        foreach (var character in Characters)
        {
            character.Running = false;
        }

        Engine.Stop();
        NotifyRunningState();
    }

    public void ClearItems()
    {
        Items.Clear();
        Engine.ClearPending();
        RefreshStatus();
    }

    private void OnTranslated(object? sender, ChatTranslationItem item)
    {
        // 回调来自翻译线程：切回 UI 线程再动集合
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            AddItem(item);
        }
        else
        {
            dispatcher.BeginInvoke(new Action(() => AddItem(item)));
        }
    }

    private void AddItem(ChatTranslationItem item)
    {
        Items.Insert(0, new ChannelTranslationItemViewModel(item));
        while (Items.Count > MaxItems)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        RefreshStatus();
    }

    private void RefreshStatus()
    {
        StatusText = string.Format(
            FindString("ChannelTranslationPage_Status"),
            Engine.ProcessedCount,
            Items.Count);
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void NotifyRunningState()
    {
        OnPropertyChanged(nameof(SelectedRunning));
        OnPropertyChanged(nameof(SelectedNotRunning));
        OnPropertyChanged(nameof(AnyRunning));
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
