using System.Windows;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// ESI 授权回调的地址解析、等待与 code 解析。
/// </summary>
/// <remarks>
/// 方案是**本地回环**（<see cref="LoopbackAuthServer"/>）：浏览器把回调直接打进主实例，
/// 不产生第二个进程、不依赖注册表。
/// </remarks>
internal static class AuthHelper
{
    /// <summary>等待回调的上限；超时按"没收到回调"处理，避免授权流程永久挂起。</summary>
    public static TimeSpan CallbackTimeout { get; set; } = TimeSpan.FromMinutes(5);

    #region 回环回调（现行方案）

    /// <summary>
    /// 建议使用的回调端口。
    /// 端口必须与 EVE 开发者后台登记的 Callback URL **完全一致**（CCP 不支持通配端口），
    /// 所以真正的取值来自 <c>Configs/ESILicense.txt</c> 第 2 行，这里只是给配置时用的建议值。
    /// </summary>
    public const int DefaultCallbackPort = 38471;

    /// <summary>建议使用的回调路径。</summary>
    public const string DefaultCallbackPath = "/callback/";

    /// <summary>建议在开发者后台与 <c>ESILicense.txt</c> 里填写的完整回调地址。</summary>
    public static string SuggestedCallbackUrl => $"http://localhost:{DefaultCallbackPort}{DefaultCallbackPath}";

    /// <summary>当前配置的回调地址；未配置时返回建议值（用于界面展示与复制）。</summary>
    public static string GetCallbackUrlForDisplay()
    {
        var configured = Core.Config.ESICallback?.Trim();
        return string.IsNullOrWhiteSpace(configured) ? SuggestedCallbackUrl : configured;
    }

    /// <summary>
    /// 解析并校验回环回调地址（即 <c>Configs/ESILicense.txt</c> 第 2 行 → <see cref="Core.Config.ESICallback"/>）。
    /// </summary>
    /// <remarks>
    /// 校验失败**不能**照常打开授权页：授权 URL 里的 <c>redirect_uri</c> 就是这里取的值，
    /// 与开发者后台登记不一致时 CCP 会直接拒绝授权，用户在浏览器里看到的是英文报错页，
    /// 比"提前失败并说清怎么改"难排查得多。
    /// </remarks>
    public static bool TryGetLoopbackEndpoint(out Uri endpoint, out string? error)
    {
        endpoint = null!;

        var raw = Core.Config.ESICallback?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = EndpointGuidance("尚未配置回调地址（Configs/ESILicense.txt 第 2 行为空）");
            return false;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            error = EndpointGuidance($"回调地址无法解析：{raw}");
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp)
        {
            error = EndpointGuidance($"回调地址必须使用 http（本地回环不允许 https）：{raw}");
            return false;
        }

        if (!IsLoopbackHost(uri.Host))
        {
            error = EndpointGuidance($"回调地址必须指向本机回环地址：{raw}");
            return false;
        }

        if (uri.Port <= 0)
        {
            error = EndpointGuidance($"回调地址必须写明确切的端口（CCP 不支持通配端口）：{raw}");
            return false;
        }

        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            error = EndpointGuidance($"回调地址不能带查询串或片段：{raw}");
            return false;
        }

        endpoint = uri;
        error = null;
        return true;
    }

    private static bool IsLoopbackHost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || host.Equals("127.0.0.1", StringComparison.Ordinal)
               || host.Equals("::1", StringComparison.Ordinal)
               || host.Equals("[::1]", StringComparison.Ordinal);
    }

    private static string EndpointGuidance(string reason)
    {
        return $"{reason}。请把 EVE 开发者后台的 Callback URL 与 Configs/ESILicense.txt 第 2 行都设为 {SuggestedCallbackUrl}";
    }

    /// <summary>
    /// 取回调结果页要用的文案。
    /// 必须在 UI 线程调用（语言资源字典不是线程安全的），再交给后台线程拼响应。
    /// </summary>
    public static LoopbackPageStrings LoadPageStrings()
    {
        return new LoopbackPageStrings
        {
            SuccessTitle = FindString("AuthCallback.SuccessTitle"),
            SuccessHint = FindString("AuthCallback.SuccessHint"),
            FailureTitle = FindString("AuthCallback.FailureTitle"),
            RetryHint = FindString("AuthCallback.RetryHint"),
        };
    }

    private static string FindString(string key)
    {
        return Application.Current?.TryFindResource(key) as string ?? key;
    }

    #endregion

    /// <summary>
    /// 从回调地址中解析 authorization code。
    /// 不依赖参数顺序（WinUI 版用 Split('=','&amp;')[1]，遇到顺序变化或额外参数就会取错）。
    /// </summary>
    public static string? ParseAuthorizationCode(string? callbackUri)
    {
        if (string.IsNullOrWhiteSpace(callbackUri))
        {
            return null;
        }

        var query = callbackUri;
        var questionMark = query.IndexOf('?');
        if (questionMark >= 0)
        {
            query = query[(questionMark + 1)..];
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            if (pair.AsSpan(0, equals).Equals("code", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pair[(equals + 1)..]);
            }
        }

        return null;
    }
}
