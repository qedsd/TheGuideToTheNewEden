using System.IO;
using Microsoft.Win32;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 自定义 URL 协议（eveauth-*）的注册表读写，用于 ESI 授权回调。
/// 移植自 WinUI 项目的同名助手，去掉其中的并发等待逻辑（授权流程尚未移植）。
/// </summary>
internal static class AuthHelper
{
    private const string ProtocolKey = @"HKEY_CLASSES_ROOT\eveauth-qedsd-neweden3\shell\open\command";
    private const string ProtocolRoot = @"HKEY_CLASSES_ROOT\eveauth-qedsd-neweden3";

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
}