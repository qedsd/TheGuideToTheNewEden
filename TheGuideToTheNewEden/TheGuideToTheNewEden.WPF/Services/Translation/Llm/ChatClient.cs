using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace TheGuideToTheNewEden.WPF.Services.Translation.Llm;

/// <summary>
/// 大模型调用客户端：把"发请求 / 重试 / 超时 / 流式读取 / 错误归一化"集中在一处，
/// 协议差异交给 <see cref="IChatProtocol"/>。
/// <para>
/// 重试策略：429 / 5xx / 网络异常 / 超时 重试 2 次（0.6s、1.8s 退避，尊重 <c>Retry-After</c>）；
/// 4xx（除 429）不重试——参数或凭据问题重试也没意义。流式请求只在**建立连接前**失败时重试（已开始输出就不再重试，避免重复计费）。
/// </para>
/// </summary>
public static class ChatClient
{
    private const int MaxAttempts = 3;

    /// <summary>超时由每次请求自己的 CTS 控制，HttpClient 本身不设超时。</summary>
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
    })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    /// <summary>一次性（非流式）调用。</summary>
    public static async Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var protocol = ChatProtocolFactory.Get(request.Endpoint.Protocol);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var timeout = CreateTimeoutSource(request.Endpoint, cancellationToken);
            try
            {
                using var message = protocol.BuildRequest(request);
                using var response = await Http.SendAsync(message, HttpCompletionOption.ResponseContentRead, timeout.Token).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var error = new ChatException(protocol.DescribeError(response.StatusCode, body), response.StatusCode, body);
                    if (!ShouldRetry(response.StatusCode) || attempt == MaxAttempts)
                    {
                        throw error;
                    }

                    lastError = error;
                    await DelayBeforeRetryAsync(attempt, response, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return protocol.ParseResponse(body);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = new ChatException($"请求超时（超过 {request.Endpoint.TimeoutSeconds} 秒）");
                if (attempt == MaxAttempts)
                {
                    throw lastError;
                }

                await DelayBeforeRetryAsync(attempt, null, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                lastError = new ChatException($"网络错误：{ex.Message}", null, null);
                if (attempt == MaxAttempts)
                {
                    throw lastError;
                }

                await DelayBeforeRetryAsync(attempt, null, cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastError ?? new ChatException("请求失败");
    }

    /// <summary>
    /// 流式调用：逐段返回增量文本。<paramref name="state"/> 会在结束时带上累计文本与 token 用量
    /// （调用方可据此在"没有增量"的协议实现下兜底）。
    /// <para>
    /// 超时是**空闲超时**：每收到一段增量就把计时往后推，因此"生成很久但一直在出字"不会被砍掉，
    /// 只有"连续 <c>TimeoutSeconds</c> 秒一个字都没有"才判超时。
    /// </para>
    /// </summary>
    public static async IAsyncEnumerable<string> StreamAsync(
        ChatRequest request,
        ChatStreamState? state = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        state ??= new ChatStreamState();
        var protocol = ChatProtocolFactory.Get(request.Endpoint.Protocol);
        var idleSeconds = GetTimeoutSeconds(request.Endpoint);

        using var timeout = CreateTimeoutSource(request.Endpoint, cancellationToken);
        using var message = protocol.BuildRequest(request);
        using var response = await Http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            throw new ChatException(protocol.DescribeError(response.StatusCode, body), response.StatusCode, body);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            string? line;
            try
            {
                // 必须用 WaitAsync 才能真正判超时：实测（.NET 10 + HttpClient 响应流）
                // StreamReader.ReadLineAsync(token) 在服务端"卡住不出字"时**不会**因为令牌取消而返回，
                // 于是"卡住"会一直挂着不报错；WaitAsync 是托管等待，空闲超时/取消都能立刻生效。
                line = await reader.ReadLineAsync(CancellationToken.None)
                    .AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(idleSeconds), timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new ChatException($"请求超时（超过 {idleSeconds} 秒没有收到新的内容）");
            }

            if (line is null)
            {
                break;
            }

            var delta = protocol.ParseStreamLine(line, state);
            if (!string.IsNullOrEmpty(delta))
            {
                // 收到增量 → 把空闲计时往后推（整段生成可以远超 TimeoutSeconds）
                timeout.CancelAfter(TimeSpan.FromSeconds(idleSeconds));
                yield return delta;
            }

            if (state.Done)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 连通性自检：发一句极短的请求，回报模型名、耗时与 token 用量（设置页的"测试连接"）。
    /// </summary>
    public static async Task<string> TestAsync(ChatEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        var probe = endpoint.Clone();
        if (probe.MaxTokens <= 0 || probe.MaxTokens > 32)
        {
            probe = new ChatEndpoint
            {
                Protocol = endpoint.Protocol,
                BaseUrl = endpoint.BaseUrl,
                ApiKey = endpoint.ApiKey,
                Model = endpoint.Model,
                ApiVersion = endpoint.ApiVersion,
                Temperature = 0,
                MaxTokens = 32,
                TimeoutSeconds = Math.Max(10, Math.Min(endpoint.TimeoutSeconds, 30)),
            };
        }

        var started = DateTime.UtcNow;
        var completion = await CompleteAsync(
            new ChatRequest
            {
                Endpoint = probe,
                SystemPrompt = "You are a connectivity probe. Reply with exactly: ok",
                Messages = [ChatMessage.User("ping")],
            },
            cancellationToken).ConfigureAwait(false);

        var elapsed = DateTime.UtcNow - started;
        var tokens = completion.Usage.IsEmpty
            ? "未返回用量"
            : $"输入 {completion.Usage.InputTokens} / 输出 {completion.Usage.OutputTokens} token";

        return $"连接成功 · {endpoint.Model} · {elapsed.TotalSeconds:F1}s · {tokens} · 回复「{Truncate(completion.Text, 40)}」";
    }

    private static int GetTimeoutSeconds(ChatEndpoint endpoint) => endpoint.TimeoutSeconds <= 0 ? 60 : endpoint.TimeoutSeconds;

    private static CancellationTokenSource CreateTimeoutSource(ChatEndpoint endpoint, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(GetTimeoutSeconds(endpoint)));
        return source;
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static async Task DelayBeforeRetryAsync(int attempt, HttpResponseMessage? response, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(600 * Math.Pow(3, attempt - 1));
        var retryAfter = response?.Headers.RetryAfter;
        if (retryAfter is not null)
        {
            var suggested = retryAfter.Delta ?? (retryAfter.Date is { } date ? date - DateTimeOffset.UtcNow : null);
            if (suggested is { TotalSeconds: > 0 and <= 30 })
            {
                delay = suggested.Value;
            }
        }

        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }

    private static string Truncate(string text, int length)
    {
        var normalized = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= length ? normalized : normalized[..length] + "…";
    }
}
