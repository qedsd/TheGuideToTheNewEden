using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Helpers
{
    /// <summary>
    /// TCP 连接测速（"TCPing"）：连得上就把握手耗时当作延迟，连不上返回 <c>-1</c>。
    /// <para>
    /// 相比最初版本（只有 <see cref="Ping"/>）补齐三件事：
    /// ① <b>连接超时可控</b>——原来只用 <c>Socket.ConnectAsync</c>，超时由操作系统的 SYN 重传决定
    /// （实测 Windows 上可长达 ~21 秒），1 秒一次的测速循环会被一次连接拖住十几秒；
    /// ② <b>失败可以不记日志</b>（<c>logErrors</c>）——"连不上"对测速来说是**正常结果**（就是丢包），
    /// 每失败一次写一条 ERROR 会把日志与错误计数刷满，而 <c>Log.GetErrorCount()</c> 是有业务在读的；
    /// ③ <b>每次调用用自己的 Stopwatch</b>——原来是 <c>static</c> 字段，并发调用会互相把计时重置。
    /// </para>
    /// </summary>
    public static class TCPingHelper
    {
        /// <summary>默认连接超时（毫秒）。</summary>
        public const int DefaultTimeoutMs = 5000;

        /// <summary>
        /// 测一次到 <paramref name="host"/>:<paramref name="port"/> 的 TCP 连接耗时。
        /// 成功返回毫秒（四舍五入到整数），失败/超时/取消一律返回 <c>-1</c>（不抛异常）。
        /// </summary>
        public static async Task<double> Ping(string host, int port)
        {
            return await PingAsync(host, port, DefaultTimeoutMs, true, CancellationToken.None);
        }

        /// <summary>
        /// 测一次 TCP 连接耗时。失败返回 <c>-1</c>（不抛异常）。
        /// </summary>
        /// <param name="timeoutMs">连接超时；超时按失败处理（返回 -1）。</param>
        /// <param name="logErrors">失败时是否记 Error 日志（周期性测速建议传 false）。</param>
        public static async Task<double> PingAsync(string host, int port, int timeoutMs = DefaultTimeoutMs,
            bool logErrors = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(host) || port <= 0 || port > 65535)
            {
                return -1;
            }

            var effectiveTimeout = timeoutMs <= 0 ? DefaultTimeoutMs : timeoutMs;
            var stopwatch = Stopwatch.StartNew();
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                var connectTask = socket.ConnectAsync(host, port);

                // netstandard2.1 没有 Task.WaitAsync，用 WhenAny 自己做超时/取消。
                var finished = await Task.WhenAny(connectTask, Task.Delay(effectiveTimeout, cancellationToken))
                    .ConfigureAwait(false);

                if (finished != connectTask)
                {
                    // 超时或取消：close 会把挂起的连接中止掉。
                    // 但那个 connectTask 随后会以异常结束，必须**观察**掉它，否则会被
                    // TaskScheduler.UnobservedTaskException（App 里挂了处理器）当成未处理异常记一条。
                    _ = connectTask.ContinueWith(
                        t => { _ = t.Exception; },
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);

                    if (logErrors)
                    {
                        Core.Log.Warn($"[TCPing] 连接 {host}:{port} 超时（>{effectiveTimeout}ms）");
                    }

                    return -1;
                }

                await connectTask.ConfigureAwait(false);
                stopwatch.Stop();
                return Math.Round(stopwatch.Elapsed.TotalMilliseconds, 0);
            }
            catch (Exception ex)
            {
                if (logErrors)
                {
                    Core.Log.Error(ex);
                }

                return -1;
            }
            finally
            {
                try
                {
                    if (socket.Connected)
                    {
                        socket.Disconnect(false);
                    }

                    socket.Dispose();
                }
                catch
                {
                    // 关闭阶段的异常没有价值，忽略。
                }
            }
        }
    }
}
