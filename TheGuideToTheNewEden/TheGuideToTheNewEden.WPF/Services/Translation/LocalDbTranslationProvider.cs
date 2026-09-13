using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>
/// 本地数据库翻译源：用 SDE 主库（英文）与本地化库（中文 zh.db）互译 EVE 专有名词
/// （物品 / 星域 / 星系 / 空间站），完全离线、不发网络请求。
/// <para>
/// 查询实现在 Core 的 <see cref="TranslationDbService"/>（两侧同表按 ID 一一对应，
/// 每类名词只做 2 次查询）。<see cref="TranslationDirection.Auto"/> 先按原文是否含中文判定方向，
/// 若该方向一条都没命中，再自动试另一方向——中英混排或输入了单个缩写时更不容易"查不到"。
/// </para>
/// </summary>
public sealed class LocalDbTranslationProvider : ITranslationProvider
{
    public const string ProviderKey = "local-db";

    public string Key => ProviderKey;

    public string DisplayNameKey => "TranslationPage_Source_LocalDb";

    public bool IsAvailable => TranslationDbService.IsAvailable;

    public string? UnavailableReasonKey
        => IsAvailable ? null : "TranslationPage_LocalDbUnavailable";

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

            var direction = TranslationLanguageHelper.Resolve(request.Direction, request.Text);
            var sourceIsChinese = direction == TranslationDirection.ChineseToEnglish;
            var items = TranslationDbService.Search(request.Text, sourceIsChinese);

            // 自动方向：原方向没结果就试另一侧（例如输入的是英文但被判成中文，或反之）
            if (items.Count == 0 && request.Direction == TranslationDirection.Auto)
            {
                sourceIsChinese = !sourceIsChinese;
                var fallback = TranslationDbService.Search(request.Text, sourceIsChinese);
                if (fallback.Count > 0)
                {
                    items = fallback;
                    direction = sourceIsChinese
                        ? TranslationDirection.ChineseToEnglish
                        : TranslationDirection.EnglishToChinese;
                }
            }

            return new TranslationOutcome { Direction = direction, Items = items };
        }, cancellationToken);
    }
}
