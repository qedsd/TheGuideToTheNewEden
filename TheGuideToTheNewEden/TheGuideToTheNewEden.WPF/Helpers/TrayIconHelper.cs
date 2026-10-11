using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using H.NotifyIcon;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 托盘图标（H.NotifyIcon <see cref="TaskbarIcon"/>）的创建与失败诊断。
/// <para>
/// 为什么不能只依赖库自己在 <c>Loaded</c> 里的那次创建：
/// </para>
/// <list type="number">
/// <item>它**只有一次**机会，失败时只在 catch 里 <c>Debugger.Break()</c>——Release 下完全静默，
/// 表现为"托盘里就是没有图标"，而日志里一个字都没有；</item>
/// <item>**真实原因（Win32 错误码）被库吞掉了**：库的 <c>TrayIcon.Create()</c> 把
/// <c>Shell_NotifyIcon(NIM_ADD)</c> 的返回值变成一句 <c>InvalidOperationException("TryCreate failed.")</c>，
/// 而实测"异常之后立刻读 <c>Marshal.GetLastWin32Error()</c>"得到的是 **0**（库的 TryCreate 内部还夹着别的
/// P/Invoke，错误码已被覆盖），所以从异常上根本看不出是"没有通知区域"还是"数据不对"。</item>
/// </list>
/// <para>
/// 这里因此做三件事：① 在主窗口 <c>Loaded</c> **之前**先自己创建一次——成功的话库在 Loaded 里的那次
/// 就变成空操作，调试时也不会再中断在 <c>Debugger.Break()</c>；② 失败则按间隔重试若干次
/// （通知区域尚未就绪是启动期最常见的原因）；③ 最终仍失败时，用**裸 <c>Shell_NotifyIcon(NIM_ADD)</c>**
/// 探一次真实 Win32 错误码写进日志（临时图标会立刻删除），便于定位。
/// </para>
/// </summary>
public static class TrayIconHelper
{
    private const int NIM_ADD = 0x00000000;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    /// <summary>
    /// 尝试同步创建托盘图标；返回是否已创建成功（失败只记 Debug 日志，不抛）。
    /// 中间过程的失败不写 Warn：真正的结论由 <see cref="EnsureCreatedAsync"/> 汇总输出，避免每次启动刷 7 行日志。
    /// </summary>
    public static bool TryCreate(TaskbarIcon trayIcon, string stage)
    {
        if (trayIcon.IsCreated)
        {
            return true;
        }

        try
        {
            trayIcon.ForceCreate(enablesEfficiencyMode: false);
        }
        catch (Exception ex)
        {
            Core.Log.Debug($"[托盘] {stage}创建托盘图标失败：{ex.Message}");
            return false;
        }

        if (trayIcon.IsCreated)
        {
            Core.Log.Debug($"[托盘] {stage}已创建托盘图标");
            return true;
        }

        Core.Log.Debug($"[托盘] {stage}创建托盘图标未成功（IsCreated 仍为 false）");
        return false;
    }

    /// <summary>
    /// 间隔重试若干次创建托盘图标；全部失败时写一行汇总 + 一行诊断。返回最终是否可用。
    /// </summary>
    /// <param name="trayIcon">托盘图标。</param>
    /// <param name="owner">用于诊断取窗口句柄的主窗口。</param>
    /// <param name="attempts">重试次数。</param>
    /// <param name="delayMs">每次重试前的等待毫秒数。</param>
    public static async Task<bool> EnsureCreatedAsync(
        TaskbarIcon trayIcon,
        Window owner,
        int attempts = 5,
        int delayMs = 400)
    {
        if (trayIcon.IsCreated)
        {
            return true;
        }

        for (var i = 1; i <= attempts && !trayIcon.IsCreated; i++)
        {
            await Task.Delay(delayMs).ConfigureAwait(true);
            if (TryCreate(trayIcon, $"第 {i} 次重试"))
            {
                return true;
            }
        }

        if (trayIcon.IsCreated)
        {
            return true;
        }

        Core.Log.Warn($"[托盘] 启动时创建托盘图标失败，重试 {attempts} 次仍未成功（详见下一行诊断）。");
        LogShellDiagnostics(trayIcon, owner);
        return false;
    }

