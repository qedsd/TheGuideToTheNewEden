using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>
/// 本地数据库翻译源：用 SDE 主库（英文）与本地化库（中文 zh.db）互译 EVE 专有名词
/// （物品 / 星域 / 星系 / 空间站），完全离线、不发网络请求。
/// <para>
/// 查询实现在 Core 的 <see cref="TranslationDbService"/>（两侧同表按 ID 一一对应，
/// 每类名词只做 2 次查询）。**只支持中英这一对**（SDE 只有中英对照），其它语言组合直接给出失败原因；
/// 源语言为"自动"时按正文脚本判定，若该方向一条都没命中，再自动试另一方向——
/// 中英混排或输入了单个缩写时更不容易"查不到"。
/// </para>
/// </summary>
public sealed class LocalDbTranslationProvider : ITranslationProvider
{
    public const string ProviderKey = "local-db";

    /// <summary>只支持中英互译时的失败原因（界面按这个键找本地化文案）。</summary>
    public const string UnsupportedPairReasonKey = "TranslationPage_Local_OnlyChineseEnglish";

    public string Key => ProviderKey;

    public string DisplayNameKey => "TranslationPage_Source_LocalDb";

    public bool IsAvailable => TranslationDbService.IsAvailable;

    public string? UnavailableReasonKey
        => IsAvailable ? null : "TranslationPage_LocalDbUnavailable";

    /// <summary>纯离线源：可以在输入时防抖自动查询。</summary>
    public bool IsRemote => false;

    /// <summary>只认 SDE 里的专有名词，不能翻译整句。</summary>
    public bool SupportsFreeText => false;

    public Task<TranslationOutcome> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (!IsAvailable)
            {
                return TranslationOutcome.Fail("Local database is not available.");
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return new TranslationOutcome();
            }

            var (from, to) = TranslationLanguageHelper.Resolve(request.From, request.To, request.Text);
            if (!TranslationLanguages.IsChineseEnglishPair(from, to))
            {
                // 界面层会先把这句翻成用户语言再提示；这里是给直接调用者看的兜底文案
                return TranslationOutcome.Fail($"{from} -> {to} is not supported by the local database (Chinese/English only).");
            }

            var sourceIsChinese = from == TranslationLanguages.Chinese;
            var items = TranslationDbService.Search(request.Text, sourceIsChinese);

            // 用户术语表优先级最高：用户固定过的译名直接覆盖 SDE 的译名
            if (UserGlossaryService.Count > 0)
            {
                items = UserGlossaryService.ApplyOverrides(items);
            }

            // 自动方向：原方向没结果就试另一侧（例如输入的是英文但被判成中文，或反之）
            if (items.Count == 0 && TranslationLanguages.Normalize(request.From) == TranslationLanguages.Auto)
            {
                sourceIsChinese = !sourceIsChinese;
                var fallback = TranslationDbService.Search(request.Text, sourceIsChinese);
                if (fallback.Count > 0)
                {
                    items = fallback;
                    from = sourceIsChinese ? TranslationLanguages.Chinese : TranslationLanguages.English;
                    to = sourceIsChinese ? TranslationLanguages.English : TranslationLanguages.Chinese;
                }
            }

            return new TranslationOutcome { From = from, To = to, Items = items };
        }, cancellationToken);
    }
}
