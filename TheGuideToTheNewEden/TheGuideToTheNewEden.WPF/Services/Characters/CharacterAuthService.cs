using System.Diagnostics;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

/// <summary>
/// ESI 授权编排：起回环监听 → 打开授权页 → 接收回调 → 换取令牌 → 落盘。
/// 国服没有可用的回调地址，走"手动粘贴 code"分支（与 WinUI 版一致）。
/// </summary>
public static class CharacterAuthService
{
    /// <summary>
    /// 上一次国际服登录失败的具体原因（供界面直接展示）。
    /// 只在 <see cref="LoginAsync"/> 内部赋值；进入方法时清空，成功时保持为 null。
    /// </summary>
    public static string? LastFailure { get; private set; }

    /// <summary>凭据是否就绪（来自 Configs/ESILicense.txt）。</summary>
    public static bool CredentialsAvailable =>
        !string.IsNullOrWhiteSpace(Core.Config.ClientId)
        && !string.IsNullOrWhiteSpace(Core.Config.ClientSecret);

    public static bool IsSerenity => GameServerSelectorService.Value == GameServerType.Serenity;

    /// <summary>打开浏览器进入授权页（国服返回的地址需要用户手动复制 code）。</summary>
    public static void OpenAuthorizationPage()
    {
        var url = IsSerenity
            ? SerenityAuthHelper.GetAuthenticationUrl(ESIScopeService.Current.GetSelectedScopes())
            : ESIService.Current.GetAuthorizeUrl();

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    /// <summary>国服：打开网易账号登出页，便于换账号再授权（对应 WinUI 的"步骤 0"）。</summary>
    public static void OpenSerenityLogoffPage()
    {
        Process.Start(new ProcessStartInfo(SerenityAuthHelper.LogoffUrl) { UseShellExecute = true });
    }

    /// <summary>
    /// 国服：授权完成后浏览器停在空白页，用户可能粘贴**整条网址**也可能只粘贴 **code**。
    /// 这里两种都接受（WinUI 用 <c>Split('=','&amp;')[1]</c>，粘贴纯 code 时会拿到错误的值）。
    /// </summary>
    public static string? ExtractAuthorizationCode(string? pasted)
    {
        if (string.IsNullOrWhiteSpace(pasted))
        {
            return null;
        }

        var text = pasted.Trim();
        return AuthHelper.ParseAuthorizationCode(text) ?? (text.Contains('=') ? null : text);
    }

    /// <summary>
    /// 国际服完整登录：起本地回环监听 → 打开授权页 → 收回调 → 换令牌 → 落盘。
    /// </summary>
    /// <returns>授权成功的角色；地址未配置 / 端口占用 / 用户拒绝 / 超时 / 换码失败时返回 null。</returns>
    public static async Task<AuthorizedCharacterData?> LoginAsync(CancellationToken cancellationToken = default)
    {
        LastFailure = null;

        if (!CredentialsAvailable)
        {
            // 调用方在进入本方法前已单独提示过"缺少凭据"，这里只记日志。
            Core.Log.Error("缺少 ESI 凭据（Configs/ESILicense.txt），无法发起授权");
            return null;
        }

        // 1) 校验回调地址：它就是发给 CCP 的 redirect_uri，必须与开发者后台登记的一致。
        if (!AuthHelper.TryGetLoopbackEndpoint(out var endpoint, out var endpointError))
        {
            Core.Log.Error(endpointError);
            LastFailure = endpointError;
            return null;
        }

        // 2) 必须先开始监听再打开授权页：用户点得快时回调可能早于浏览器打开到达。
        using var server = LoopbackAuthServer.TryStart(
            endpoint, AuthHelper.LoadPageStrings(), AuthHelper.CallbackTimeout, out var startError);
        if (server is null)
        {
            var message = $"无法监听授权回调地址 {endpoint}（端口可能已被占用）：{startError}";
            Core.Log.Error(message);
            LastFailure = message;
            return null;
        }

        // 3) 打开授权页，等浏览器把回调打回来（超时、拒绝都会正常返回，不会挂起）。
        OpenAuthorizationPage();

        var result = await server.WaitForCallbackAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Outcome == AuthCallbackOutcome.Denied)
            {
                Core.Log.Warn($"授权被拒绝：{result.Message}");
            }
            else
            {
                Core.Log.Warn(result.Message ?? "未收到授权回调");
            }

            LastFailure = result.Message;
            return null;
        }

        // 4) 换令牌并落盘。
        return await CompleteFromCallbackAsync(result.CallbackUri!);
    }

    /// <summary>用授权回调地址换取令牌并保存。</summary>
    public static async Task<AuthorizedCharacterData?> CompleteFromCallbackAsync(string callbackUri)
    {
        var code = AuthHelper.ParseAuthorizationCode(callbackUri);
        if (string.IsNullOrEmpty(code))
        {
            Core.Log.Error($"无法从回调地址解析 authorization code: {callbackUri}");
            LastFailure = "回调地址里没有 code 参数";
            return null;
        }

        return await VerifyAndStoreAsync(code);
    }

    /// <summary>国服：用用户手动复制的 code（或整条回调网址）换取令牌并保存。</summary>
    public static Task<AuthorizedCharacterData?> CompleteSerenityAsync(string pasted)
    {
        return VerifyAndStoreAsync(ExtractAuthorizationCode(pasted));
    }

    private static async Task<AuthorizedCharacterData?> VerifyAndStoreAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        try
        {
            var character = await ESIService.Current.VerifyAuthorization(code);
            if (character is null)
            {
                LastFailure = "授权码校验失败（授权码可能已过期或已被使用）";
                return null;
            }

            CharacterStore.Add(character);
            return character;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            LastFailure = $"换取令牌失败：{ex.Message}";
            return null;
        }
    }
}