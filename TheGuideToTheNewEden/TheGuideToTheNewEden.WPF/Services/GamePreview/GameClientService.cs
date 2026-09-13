using System.Diagnostics;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WPF.Helpers.Interop;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>
/// EVE 客户端进程的发现与激活。只做"读进程/窗口"和"把某个客户端切到前台"两件事，
/// 不持有任何状态——预览窗口的创建与生命周期归 <see cref="PreviewWindowManager"/>。
/// </summary>
public static class GameClientService
{
    public const string DefaultKeyword = "exefile";

    /// <summary>激活方式：0 标准 / 1 兼容（AttachThreadInput）/ 2 SwitchToThisWindow。</summary>
    public const int MaxActivationMode = 2;

    /// <summary>
    /// 按进程名关键词（逗号分隔）查找有主窗口的客户端。
    /// 关键词会 Trim 并忽略空项——WinUI 版不处理空串，导致 <c>Contains("")</c> 匹配到所有进程。
    /// </summary>
    public static List<ProcessInfo> FindClients(string? processKeywords)
    {
        var keywords = SplitKeywords(processKeywords);
        var result = new List<ProcessInfo>();

        Process[] all;
        try
        {
            all = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return result;
        }

        foreach (var process in all)
        {
            var keep = false;
            try
            {
                if (!Matches(process.ProcessName, keywords) || process.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                result.Add(new ProcessInfo
                {
                    Process = process,
                    MainWindowHandle = process.MainWindowHandle,
                    ProcessName = process.ProcessName,
                    WindowTitle = NativeMethods.GetWindowTitle(process.MainWindowHandle),
                });
                keep = true;
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
            finally
            {
                if (!keep)
                {
                    process.Dispose();
                }
            }
        }

        return result;
    }

    public static List<string> SplitKeywords(string? processKeywords)
    {
        var keywords = (processKeywords ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keywords.Count == 0)
        {
            keywords.Add(DefaultKeyword);
        }

        return keywords;
    }

    private static bool Matches(string processName, List<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (processName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>把客户端窗口切到前台。必须在 UI 线程调用。</summary>
    public static bool Activate(IntPtr hWnd, int mode)
    {
        if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd))
        {
            return false;
        }

        try
        {
            if (NativeMethods.IsIconic(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            }

            return Math.Clamp(mode, 0, MaxActivationMode) switch
            {
                1 => ActivateAttached(hWnd),
                2 => ActivateSwitchToThisWindow(hWnd),
                _ => ActivateStandard(hWnd),
            };
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
    }

    private static bool ActivateStandard(IntPtr hWnd)
    {
        if (NativeMethods.SetForegroundWindow(hWnd))
        {
            return true;
        }

        // SetForegroundWindow 在"调用方不是前台进程"时会失败，退一步把它提到 z 序顶部
        return NativeMethods.BringWindowToTop(hWnd);
    }

    /// <summary>
    /// 把当前线程的输入队列临时接到目标窗口线程上，绕开前台权限限制。
    /// 不论成功与否都要 detach，否则会把两个线程的输入状态粘在一起。
    /// </summary>
    private static bool ActivateAttached(IntPtr hWnd)
    {
        var currentThreadId = NativeMethods.GetCurrentThreadId();
        var targetThreadId = NativeMethods.GetWindowThreadProcessId(hWnd, IntPtr.Zero);
        var attached = false;

        try
        {
            if (targetThreadId != 0 && targetThreadId != currentThreadId)
            {
                attached = NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);
            }

            NativeMethods.BringWindowToTop(hWnd);
            return NativeMethods.SetForegroundWindow(hWnd);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
            }
        }
    }

    private static bool ActivateSwitchToThisWindow(IntPtr hWnd)
    {
        NativeMethods.SwitchToThisWindow(hWnd, true);
        return true;
    }
}
