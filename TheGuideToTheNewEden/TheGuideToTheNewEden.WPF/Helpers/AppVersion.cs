using System.Reflection;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>应用版本号（主窗口标题与"软件更新"页共用同一来源）。</summary>
public static class AppVersion
{
    /// <summary>
    /// 当前版本号：优先取 <see cref="AssemblyInformationalVersionAttribute"/>（csproj 里
    /// <c>&lt;Version&gt;</c> / <c>&lt;InformationalVersion&gt;</c> 会写进它），否则退回程序集版本。
    /// 形如 <c>1.2.3+abc1234</c>（SourceLink 会附上 commit），这里只取 <c>+</c> 之前的部分。
    /// </summary>
    public static string Current { get; } = Resolve();

    /// <summary>"名称 版本"形式，例如 <c>新伊甸漫游指南 1.2.3</c>；名称需由调用方传入（走本地化）。</summary>
    public static string Format(string? displayName)
        => string.IsNullOrWhiteSpace(displayName)
            ? Current
            : $"{displayName} {Current}";

    private static string Resolve()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = string.IsNullOrWhiteSpace(informational)
            ? assembly.GetName().Version?.ToString()
            : informational;

        if (string.IsNullOrWhiteSpace(version))
        {
            return "Unknown";
        }

        // 去掉 "+<commit>" 这类构建元数据，标题里没必要显示
        var plus = version.IndexOf('+');
        return plus > 0 ? version[..plus] : version;
    }
}
