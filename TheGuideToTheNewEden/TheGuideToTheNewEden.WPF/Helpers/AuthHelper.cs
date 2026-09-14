using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// ESI 授权回调的地址解析、等待与 code 解析。
/// </summary>
/// <remarks>
/// 现行方案是**本地回环**（<see cref="LoopbackAuthServer"/>）：浏览器直接把回调打进主实例，
/// 不产生第二个进程。下面自定义 URL 协议（注册表）那一组是为兼容旧配置而**保留的代码**，
/// 授权流程已不再调用它，也不再在授权时写注册表。
/// </remarks>
internal static class AuthHelper
{
    /// <summary>等待回调的上限；超时按"没收到回调"处理，避免授权流程永久挂起。</summary>
    public static TimeSpan CallbackTimeout { get; set; } = TimeSpan.FromMinutes(5);

    #region 回环回调（现行方案）

    /// <summary>
    /// 建议使用的回调端口。
    /// 端口必须与 EVE 开发者后台登记的 Callback URL **完全一致**（CCP 不支持通配端口），
    /// 所以真正的取值来自 <c>Configs/ESILicense.txt</c> 第 2 行，这里只是给配置时用的建议值。
    /// </summary>
    public const int DefaultCallbackPort = 38471;

    /// <summary>建议使用的回调路径。</summary>
    public const string DefaultCallbackPath = "/callback/";

    /// <summary>建议在开发者后台与 <c>ESILicense.txt</c> 里填写的完整回调地址。</summary>
    public static string SuggestedCallbackUrl => $"http://localhost:{DefaultCallbackPort}{DefaultCallbackPath}";

    /// <summary>当前配置的回调地址；未配置时返回建议值（用于界面展示与复制）。</summary>
    public static string GetCallbackUrlForDisplay()
    {
        var configured = Core.Config.ESICallback?.Trim();
        return string.IsNullOrWhiteSpace(configured) ? SuggestedCallbackUrl : configured;
    }

    /// <summary>
    /// 解析并校验回环回调地址（即 <c>Configs/ESILicense.txt</c> 第 2 行 → <see cref="Core.Config.ESICallback"/>）。
    /// </summary>
    /// <remarks>
    /// 校验失败**不能**照常打开授权页：授权 URL 里的 <c>redirect_uri</c> 就是这里取的值，
    /// 与开发者后台登记不一致时 CCP 会直接拒绝授权，用户在浏览器里看到的是英文报错页，
    /// 比"提前失败并说清怎么改"难排查得多。
    /// </remarks>
    public static bool TryGetLoopbackEndpoint(out Uri endpoint, out string? error)
    {
        endpoint = null!;

        var raw = Core.Config.ESICallback?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = EndpointGuidance("尚未配置回调地址（Configs/ESILicense.txt 第 2 行为空）");
            return false;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            error = EndpointGuidance($"回调地址无法解析：{raw}");
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp)
        {
            error = EndpointGuidance($"回调地址必须使用 http（本地回环不允许 https）：{raw}");
            return false;
        }

        if (!IsLoopbackHost(uri.Host))
        {
            error = EndpointGuidance($"回调地址必须指向本机回环地址：{raw}");
            return false;
        }

        if (uri.Port <= 0)
        {
            error = EndpointGuidance($"回调地址必须写明确切的端口（CCP 不支持通配端口）：{raw}");
            return false;
        }

        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            error = EndpointGuidance($"回调地址不能带查询串或片段：{raw}");
            return false;
        }

        endpoint = uri;
        error = null;
        return true;
    }

    private static bool IsLoopbackHost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || host.Equals("127.0.0.1", StringComparison.Ordinal)
               || host.Equals("::1", StringComparison.Ordinal)
               || host.Equals("[::1]", StringComparison.Ordinal);
    }

    private static string EndpointGuidance(string reason)
    {
        return $"{reason}。请把 EVE 开发者后台的 Callback URL 与 Configs/ESILicense.txt 第 2 行都设为 {SuggestedCallbackUrl}";
    }

    /// <summary>
    /// 取回调结果页要用的文案。
    /// 必须在 UI 线程调用（语言资源字典不是线程安全的），再交给后台线程拼响应。
    /// </summary>
    public static LoopbackPageStrings LoadPageStrings()
    {
        return new LoopbackPageStrings
        {
            SuccessTitle = FindString("AuthCallback.SuccessTitle"),
            SuccessHint = FindString("AuthCallback.SuccessHint"),
            FailureTitle = FindString("AuthCallback.FailureTitle"),
            RetryHint = FindString("AuthCallback.RetryHint"),
        };
    }

    private static string FindString(string key)
    {
        return Application.Current?.TryFindResource(key) as string ?? key;
    }

    #endregion

    /// <summary>
    /// 从回调地址中解析 authorization code。
    /// 不依赖参数顺序（WinUI 版用 Split('=','&amp;')[1]，遇到顺序变化或额外参数就会取错）。
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

    #region 自定义 URL 协议 / 注册表（保留，授权流程已不使用）

    /*
     * 这一组是走"浏览器 → 自定义协议 → 第二个客户端进程 → 单实例转发"的旧通道。
     *
     * 停用原因：Windows 的协议激活只能"运行一条命令行"，无法把 URL 投递给已在运行的进程，
     * 因此回调必然要多起一个客户端进程再转发；而 CCP 不允许为同一个 SSO 应用登记两个
     * Callback URL，所以两种通道只能二选一。现改为回环方案（零第二进程）。
     *
     * 代码保留的用途：
     *   - 设置 → 测试 → 回环回调 之外的"HKCR 注册表"卡片仍可直接读写这项注册表，便于旧环境排查；
     *   - 安装包（TheGuideToTheNewEden.nsi 的 Protocol 段）仍会预注册协议，不动它无副作用。
     * 另：App.OnSingleInstanceActivated 仍会识别 eveauth 命令行参数，避免误判成"用户又开了一次程序"。
     */

    /// <summary>统一使用一个协议名（WinUI 版存在 neweden2/neweden3 混用的问题）。</summary>
    public const string ProtocolName = "eveauth-qedsd-neweden3";

    /// <summary>机器级注册（安装包写入，对所有用户有效；写它需要管理员权限）。</summary>
    private const string MachineRoot = @"HKEY_CLASSES_ROOT\" + ProtocolName;
    private const string MachineCommandKey = MachineRoot + @"\shell\open\command";

    /// <summary>当前用户级注册（不需要管理员权限；HKEY_CLASSES_ROOT 读取时会与机器级合并）。</summary>
    private const string UserRoot = @"HKEY_CURRENT_USER\Software\Classes\" + ProtocolName;
    private const string UserCommandKey = UserRoot + @"\shell\open\command";

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
    /// 等待浏览器重定向回来的 eveauth 回调地址（旧通道，授权流程已不再调用）。
    /// 授权页把浏览器重定向到自定义协议，由新启动的第二个进程把命令行交给单实例，
    /// 主实例的 SingleInstanceHelper 随即触发 Activated。
    /// </summary>
    /// <returns>回调地址；超时或外部取消时返回 null。</returns>
    public static async Task<string?> WaitForProtocolCallbackAsync(CancellationToken cancellationToken)
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

    #endregion
}
