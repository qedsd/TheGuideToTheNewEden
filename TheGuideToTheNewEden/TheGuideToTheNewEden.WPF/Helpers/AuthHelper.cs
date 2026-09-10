using System.IO;
using Microsoft.Win32;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 自定义 URL 协议（eveauth-*）的注册表读写，以及 ESI 授权回调的等待与解析。
/// </summary>
internal static class AuthHelper
{
    /// <summary>统一使用一个协议名（WinUI 版存在 neweden2/neweden3 混用的问题）。</summary>
    public const string ProtocolName = "eveauth-qedsd-neweden3";

    private const string ProtocolRoot = @"HKEY_CLASSES_ROOT\eveauth-qedsd-neweden3";
    private const string ProtocolKey = @"HKEY_CLASSES_ROOT\eveauth-qedsd-neweden3\shell\open\command";

    public static string? ReadProtocol()
    {
        return Registry.GetValue(ProtocolKey, null, null) as string;
    }

    public static void WriteProtocol()
    {
        var exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TheGuideToTheNewEden.WPF.exe");
        Registry.SetValue(ProtocolRoot, "URL Protocol", string.Empty);
        Registry.SetValue(ProtocolKey, null, $"\"{exe}\" \"%1\"");
    }

    public static void DeleteProtocol()
    {
        Registry.SetValue(ProtocolKey, null, string.Empty);
    }

    /// <summary>
    /// 等待浏览器重定向回来的 eveauth 回调地址。
    /// 授权页会把浏览器重定向到自定义协议，由新启动的第二个进程把命令行交给单实例，
    /// 主实例的 SingleInstanceHelper 随即触发 Activated。
    /// </summary>
    public static async Task<string?> WaitForCallbackAsync(CancellationToken cancellationToken)
    {
        var singleInstance = App.SingleInstanceHelper;
        if (singleInstance is null)
        {
            return null;
        }

        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnActivated(object? sender, string[] args)
        {
            var uri = args?.FirstOrDefault(a =>
                a.StartsWith("eveauth", StringComparison.OrdinalIgnoreCase));
            completion.TrySetResult(uri);
        }

        singleInstance.Activated += OnActivated;
        try
        {
            await using var registration = cancellationToken.Register(() => completion.TrySetResult(null));
            return await completion.Task;
        }
        finally
        {
            singleInstance.Activated -= OnActivated;
        }
    }

    /// <summary>
    /// 从回调地址中解析 authorization code。
    /// 不依赖参数顺序（WinUI 版用 Split('=','&')[1]，遇到顺序变化或额外参数就会取错）。
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