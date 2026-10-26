namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>
/// **发布内置的 ESI 应用凭据**（编译期常量）。
/// <para>
/// 用途：发布时把 EVE 开发者后台的 Client ID / Callback URL / Client Secret 直接编译进程序，
/// 终端用户打开软件即可授权，无需在 <c>Configs/ESILicense.txt</c> 里放任何文件
/// （与 WinUI 3 版在 <c>ActivationService.GetESILicense()</c> 里预留的做法一致）。
/// </para>
/// <para>
/// **发布前只改这里**：把下面三个常量填成真实值后重新编译 Release。
/// 三项必须同时填写，任一为空都会被视为“未内置”，此时回退到
/// <c>SettingsService.DataPath\Configs\ESILicense.txt</c>（供开发/运维临时覆盖）。
/// </para>
/// <para>
/// 顺序与 <c>ESILicense.txt</c> 完全一致：
/// 第 1 行 ClientId、第 2 行 Callback、第 3 行 ClientSecret。
/// Callback 必须与 EVE 开发者后台登记的 Callback URL 完全一致（欧服必须是本机回环 http 地址，不支持通配端口）。
/// </para>
/// <para>
/// ⚠ 安全提示：常量会被写进程序集，拿到程序的人可以提取出来；不要把填写后的真实值提交到公开仓库。
/// </para>
/// </summary>
internal static class EsiLicenseRelease
{
    // TODO:release 发布前填写：EVE 开发者后台的 Client ID。
    internal const string ClientId = "";

    // TODO:release 发布前填写：与开发者后台登记完全一致的 Callback URL（例：http://localhost:38471/callback/）。
    internal const string Callback = "";

    // TODO:release 发布前填写：EVE 开发者后台的 Client Secret。
    internal const string ClientSecret = "";

    /// <summary>三项都非空才视为已内置；任一项为空则回退 <c>Configs/ESILicense.txt</c>。</summary>
    internal static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(Callback)
        && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>按 <c>ESILicense.txt</c> 的行序返回：ClientId / Callback / ClientSecret。</summary>
    internal static string[] Lines => [ClientId, Callback, ClientSecret];
}
