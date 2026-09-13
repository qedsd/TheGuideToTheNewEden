namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>
/// 翻译服务：登记所有可用的翻译源并把请求分发给选中的那一个。
/// <para>
/// 目前只有「本地数据库」一个源（<see cref="LocalDbTranslationProvider"/>，离线）。
/// 接入在线翻译时只需在这里多注册一个 <see cref="ITranslationProvider"/>：
/// 页面上的来源下拉会自动多出一项，无需改动界面与 ViewModel。
/// </para>
/// </summary>
public static class TranslationService
{
    private static readonly ITranslationProvider[] RegisteredProviders =
    [
        new LocalDbTranslationProvider(),
    ];

    /// <summary>全部已注册的翻译源（顺序即界面下拉顺序）。</summary>
    public static IReadOnlyList<ITranslationProvider> Providers => RegisteredProviders;

    /// <summary>本地数据库翻译源（离线专有名词互译）。</summary>
    public static ITranslationProvider LocalDatabase => RegisteredProviders[0];

    /// <summary>按标识取翻译源；找不到时退回第一个（设置里的旧值被删掉时不至于让页面不可用）。</summary>
    public static ITranslationProvider GetProvider(string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            var matched = RegisteredProviders.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal));
            if (matched is not null)
            {
                return matched;
            }
        }

        return RegisteredProviders[0];
    }
}
