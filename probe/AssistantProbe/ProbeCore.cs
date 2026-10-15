using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 需要核心服务（设置存储/本地 SDE 库/路径）的套件共用的初始化入口。
/// <para>
/// <c>CoreInitializer.Init()</c> 只跑一次即可：任何单独套件（<c>dotnet run … map</c>）与 <c>all</c> 都先经过这里，
/// 从而保证 <c>SettingsService.DataPath</c>、本地库与 ESI 凭据都就绪。
/// </para>
/// </summary>
internal static class ProbeCore
{
    private static bool _initialized;

    public static void Ensure()
    {
        if (_initialized)
        {
            return;
        }

        CoreInitializer.Init();
        _initialized = true;
    }
}
