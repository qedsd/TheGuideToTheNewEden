using System.Diagnostics;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

/// <summary>
/// ESI 授权编排：打开授权页 → 接收回调 → 换取令牌 → 落盘。
/// 国服没有可用的回调地址，走"手动粘贴 code"分支（与 WinUI 版一致）。
/// </summary>
public static class CharacterAuthService
{
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
    /// 国际服完整登录：打开授权页并等待自定义协议回调。
    /// </summary>
    public static async Task<AuthorizedCharacterData?> LoginAsync(CancellationToken cancellationToken = default)
    {
        if (!CredentialsAvailable)
        {
            Core.Log.Error("缺少 ESI 凭据（Configs/ESILicense.txt），无法发起授权");
            return null;
        }

        AuthHelper.WriteProtocol();
        OpenAuthorizationPage();

        var callbackUri = await AuthHelper.WaitForCallbackAsync(cancellationToken);
        if (string.IsNullOrEmpty(callbackUri))
        {
            return null;
        }

        return await CompleteFromCallbackAsync(callbackUri);
    }

    /// <summary>用授权回调地址换取令牌并保存。</summary>
    public static async Task<AuthorizedCharacterData?> CompleteFromCallbackAsync(string callbackUri)
    {
        var code = AuthHelper.ParseAuthorizationCode(callbackUri);
        if (string.IsNullOrEmpty(code))
        {
            Core.Log.Error($"无法从回调地址解析 authorization code: {callbackUri}");
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
                return null;
            }

            CharacterStore.Add(character);
            return character;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }
}