using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>授权回调的到达方式。</summary>
internal enum AuthCallbackOutcome
{
    /// <summary>已拿到 authorization code。</summary>
    Success,

    /// <summary>用户在授权页点了拒绝（回调带了 <c>error</c>）。</summary>
    Denied,

    /// <summary>在超时时间内没有收到回调。</summary>
    Timeout,
}

/// <summary>回环授权的等待结果。</summary>
internal sealed class AuthCallbackResult
{
    private AuthCallbackResult(AuthCallbackOutcome outcome, string? callbackUri, string? message)
    {
        Outcome = outcome;
        CallbackUri = callbackUri;
        Message = message;
    }

    public AuthCallbackOutcome Outcome { get; }

    /// <summary>完整的回调地址（形如 <c>http://localhost:38471/callback/?code=…</c>）。</summary>
    public string? CallbackUri { get; }

    /// <summary>失败时的说明（拒绝原因 / 超时提示）。</summary>
    public string? Message { get; }

    public bool IsSuccess => Outcome == AuthCallbackOutcome.Success;

    public static AuthCallbackResult Success(string callbackUri) =>
        new(AuthCallbackOutcome.Success, callbackUri, null);

    public static AuthCallbackResult Denied(string? message) =>
        new(AuthCallbackOutcome.Denied, null, message);

    public static AuthCallbackResult Timeout(string? message) =>
        new(AuthCallbackOutcome.Timeout, null, message);
}

/// <summary>
/// 回调结果页上要显示的文字。
/// </summary>
/// <remarks>
/// 由调用方在 **UI 线程**取好语言资源后传入：<see cref="LoopbackAuthServer"/> 的响应是在
/// 后台线程拼的，直接在那里访问 <c>Application.Current</c> 的资源字典属于跨线程访问。
/// </remarks>
internal sealed class LoopbackPageStrings
{
    public string SuccessTitle { get; init; } = string.Empty;

    public string SuccessHint { get; init; } = string.Empty;

    public string FailureTitle { get; init; } = string.Empty;

    public string RetryHint { get; init; } = string.Empty;
}

/// <summary>
/// 授权回调用的本地回环 HTTP 服务器（OAuth 2.0 for Native Apps 的 loopback 流程）。
/// </summary>
/// <remarks>
/// **为什么用回环而不是自定义协议（注册表）**：
/// Windows 的协议激活只能"运行一条命令行"，无法把 URL 投递给已在运行的进程
/// （CCP 文档也写明 multi-instance applications require response routing）。
/// 也就是说，走注册表必然要**多起一个客户端进程**再由它转发命令行，代价是：
/// 进程启动开销、单实例转发链路、以及转发进程误触退出清理的风险（见 <c>Program</c> 的说明）。
/// 回环方案里浏览器直接把回调打进**主实例自己的**监听端口，全程零第二进程。
///
/// **绑定策略**：只绑 <c>127.0.0.1</c> 与 <c>::1</c>——不暴露到局域网、
/// 不触发 Windows 防火墙提示、也不需要 <c>HttpListener</c> 那种 URL ACL（非管理员会直接 AccessDenied）。
/// IPv6 回环是必需的补充：浏览器可能把 <c>localhost</c> 解析成 <c>::1</c>，
/// 只绑 IPv4 会出现"授权页跳转过去了但回调收不到"。
///
/// **端口**：CCP 不允许通配端口（回调地址必须是登记时那个确切值），因此端口固定，
/// 由 <c>Configs/ESILicense.txt</c> 第 2 行的回调地址决定，不在这里写死。
/// </remarks>
internal sealed class LoopbackAuthServer : IDisposable
{
    /// <summary>请求头读取上限，防止异常客户端把内存吃满。</summary>
    private const int MaxRequestBytes = 16 * 1024;

    /// <summary>单个连接从连上到把请求行读出来之间的上限。</summary>
    private static readonly TimeSpan RequestReadTimeout = TimeSpan.FromSeconds(5);

    private readonly Uri _endpoint;
    private readonly string _expectedPath;
    private readonly string _authority;
    private readonly TimeSpan _timeout;
    private readonly LoopbackPageStrings _strings;
    private readonly List<TcpListener> _listeners = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TaskCompletionSource<AuthCallbackResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _disposed;

    private LoopbackAuthServer(Uri endpoint, LoopbackPageStrings strings, TimeSpan timeout)
    {
        _endpoint = endpoint;
        _expectedPath = NormalizePath(endpoint.AbsolutePath);
        _authority = endpoint.GetLeftPart(UriPartial.Authority);
        _strings = strings;
        _timeout = timeout;
    }

    /// <summary>实际监听的端口；未配置回调地址时用于给出建议值。</summary>
    public int Port => _endpoint.Port;

    /// <summary>
    /// 绑定端口并开始监听；任何一步失败都返回 null 并给出可直接展示的原因。
    /// </summary>
    /// <remarks>
    /// 必须在**打开授权页之前**调用：用户点得快时回调可能先于浏览器打开到达。
    /// </remarks>
    public static LoopbackAuthServer? TryStart(
        Uri endpoint,
        LoopbackPageStrings strings,
        TimeSpan timeout,
        out string? error)
    {
        var server = new LoopbackAuthServer(endpoint, strings, timeout);
        try
        {
            server.Bind();
            error = null;
            return server;
        }
        catch (Exception ex)
        {
            server.Dispose();
            error = ex.Message;
            return null;
        }
    }

