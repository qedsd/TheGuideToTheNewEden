using TheGuideToTheNewEden.Core.Models.Channel.Translation;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>
/// 频道翻译"逐条翻译时用到的开关"。
/// <para>
/// 这些开关是**按角色**配的（跳过自己 / 只译非中文 / 最短长度 / 带上上下文 / 上下文条数），
/// 而 <see cref="ChatTranslationEngine"/> 是所有角色共用的同一条队列——所以设置**随消息一起入队**
/// （入队时快照），而不是挂在引擎上全局共享：否则多开两个角色时，后启动的那个会把前一个的参数覆盖掉。
/// </para>
/// </summary>
public sealed record ChatTranslationOptions
{
    /// <summary>跳过自己（监听角色自己）的发言。</summary>
    public bool SkipMyself { get; init; } = true;

    /// <summary>只翻译"非中文为主"的消息。</summary>
    public bool OnlyNonChinese { get; init; } = true;

    /// <summary>最短长度（去空白后的字符数）。</summary>
    public int MinLength { get; init; } = 2;

    /// <summary>是否把同一频道里此前已翻译过的几条当上下文一起发给模型。</summary>
    public bool UseContext { get; init; } = true;

    /// <summary>上下文条数上限。</summary>
    public int ContextLimit { get; init; } = 4;

    /// <summary>按角色的频道翻译设置生成一份开关快照。</summary>
    public static ChatTranslationOptions FromSetting(ChannelTranslationSetting setting) => new()
    {
        SkipMyself = setting.SkipMyself,
        OnlyNonChinese = setting.AutoTranslateToZhOnly,
        MinLength = setting.MinLength,
        UseContext = setting.UseContext,
        ContextLimit = setting.ContextLimit,
    };
}
