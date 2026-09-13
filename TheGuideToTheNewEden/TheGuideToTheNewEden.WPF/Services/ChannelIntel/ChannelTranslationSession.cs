using TheGuideToTheNewEden.Core.Models.Channel.Translation;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>
/// 一个角色的频道翻译会话（对齐 WinUI 版 <c>Models/ChannelTranslation</c>）：
/// 为每个勾选频道创建 Core 的 <see cref="ChannelTranslationObserver"/>（增量读聊天日志、
/// 按触发关键词过滤），命中后把消息排进共享的 <see cref="ChatTranslationEngine"/> 串行翻译。
/// </summary>
public sealed class ChannelTranslationSession
{
    private readonly List<ChannelTranslationObserver> _observers = [];

    public ChannelTranslationSetting Setting { get; }

    public bool Running { get; private set; }

    public ChannelTranslationSession(string characterName, ChatTranslationEngine? engine = null)
    {
        Engine = engine ?? ChatTranslationEngine.Current;
        Setting = ChannelTranslationSettingService.GetValue(characterName)
            ?? new ChannelTranslationSetting { CharacterName = characterName };
    }

    /// <summary>共享的翻译引擎（单条队列串行翻译）。</summary>
    public ChatTranslationEngine Engine { get; }

    /// <summary>页面勾选的频道（日志文件路径）。</summary>
    public void SetSelectedChannels(IEnumerable<string> paths)
        => Setting.Channels = paths.ToList();

    public void Start()
    {
        Stop();

        // 跳过自己/只译非中文/最短长度/上下文等开关是**按角色**的：每次入队带着当前设置的快照，
        // 这样多开几个角色时不会互相覆盖（引擎是全角色共用的一条队列）
        Engine.SkipMyself = Setting.SkipMyself;
        Engine.OnlyNonChinese = Setting.AutoTranslateToZhOnly;
        Engine.MinLength = Setting.MinLength;
        Engine.UseContext = Setting.UseContext;
        Engine.ContextLimit = Setting.ContextLimit;
        Engine.Start();

        foreach (var path in Setting.Channels)
        {
            var observer = new ChannelTranslationObserver(path, Setting.CharacterName, Setting.Keyword);
            if (Core.Services.ObservableFileService.Add(observer))
            {
                observer.OnContentUpdate += Observer_OnContentUpdate;
                _observers.Add(observer);

                // 观察者只读"启动之后"的新内容，因此先补翻日志尾部：加入频道时写下的
                // 「频道置顶信息（MOTD）」以及最近几句都能立刻看到译文（同文本会命中 AI 结果缓存）
                var recent = ChannelChatLogReader.ReadRecent(path, Setting.CharacterName, Setting.Keyword);
                if (recent.Count > 0)
                {
                    Engine.Enqueue(Setting.CharacterName, recent, Options);
                }
            }
        }

        Save();
        Running = true;
    }

    private void Observer_OnContentUpdate(object? sender, IEnumerable<Core.Models.EVELogs.ChatContent> news)
    {
        // 观察者已按触发关键词决定 Important（未设关键词时全部为 true）
        var hits = news.Where(p => p.Important).ToList();
        if (hits.Count > 0)
        {
            Engine.Enqueue(Setting.CharacterName, hits, Options);
        }
    }

    /// <summary>当前角色的开关快照（随消息入队，改动对下一条生效）。</summary>
    private ChatTranslationOptions Options => ChatTranslationOptions.FromSetting(Setting);

    public void Stop()
    {
        Core.Services.ObservableFileService.Remove(_observers);
        _observers.Clear();
        Running = false;
    }

    public void Save() => ChannelTranslationSettingService.SetValue(Setting);
}