    /// <summary>
    /// 用裸 <c>Shell_NotifyIcon</c> 探真实 Win32 错误码（库把这个信息丢掉了）。
    /// 成功时立刻 <c>NIM_DELETE</c> 去掉这个临时图标。
    /// </summary>
    private static void LogShellDiagnostics(TaskbarIcon trayIcon, Window owner)
    {
        try
        {
            var hwnd = new WindowInteropHelper(owner).Handle;
            var hicon = trayIcon.TrayIcon.Icon;

            var data = new NotifyIconData
            {
                hWnd = hwnd,
                uID = 0x7FFF,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = 0x0400 + 0x7FFF,
                hIcon = hicon,
                szTip = "diagnostic",
                szInfo = string.Empty,
                szInfoTitle = string.Empty,
            };
            data.cbSize = Marshal.SizeOf<NotifyIconData>();

            var ok = Shell_NotifyIcon(NIM_ADD, ref data);
            var error = Marshal.GetLastWin32Error();
            if (ok)
            {
                Shell_NotifyIcon(NIM_DELETE, ref data);
            }

            Core.Log.Error(
                $"[托盘] 托盘图标最终创建失败。裸 Shell_NotifyIcon(NIM_ADD) 返回 {ok}，Win32 错误 = {error} (0x{error:X})" +
                $"（{DescribeWin32Error(error)}）；窗口句柄 = 0x{hwnd:X}，图标句柄 = 0x{hicon:X}，cbSize = {data.cbSize}。" +
                "该信息用于区分「当前会话没有通知区域」与「图标/窗口数据不正确」。");

            // 上下文单独一行、单独 try：即便这段 Win32 读取有问题，上面那行关键信息也已经写出去了。
            try
            {
                var appContext = DescribeProcess((uint)Environment.ProcessId, currentThreadDesktop: true);
                var shellContext = DescribeShell();
                Core.Log.Error($"[托盘] 上下文对比：应用[{appContext}] vs shell[{shellContext}]。{BuildHint(appContext, shellContext)}");

                // 再打出**整条启动链**：只有它能回答"明明是手动双击 / VS 启动的，为什么还是 Low"
                // （通常是启动器本身就在沙箱里，或某处存在强制降级策略）。
                Core.Log.Error($"[托盘] 启动链（自下而上，括号内为完整性级别）：{DescribeParentChain()}");
            }
            catch (Exception ex)
            {
                Core.Log.Warn($"[托盘] 上下文诊断失败：{ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error($"[托盘] 诊断失败：{ex}");
        }
    }

    private static string DescribeWin32Error(int error) => error switch
    {
        0 => "未报错：说明裸调用是成功的，问题在库内部的数据/时序（可据此改用自建托盘图标）",
        5 => "ERROR_ACCESS_DENIED：进程被 shell 拒绝——最常见的是**完整性级别低→高被 UIPI 拦**、**会话号不同**、**桌面/窗口站不同**或**用户不同**",
        8 => "ERROR_NOT_ENOUGH_MEMORY",
        87 => "ERROR_INVALID_PARAMETER：NOTIFYICONDATA 数据不被接受",
        1400 => "ERROR_INVALID_WINDOW_HANDLE：窗口句柄无效（窗口可能尚未创建）",
        _ => "见 Win32 错误码表",
    };

    // ---------- 上下文诊断（应用进程 vs shell 进程）----------

    /// <summary>
    /// 把"应用 vs shell"的差异翻译成一句人话结论（这一段就是使用者真正需要看的）。
    /// </summary>
    private static string BuildHint(string appContext, string shellContext)
    {
        var appLow = appContext.Contains("完整性=Low", StringComparison.Ordinal);
        var shellMediumOrHigher = shellContext.Contains("完整性=Medium", StringComparison.Ordinal)
                                  || shellContext.Contains("完整性=High", StringComparison.Ordinal);

        if (appLow && shellMediumOrHigher)
        {
            return "结论：本进程以**低完整性级别**运行，而 shell 是 Medium——Windows 的 UIPI 会拒绝低完整性进程" +
                   "向 shell 发消息（Shell_NotifyIcon 正是如此），所以托盘图标建不起来。" +
                   "**看下一行的「启动链」定位是哪一层把级别降下来的**：若链条里出现 DeepSeek Harness.exe（或它拉起的" +
                   "powershell.exe / cmd.exe / devenv.exe）而那一层是 Low，说明程序是**在 DSH 沙箱里启动的**" +
                   "（子进程只能继承或降低完整性、不能升高）；此时请从**沙箱外**启动：" +
                   "桌面资源管理器双击，或从**开始菜单**打开终端/Visual Studio 再运行。" +
                   "用 `whoami /groups | findstr Mandatory` 可自查当前上下文（出现 Low Mandatory Level 即为此因）。";
        }

        if (!appContext.Contains("会话=", StringComparison.Ordinal) || !shellContext.Contains("会话=", StringComparison.Ordinal))
        {
            return "结论：会话号没能读全（多半是权限不足）。若确认应用与 shell 不在同一会话（例如应用跑在另一个登录会话/" +
                   "服务会话），托盘图标同样会被拒；处置：在与桌面相同的会话、相同的用户下启动程序。";
        }

        return "结论：若上面两项的完整性级别/会话号/桌面名存在差异，即为 Shell_NotifyIcon 被拒的原因" +
               "（低→高会被 UIPI 拒绝；会话或桌面不同则消息根本到不了 shell）。";
    }

    /// <summary>
    /// 自下而上打印整条启动链（本进程 → 父 → 祖父 …，最多 8 层），每层带完整性级别。
    /// 用于回答"明明是手动双击 / VS 启动的，为什么还是 Low"——一眼就能看出是**哪一层**把级别降下来的。
    /// </summary>
    private static string DescribeParentChain()
    {
        var parts = new List<string>();
        var pid = (uint)Environment.ProcessId;

        for (var depth = 0; depth < 8 && pid != 0; depth++)
        {
            var handle = pid == (uint)Environment.ProcessId
                ? GetCurrentProcess()
                : OpenProcess(ProcessQueryLimitedInformation, false, pid);

            if (handle == 0)
            {
                // 打不开通常意味着那一层完整性更高（或权限不足），本身就是有用的信息
                parts.Add($"PID{pid}(打不开,错误{Marshal.GetLastWin32Error()})");
                break;
            }

            try
            {
                var name = ImageName(handle);
                var integrity = "(?)";
                if (OpenProcessToken(handle, TokenQuery, out var token))
                {
                    try
                    {
                        integrity = IntegrityLevel(token);
                    }
                    finally
                    {
                        CloseHandle(token);
                    }
                }
                else
                {
                    integrity = $"(拒绝{Marshal.GetLastWin32Error()})";
                }

                parts.Add($"{name}(PID{pid},{integrity})");
                pid = ParentProcessId(handle);
            }
            finally
            {
                if (pid != (uint)Environment.ProcessId)
                {
                    CloseHandle(handle);
                }
            }
        }

        return string.Join(" ← ", parts);
    }

    private static string ImageName(nint handle)
    {
        var buffer = new StringBuilder(1024);
        var size = buffer.Capacity;
        return QueryFullProcessImageName(handle, 0, buffer, ref size)
            ? System.IO.Path.GetFileName(buffer.ToString())
            : "(读不到映像名)";
    }

    private static uint ParentProcessId(nint handle)
    {
        var info = new ProcessBasicInformation();
        var status = NtQueryInformationProcess(handle, 0, ref info, Marshal.SizeOf<ProcessBasicInformation>(), out _);
        return status == 0 ? (uint)info.InheritedFromUniqueProcessId : 0;
    }

    private static string DescribeShell()
    {
        var shellWindow = GetShellWindow();
        if (shellWindow == 0)
        {
            return "本桌面找不到 shell 窗口（GetShellWindow = 0，说明没有任务栏）";
        }

        var threadId = GetWindowThreadProcessId(shellWindow, out var pid);
        var desktop = threadId == 0 ? "(未知)" : DesktopName(GetThreadDesktop(threadId));
        return pid == 0
            ? $"窗口 0x{shellWindow:X}，进程号读取失败"
            : DescribeProcess(pid, currentThreadDesktop: false) + $"，桌面={desktop}，窗口=0x{shellWindow:X}";
    }

    private static string DescribeProcess(uint pid, bool currentThreadDesktop)
    {
        // 会话号优先用 ProcessIdToSessionId：它只要 PID，不像 Process.GetProcessById 那样会因跨用户/跨会话
        // 抛异常（实测读 shell 进程时就会失败，日志里只剩 "(未知)"，反而看不出"会话是否一致"这个关键差异）。
        var session = "(未知)";
        if (ProcessIdToSessionId(pid, out var sessionId))
        {
            session = sessionId.ToString();
        }
        else
        {
            try
            {
                session = System.Diagnostics.Process.GetProcessById((int)pid).SessionId.ToString();
            }
            catch
            {
                // 拿不到就算了
            }
        }

        var desktop = currentThreadDesktop ? DesktopName(GetThreadDesktop(GetCurrentThreadId())) : "(见 shell 行)";
        var integrity = "(读不到)";
        var user = "(读不到)";

        var handle = pid == (uint)Environment.ProcessId
            ? GetCurrentProcess()
            : OpenProcess(ProcessQueryLimitedInformation, false, pid);

        if (handle != 0)
        {
            try
            {
                if (OpenProcessToken(handle, TokenQuery, out var token))
                {
                    try
                    {
                        integrity = IntegrityLevel(token);
                        user = TokenUserName(token);
                    }
                    finally
                    {
                        CloseHandle(token);
                    }
                }
                else
                {
                    integrity = $"(OpenProcessToken 失败 {Marshal.GetLastWin32Error()})";
                }
            }
            finally
            {
                if (pid != (uint)Environment.ProcessId)
                {
                    CloseHandle(handle);
                }
            }
        }
        else
        {
            integrity = $"(OpenProcess 失败 {Marshal.GetLastWin32Error()}，多半是权限不足)";
        }

        return $"PID={pid} 会话={session} 完整性={integrity} 用户={user} 桌面={desktop}";
    }

    /// <summary>读进程令牌的完整性级别（Low/Medium/High/System…）。</summary>
    private static string IntegrityLevel(nint token)
    {
        if (!GetTokenInformation(token, TokenIntegrityLevel, 0, 0, out var size) && size == 0)
        {
            return $"(取长度失败 {Marshal.GetLastWin32Error()})";
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, size, out _))
            {
                return $"(读取失败 {Marshal.GetLastWin32Error()})";
            }

            // TOKEN_MANDATORY_LABEL { SID_AND_ATTRIBUTES Label; ... } → 第一个字段就是 SID 指针
            var sid = Marshal.ReadIntPtr(buffer);
            var countPointer = GetSidSubAuthorityCount(sid);
            if (countPointer == 0)
            {
                return "(SID 计数读取失败)";
            }

            var count = Marshal.ReadByte(countPointer);
            if (count == 0)
            {
                return "(SID 为空)";
            }

            var rid = (uint)Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
            return rid switch
            {
                0x0000 => "Untrusted",
                0x1000 => "Low",
                0x2000 => "Medium",
                0x2100 => "MediumPlus",
                0x3000 => "High",
                0x4000 => "System",
                0x5000 => "ProtectedProcess",
                _ => $"0x{rid:X}",
            };
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string TokenUserName(nint token)
    {
        // TokenUser = 1
        if (!GetTokenInformation(token, 1, 0, 0, out var size) && size == 0)
        {
            return "(取长度失败)";
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!GetTokenInformation(token, 1, buffer, size, out _))
            {
                return "(读取失败)";
            }

            var sid = Marshal.ReadIntPtr(buffer);
            if (!ConvertSidToStringSid(sid, out var stringSid))
            {
                return "(SID 转字符串失败)";
            }

            try
            {
                return Marshal.PtrToStringUni(stringSid) ?? "(空)";
            }
            finally
            {
                LocalFree(stringSid);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string DesktopName(nint desktop)
    {
        if (desktop == 0)
        {
            return "(null)";
        }

        var buffer = new StringBuilder(256);
        return GetUserObjectInformation(desktop, UoiName, buffer, buffer.Capacity * 2, out _)
            ? buffer.ToString()
            : $"(读取失败 {Marshal.GetLastWin32Error()})";
    }

    // ---------- Win32 ----------

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenIntegrityLevel = 25;
    private const int UoiName = 2;

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(nint processHandle, uint desiredAccess, out nint tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(nint tokenHandle, int tokenInformationClass, nint tokenInformation, int tokenInformationLength, out int returnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern nint GetSidSubAuthorityCount(nint sid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern nint GetSidSubAuthority(nint sid, uint subAuthorityIndex);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertSidToStringSid(nint sid, out nint stringSid);

    [DllImport("kernel32.dll")]
    private static extern nint LocalFree(nint handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(nint processHandle, uint flags, StringBuilder exeName, ref int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public nint ExitStatus;
        public nint PebBaseAddress;
        public nint AffinityMask;
        public nint BasePriority;
        public nint UniqueProcessId;
        public nint InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(nint processHandle, int processInformationClass, ref ProcessBasicInformation processInformation, int processInformationLength, out int returnLength);

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern nint GetThreadDesktop(uint threadId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetUserObjectInformation(nint handle, int index, StringBuilder info, int length, out int lengthNeeded);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public nint hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public nint hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public int dwState;
        public int dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public int uVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public int dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NotifyIconData lpData);
}
