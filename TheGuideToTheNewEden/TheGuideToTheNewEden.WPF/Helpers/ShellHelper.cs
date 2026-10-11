using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 用系统 shell 打开目录 / 文件，以及在资源管理器里定位某个文件。
/// <para>
/// ⚠️ **不要写 <c>Process.Start("explorer.exe", path)</c>**：那是**另起一个 explorer 进程**。
/// 在受限上下文（低完整性 / 沙箱，见 REFACTORING 阶段 82）里，这个新进程会以
/// <c>0xc0000142</c>（<c>STATUS_DLL_INIT_FAILED</c>）启动失败，Windows 还会弹出一个
/// "explorer.exe - Application Error" 系统错误框——用户实机截图正是这个。
/// </para>
/// <para>
/// 正确做法是走 <c>ShellExecute</c>（<see cref="ProcessStartInfo.UseShellExecute"/> = true），
/// 把请求交给**已在运行的那个 shell**，不产生新进程；"定位文件"用
/// <c>SHOpenFolderAndSelectItems</c>（同样是 shell 自己完成，不拉 explorer.exe）。
/// </para>
/// <para>
/// 这两种做法在受限上下文里同样可能被系统拒绝（实测：ShellExecute 抛 <c>Win32Exception：拒绝访问</c>），
/// 但都是**可捕获**的。因此本类失败时不再往上抛，而是**把路径复制到剪贴板 + 应用内提示**，
/// 让用户至少能自己粘贴到资源管理器打开，而不是面对一个系统错误框。
/// </para>
/// </summary>
public static class ShellHelper
{
    /// <summary>用系统 shell 打开目录；失败时把路径复制到剪贴板并提示（不抛异常）。</summary>
    public static void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"[Shell] 目录不存在且创建失败：{folder} —— {ex.Message}");
        }

        if (!TryOpen(folder, out _))
        {
            NotifyFailure(folder);
        }
    }

    /// <summary>用系统 shell 打开文件（用默认程序）；失败时把路径复制到剪贴板并提示（不抛异常）。</summary>
    public static void OpenFile(string filePath)
    {
        if (!TryOpen(filePath, out _))
        {
            NotifyFailure(filePath);
        }
    }

    /// <summary>
    /// 在资源管理器里**定位并选中**某个文件（等价于原来 <c>explorer.exe /select</c> 的效果，
    /// 但不另起 explorer 进程）。定位失败时退一步打开所在目录，再失败则复制路径并提示。
    /// </summary>
    public static void RevealInFolder(string filePath)
    {
        if (TryRevealInFolder(filePath))
        {
            return;
        }

        var folder = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(folder))
        {
            OpenFolder(folder);
            return;
        }

        NotifyFailure(filePath);
    }

    /// <summary>用 ShellExecute 打开任意目标（目录/文件/URL）。失败返回 false 并给出原因。</summary>
    public static bool TryOpen(string target, out string error)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            Core.Log.Warn($"[Shell] 无法用系统 shell 打开：{target} —— {error}");
            return false;
        }
    }

    /// <summary>调用 shell 的"打开所在文件夹并选中该项"。</summary>
    private static bool TryRevealInFolder(string filePath)
    {
        nint itemIdList = 0;
        try
        {
            var hr = SHParseDisplayName(filePath, 0, out itemIdList, 0, out _);
            if (hr != 0 || itemIdList == 0)
            {
                Core.Log.Warn($"[Shell] SHParseDisplayName 失败：{filePath} —— HRESULT 0x{hr:X}");
                return false;
            }

            // cidl = 0 且 apidl = null 时，pidlFolder 指向的那一项本身就是"被选中的项"。
            hr = SHOpenFolderAndSelectItems(itemIdList, 0, null, 0);
            if (hr != 0)
            {
                Core.Log.Warn($"[Shell] SHOpenFolderAndSelectItems 失败：{filePath} —— HRESULT 0x{hr:X}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"[Shell] 定位文件失败：{filePath} —— {ex.Message}");
            return false;
        }
        finally
        {
            if (itemIdList != 0)
            {
                ILFree(itemIdList);
            }
        }
    }

    /// <summary>打不开时的兜底：把路径复制到剪贴板 + 应用内提示（受限上下文里用户只能这样自己打开）。</summary>
    private static void NotifyFailure(string target)
    {
        var copied = TryCopyToClipboard(target);
        Core.Log.Warn($"[Shell] 已放弃用系统 shell 打开（当前运行上下文可能受限）：{target}；路径已复制到剪贴板 = {copied}");

        var message = $"{FindString("Shell_OpenDenied")}\n{target}";
        if (copied)
        {
            message += $"\n{FindString("Shell_PathCopied")}";
        }

        PageNotifyService.Warning(message);
    }

    private static bool TryCopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (Exception ex)
        {
            // 剪贴板被别的程序占用时 SetText 会抛（另有 COMException 的情况），这里只记日志。
            Core.Log.Warn($"[Shell] 复制路径到剪贴板失败：{ex.Message}");
            return false;
        }
    }

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHParseDisplayName(
        string pszName,
        nint pbc,
        out nint ppidl,
        uint sfgaoIn,
        out uint psfgaoOut);

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHOpenFolderAndSelectItems(
        nint pidlFolder,
        uint cidl,
        [In] nint[]? apidl,
        uint dwFlags);

    [DllImport("shell32.dll")]
    private static extern void ILFree(nint pidl);
}
