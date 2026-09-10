using System.Text;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 国服（Serenity）授权地址构造。
/// 国服使用独立客户端 ID 与回调地址，且不支持部分权限，需要过滤。
/// </summary>
internal static class SerenityAuthHelper
{
    private const string Host = "https://login.evepc.163.com/v2/oauth/authorize";
    private const string ClientIdValue = "bc90aa496a404724a93f41b4f4e97761";
    private const string RedirectUri = "https://ali-esi.evepc.163.com/ui/oauth2-redirect.html";

    /// <summary>国服不支持的权限。</summary>
    private static readonly string[] InvalidScopes =
    [
        "esi-wallet.read_corporation_wallet.v1",
        "esi-characters.read_chat_channels.v1",
    ];

    public static string ClientId => ClientIdValue;

    public static string LogoffUrl => "https://login.evepc.163.com/account/logoff";

    public static string GetAuthenticationUrl(IEnumerable<string> scopes)
    {
        var scope = string.Join(' ', scopes.Where(s => !InvalidScopes.Contains(s)));
        var deviceId = Random.Shared.Next(int.MaxValue);

        return $"{Host}?response_type=code&client_id={ClientIdValue}&redirect_uri={RedirectUri}" +
               $"&scope={scope}&state=test&realm=ESI&device_id={deviceId}";
    }
}