    private void Bind()
    {
        // IPv4 必须成功——绝大多数情况浏览器走的就是 127.0.0.1。
        _listeners.Add(BindListener(IPAddress.Loopback, _endpoint.Port));

        // IPv6 是补充：绑不上只降级（例如端口被别的进程占在 ::1 上），不影响 IPv4 路径。
        try
        {
            _listeners.Add(BindListener(IPAddress.IPv6Loopback, _endpoint.Port));
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"IPv6 回环 ::1:{_endpoint.Port} 监听失败，本次只监听 127.0.0.1：{ex.Message}");
        }

        foreach (var listener in _listeners)
        {
            _ = Task.Run(() => AcceptLoopAsync(listener));
        }

        var display = _expectedPath == "/" ? $"{_authority}/" : $"{_authority}{_expectedPath}/";
        Core.Log.Info($"授权回调已监听 {display}");
    }

    private static TcpListener BindListener(IPAddress address, int port)
    {
        var listener = new TcpListener(address, port);
        try
        {
            listener.Start();
            return listener;
        }
        catch (SocketException)
        {
            // 端口可能被上一次连接留下的 TIME_WAIT 短暂占用，带 SO_REUSEADDR 再试一次。
            // 只在首次失败时才退让，正常路径不经过这里，因此不会长期敞着"端口可被顶替"的口子。
            listener = new TcpListener(address, port);
            listener.Server.ExclusiveAddressUse = false;
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Start();
            Core.Log.Debug($"回环授权：{address}:{port} 首次绑定失败，已带 SO_REUSEADDR 重新绑定（多为上次连接的 TIME_WAIT）");
            return listener;
        }
    }

    /// <summary>等待浏览器把回调打进来；超时、外部取消、用户拒绝都会返回结果而不是挂起。</summary>
    public async Task<AuthCallbackResult> WaitForCallbackAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        using var registration = timeout.Token.Register(() => _completion.TrySetResult(
            AuthCallbackResult.Timeout($"未在 {FormatDuration(_timeout)}内收到授权回调")));

        return await _completion.Task.ConfigureAwait(false);
    }

    private static string FormatDuration(TimeSpan value)
    {
        return value.TotalMinutes >= 1
            ? $"{value.TotalMinutes:0.#} 分钟"
            : $"{value.TotalSeconds:0} 秒";
    }

    private async Task AcceptLoopAsync(TcpListener listener)
    {
        while (!_shutdown.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Dispose 里的 Stop() 会让挂起的 Accept 抛出（ObjectDisposed/SocketException），正常收尾。
                return;
            }

            // 单独处理，避免一个慢连接堵住 accept。
            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using (client)
            using (var timeout = new CancellationTokenSource(RequestReadTimeout))
            {
                var stream = client.GetStream();
                var requestLine = await ReadRequestLineAsync(stream, timeout.Token).ConfigureAwait(false);
                if (string.IsNullOrEmpty(requestLine))
                {
                    return;
                }

                var target = ExtractTarget(requestLine);
                if (string.IsNullOrEmpty(target))
                {
                    await WriteResponseAsync(stream, 400, "text/plain; charset=utf-8", "Bad Request")
                        .ConfigureAwait(false);
                    return;
                }

                var queryStart = target.IndexOf('?');
                var path = queryStart >= 0 ? target[..queryStart] : target;
                var query = queryStart >= 0 ? target[(queryStart + 1)..] : string.Empty;

                if (!string.Equals(NormalizePath(path), _expectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    // 浏览器加载完页面还会来取 favicon，以及各种探测请求——一律忽略，不结束等待。
                    Core.Log.Debug($"回环授权：忽略无关请求 {path}");
                    await WriteResponseAsync(stream, 404, "text/plain; charset=utf-8", "Not Found")
                        .ConfigureAwait(false);
                    return;
                }

                await HandleCallbackAsync(stream, target, query).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"处理回环授权请求时出错（已忽略）：{ex.Message}");
        }
    }

    private async Task HandleCallbackAsync(Stream stream, string target, string query)
    {
        string? code = null;
        string? error = null;
        string? errorDescription = null;

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var name = pair[..equals];
            var value = Uri.UnescapeDataString(pair[(equals + 1)..]);

            if (name.Equals("code", StringComparison.OrdinalIgnoreCase))
            {
                code = value;
            }
            else if (name.Equals("error", StringComparison.OrdinalIgnoreCase))
            {
                error = value;
            }
            else if (name.Equals("error_description", StringComparison.OrdinalIgnoreCase))
            {
                errorDescription = value;
            }
        }

        if (!string.IsNullOrEmpty(error))
        {
            var message = string.IsNullOrEmpty(errorDescription) ? error : $"{error}：{errorDescription}";
            Core.Log.Warn($"授权被拒绝：{message}");
            _completion.TrySetResult(AuthCallbackResult.Denied(message));
            await WriteResponseAsync(stream, 200, "text/html; charset=utf-8", BuildPage(false, message))
                .ConfigureAwait(false);
            StopListening();
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            // 参数不全（少见）：回一页说明但**不结束等待**，用户还有机会重试。
            Core.Log.Warn($"回环授权：回调缺少 code 参数（{target}）");
            await WriteResponseAsync(stream, 400, "text/html; charset=utf-8", BuildPage(false, "missing code"))
                .ConfigureAwait(false);
            return;
        }

        _completion.TrySetResult(AuthCallbackResult.Success(_authority + target));
        await WriteResponseAsync(stream, 200, "text/html; charset=utf-8", BuildPage(true, null))
            .ConfigureAwait(false);

        // 马上松口端口，用户紧接着再点一次"添加角色"不会撞上端口占用。
        StopListening();
    }

    // ---------- HTTP ----------

    private static async Task<string?> ReadRequestLineAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[1024];
        var text = new StringBuilder();

        while (text.Length < MaxRequestBytes)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 超时或对端提前断开。
                return null;
            }

            if (read <= 0)
            {
                break;
            }

            text.Append(Encoding.ASCII.GetString(buffer, 0, read));

            // 请求行以 CRLF 结束；这里只需拿到第一行，不必读完整个请求头。
            var end = text.ToString().IndexOfAny(new[] { '\r', '\n' });
            if (end >= 0)
            {
                return text.ToString(0, end);
            }
        }

        var trimmed = text.ToString().Trim();
        return trimmed.Length > 0 ? trimmed : null;
    }

    private static string? ExtractTarget(string requestLine)
    {
        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1] : null;
    }

    private static async Task WriteResponseAsync(Stream stream, int status, string contentType, string body)
    {
        var payload = Encoding.UTF8.GetBytes(body);
        var reason = status switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            _ => "OK",
        };

        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status} {reason}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {payload.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n" +
            "\r\n");

        await stream.WriteAsync(header.AsMemory()).ConfigureAwait(false);
        await stream.WriteAsync(payload.AsMemory()).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    private string BuildPage(bool success, string? detail)
    {
        var title = success ? _strings.SuccessTitle : _strings.FailureTitle;
        var hint = success ? _strings.SuccessHint : _strings.RetryHint;
        var accent = success ? "#16a34a" : "#dc2626";
        var mark = success ? "&#10003;" : "&#10005;";
        var detailBlock = string.IsNullOrWhiteSpace(detail)
            ? string.Empty
            : $"<div class=\"detail\">{Encode(detail)}</div>";

        return $$"""
            <!DOCTYPE html>
            <html lang="zh-CN">
            <head>
            <meta charset="utf-8"/>
            <meta name="viewport" content="width=device-width,initial-scale=1"/>
            <title>{{Encode(title)}}</title>
            <style>
            :root{color-scheme:light dark}
            *{box-sizing:border-box}
            body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:#f3f4f6;color:#111827;
                 font-family:"Segoe UI","Microsoft YaHei UI","Microsoft YaHei",system-ui,sans-serif}
            .card{width:min(460px,calc(100vw - 48px));padding:40px 32px;border-radius:16px;background:#fff;
                  box-shadow:0 12px 32px rgba(0,0,0,.10);text-align:center}
            .mark{width:64px;height:64px;margin:0 auto 20px;border-radius:50%;display:flex;align-items:center;justify-content:center;
                  font-size:34px;line-height:1;color:#fff;background:{{accent}}}
            h1{margin:0 0 12px;font-size:22px;font-weight:600}
            p{margin:0;font-size:14px;line-height:1.6;color:#4b5563}
            .detail{margin-top:14px;padding:10px 12px;border-radius:8px;background:#f3f4f6;font-size:12.5px;
                    color:#6b7280;word-break:break-word}
            @media (prefers-color-scheme:dark){
              body{background:#111827;color:#f9fafb}
              .card{background:#1f2937;box-shadow:0 12px 32px rgba(0,0,0,.5)}
              p{color:#9ca3af}
              .detail{background:#111827;color:#9ca3af}
            }
            </style>
            </head>
            <body>
            <main class="card">
              <div class="mark">{{mark}}</div>
              <h1>{{Encode(title)}}</h1>
              <p>{{Encode(hint)}}</p>
              {{detailBlock}}
            </main>
            </body>
            </html>
            """;
    }

    private static string Encode(string text) => text
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "/";
        }

        var trimmed = path.TrimEnd('/');
        if (trimmed.Length == 0)
        {
            return "/";
        }

        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }

    // ---------- 释放 ----------

    private void StopListening()
    {
        foreach (var listener in _listeners)
        {
            try
            {
                listener.Stop();
            }
            catch
            {
                // 已经停掉了。
            }
        }

        _listeners.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _shutdown.Cancel();
        }
        catch
        {
            // 忽略。
        }

        StopListening();

        // 等待方若还挂着（例如上游异常退出），补一个结果免得永远挂着。
        _completion.TrySetResult(AuthCallbackResult.Timeout("授权回调服务器已停止"));

        _shutdown.Dispose();
    }
}
