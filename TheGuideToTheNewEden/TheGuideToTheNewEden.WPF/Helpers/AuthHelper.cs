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

    /// <summary>机器级注册（安装包写入，对所有用户有效；写它需要管理员权限）。</summary>
    private const string MachineRoot = @"HKEY_CLASSES_ROOT\" + ProtocolName;
    private const string MachineCommandKey = MachineRoot + @"\shell\open\command";

    /// <summary>当前用户级注册（不需要管理员权限；HKEY_CLASSES_ROOT 读取时会与机器级合并）。</summary>
    private const string UserRoot = @"HKEY_CURRENT_USER\Software\Classes\" + ProtocolName;
    private const string UserCommandKey = UserRoot + @"\shell\open\command";

    /// <summary>等待回调的上限；超时按"没收到回调"处理，避免授权流程永久挂起。</summary>
    public static TimeSpan CallbackTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 注册表里应写入的命令行。
    /// </summary>
    /// <remarks>
    /// 可执行文件路径一律取**当前进程**，绝不硬编码文件名：
    /// 本项目改过 AssemblyName（TheGuideToTheNewEden.WPF.exe → TheGuideToTheNewEden.exe），
    /// 硬编码会让注册表指向一个不存在的 exe —— 浏览器点了回调也打不开客户端。
    /// </remarks>
    private static string BuildCommand()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            // 极端兜底：Environment.ProcessPath 在个别宿主下可能为 null，用进程目录 + 当前程序集名。
            exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TheGuideToTheNewEden.exe");
        }

        return $"\"{exe}\" \"%1\"";
    }

    /// <summary>
    /// 读取当前生效的命令行。
    /// 先看用户级：HKEY_CLASSES_ROOT 是 HKCU 与 HKLM 的合并视图，**同名时 HKCU 优先**，
    /// 所以顺序反了会把"仅机器级过期"误判成"当前值就是过期的"。
    /// </summary>
    public static string? ReadProtocol()
    {
        return (Registry.GetValue(UserCommandKey, null, null) as string)
               ?? (Registry.GetValue(MachineCommandKey, null, null) as string);
    }

    /// <summary>
    /// 写入协议注册（幂等）。
    /// 已经是正确值时**不做写操作**：安装包写好的机器级注册对普通用户是"可读不可写"的，
    /// 硬写会抛权限异常；只有确实需要修正时才尝试写，且机器级失败会退回当前用户级。
    /// </summary>
    /// <exception cref="InvalidOperationException">机器级与用户级都写不进去、且当前值不正确时抛出。</exception>
    public static void WriteProtocol()
    {
        var expected = BuildCommand();
        if (string.Equals(ReadProtocol(), expected, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (TryWrite(MachineRoot, MachineCommandKey, expected, out var machineError))
        {
            return;
        }

        if (TryWrite(UserRoot, UserCommandKey, expected, out var userError))
        {
            Core.Log.Warn($"协议 {ProtocolName} 写不进机器级注册表（{machineError}），已退回当前用户级");
            return;
        }

        var message = $"协议 {ProtocolName} 注册失败：机器级 {machineError}；用户级 {userError}";
        Core.Log.Error(message);
        throw new InvalidOperationException(message);
    }

    private static bool TryWrite(string root, string commandKey, string value, out string? error)
    {
        try
        {
            Registry.SetValue(root, "URL Protocol", string.Empty);
            Registry.SetValue(commandKey, null, value);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 删除协议注册（机器级与用户级都清掉）。
    /// 旧实现是"写空串"，那会留下一个坏关联（协议还在、但命令为空）。
    /// 无权删除的层级记 Warn，不抛出。
    /// </summary>
    public static void DeleteProtocol()
    {
        try
        {
            Registry.ClassesRoot.DeleteSubKeyTree(ProtocolName, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"删除机器级协议注册失败：{ex.Message}");
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + ProtocolName, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"删除用户级协议注册失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 等待浏览器重定向回来的 eveauth 回调地址。
    /// 授权页会把浏览器重定向到自定义协议，由新启动的第二个进程把命令行交给单实例，
    /// 主实例的 SingleInstanceHelper 随即触发 Activated。
    /// </summary>
    /// <returns>回调地址；超时或外部取消时返回 null。</returns>
    public static async Task<string?> WaitForCallbackAsync(CancellationToken cancellationToken)
    {
        // 单实例状态挂在 Program（非 WPF 派生类型）上，原因见 Program 的注释。
        var singleInstance = Program.SingleInstance;
        if (singleInstance is null)
        {
            return null;
        }

        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnActivated(object? sender, string[] args)
        {
            // 只认授权回调：普通"再开一次程序"（命令行里没有 eveauth）不结束等待，
            // 否则用户在等授权时随手双击一下图标就会把登录流程打断。
            var uri = args?.FirstOrDefault(a =>
                a.StartsWith("eveauth", StringComparison.OrdinalIgnoreCase));
            if (uri is not null)
            {
                completion.TrySetResult(uri);
            }
        }

        singleInstance.Activated += OnActivated;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CallbackTimeout);
            await using var registration = timeout.Token.Register(() => completion.TrySetResult(null));
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
