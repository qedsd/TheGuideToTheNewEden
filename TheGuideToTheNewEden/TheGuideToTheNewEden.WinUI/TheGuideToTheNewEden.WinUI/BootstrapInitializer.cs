using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TheGuideToTheNewEden.WinUI;

/// <summary>
/// Ensures the Windows App SDK bootstrap is initialized before any WinUI code runs.
/// When the app is deployed as framework-dependent (non-MSIX, non-SelfContained),
/// the auto-generated bootstrap initialization from WindowsAppSDKBootstrapInitialize=true
/// may not be generated correctly. This module initializer guarantees bootstrap runs
/// before Main() is entered.
/// 
/// Without this call, native WinRT types in Microsoft.UI.Xaml.dll cannot be activated,
/// resulting in 0xc000027b (STATUS_INVALID_CRUNTIME_PARAMETER) crashes.
/// </summary>
internal static class BootstrapInitializer
{
    // Windows App SDK version: 2.3
    private const uint MajorMinorVersion = (2 << 16) | 3;  // 0x00020003
    private const uint OnPackageIdentityNoOp = 0x0010;      // MddBootstrapInitializeOptions_OnPackageIdentity_NOOP

    /// <summary>
    /// Initialize the Windows App SDK bootstrap for unpackaged deployment.
    /// Calls the native bootstrap directly to avoid requiring a managed assembly reference.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        int hr = MddBootstrapInitialize2(MajorMinorVersion, null, 0, OnPackageIdentityNoOp);
        if (hr < 0)
        {
            // Log to Windows Event Log if possible
            try
            {
                System.Diagnostics.EventLog.WriteEntry("Application",
                    $"MddBootstrapInitialize2 failed with HRESULT 0x{hr:X8}",
                    System.Diagnostics.EventLogEntryType.Error);
            }
            catch
            {
                // Ignore logging failure
            }
        }
    }

    /// <summary>
    /// Initialize the calling process to use Windows App Runtime framework package.
    /// </summary>
    /// <param name="majorMinorVersion">Major.Minor encoded as 0xMMMMNNNN (e.g. 2.3 == 0x00020003)</param>
    /// <param name="versionTag">Version pre-release identifier, or null if none</param>
    /// <param name="minVersion">Minimum version as PACKAGE_VERSION uint64</param>
    /// <param name="options">MddBootstrapInitializeOptions flags</param>
    /// <returns>HRESULT (0 = S_OK)</returns>
    [DllImport("Microsoft.WindowsAppRuntime.Bootstrap.dll", CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern int MddBootstrapInitialize2(
        uint majorMinorVersion,
        [MarshalAs(UnmanagedType.LPWStr)] string? versionTag,
        ulong minVersion,
        uint options);
}
