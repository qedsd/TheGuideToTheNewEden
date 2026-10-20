using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace TheGuideToTheNewEden.Probe;

/// <summary>mock 大模型服务的一条响应。</summary>
/// <param name="Body">响应体（SSE 时是完整的 <c>data: …</c> 流）。</param>
/// <param name="Sse">true = <c>text/event-stream</c>。</param>
internal sealed record MockResponse(string Body, bool Sse = false);

/// <summary>
/// 极简 HTTP mock 服务（<see cref="TcpListener"/> + 手写 HTTP/1.1）：记录每一次请求体，
/// 按调用方给的脚本返回 JSON（非流式）或 SSE（流式）。
/// <para>
/// 用途：<see cref="EngineSuite"/> 要真实走过 <c>ChatClient</c> 的 HTTP 路径，而真相端点会花钱、
/// 且无法按需制造"工具调用 / 未知工具 / 抛错 / 循环上限"这些分支。
/// 引擎的端点由 <c>AssistantContext.Endpoint</c> 注入，**不需要改用户的 settings.json**。
/// </para>
/// </summary>
internal sealed class MockLlmServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Func<int, string, MockResponse> _responder;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;

    /// <summary>按到达顺序记录的请求体（用于断言发出去的消息形状）。</summary>
    public List<string> RequestBodies { get; } = [];

    public int Port { get; }

    /// <summary>OpenAI 兼容端点的基地址（<c>…/v1</c>，适配器会补 <c>/chat/completions</c>）。</summary>
    public string BaseUrl => $"http://127.0.0.1:{Port}/v1";

    public MockLlmServer(Func<int, string, MockResponse> responder)
    {
        _responder = responder;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch (Exception)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(client));
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();
                var body = await ReadRequestAsync(stream);
                int index;
                lock (RequestBodies)
                {
                    index = RequestBodies.Count;
                    RequestBodies.Add(body);
                }

                var response = _responder(index, body);
                var bytes = Encoding.UTF8.GetBytes(response.Body);
                var header = new StringBuilder()
                    .Append("HTTP/1.1 200 OK\r\n")
                    .Append(response.Sse
                        ? "Content-Type: text/event-stream; charset=utf-8\r\n"
                        : "Content-Type: application/json; charset=utf-8\r\n")
                    .Append($"Content-Length: {bytes.Length}\r\n")
                    .Append("Connection: close\r\n\r\n")
                    .ToString();
                await stream.WriteAsync(Encoding.UTF8.GetBytes(header));
                await stream.WriteAsync(bytes);
                await stream.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [mock] 处理请求失败：{ex.Message}");
        }
    }

    /// <summary>读一个 HTTP 请求（只关心中间件：Content-Length + 请求体）。</summary>
    private static async Task<string> ReadRequestAsync(NetworkStream stream)
    {
        var buffer = new List<byte>(8192);
        var chunk = new byte[4096];
        var headerEnd = -1;

        while (headerEnd < 0)
        {
            var read = await stream.ReadAsync(chunk);
            if (read <= 0)
            {
                return string.Empty;
            }

            buffer.AddRange(chunk.Take(read));
            headerEnd = IndexOfHeaderEnd(buffer);
        }

        var headerText = Encoding.ASCII.GetString(buffer.Take(headerEnd).ToArray());
        var contentLength = 0;
        foreach (var line in headerText.Split("\r\n"))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line["Content-Length:".Length..].Trim(), out var length))
            {
                contentLength = length;
            }
        }

        var bodyStart = headerEnd + 4;
        while (buffer.Count - bodyStart < contentLength)
        {
            var read = await stream.ReadAsync(chunk);
            if (read <= 0)
            {
                break;
            }

            buffer.AddRange(chunk.Take(read));
        }

        return Encoding.UTF8.GetString(buffer.Skip(bodyStart).Take(contentLength).ToArray());
    }

    private static int IndexOfHeaderEnd(List<byte> buffer)
    {
        for (var i = 0; i + 3 < buffer.Count; i++)
        {
            if (buffer[i] == (byte)'\r' && buffer[i + 1] == (byte)'\n' && buffer[i + 2] == (byte)'\r' && buffer[i + 3] == (byte)'\n')
            {
                return i;
            }
        }

        return -1;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _listener.Stop();
        }
        catch (Exception)
        {
            // 关闭期的异常无意义
        }

        try
        {
            _acceptLoop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
            // 同上
        }

        _cts.Dispose();
    }

    // ---------- 常用响应构造（用 JsonObject 拼，避免手工转义踩坑） ----------

    /// <summary>OpenAI 兼容：纯文本回答（非流式）。</summary>
    public static MockResponse Text(string text)
    {
        var body = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = text },
                    ["finish_reason"] = "stop",
                },
            },
            ["usage"] = new JsonObject { ["prompt_tokens"] = 11, ["completion_tokens"] = 7 },
        };
        return new MockResponse(body.ToJsonString());
    }

    /// <summary>OpenAI 兼容：要求调用工具（非流式）。</summary>
    public static MockResponse ToolCall(string id, string name, string argumentsJson)
        => ToolCalls([(id, name, argumentsJson)]);

    /// <summary>OpenAI 兼容：一次返回**多个**工具调用（阶段 134 的单轮并行上限用）。</summary>
    public static MockResponse ToolCalls(params (string Id, string Name, string Arguments)[] calls)
    {
        var array = new JsonArray();
        foreach (var (id, name, arguments) in calls)
        {
            array.Add(new JsonObject
            {
                ["id"] = id,
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = name, ["arguments"] = arguments },
            });
        }

        var body = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["message"] = new JsonObject
                    {
                        ["role"] = "assistant",
                        ["content"] = null,
                        ["tool_calls"] = array,
                    },
                    ["finish_reason"] = "tool_calls",
                },
            },
            ["usage"] = new JsonObject { ["prompt_tokens"] = 20, ["completion_tokens"] = 9 },
        };
        return new MockResponse(body.ToJsonString());
    }

    /// <summary>OpenAI 兼容：SSE 流式文本（逐片）。</summary>
    public static MockResponse Stream(params string[] deltas)
    {
        var builder = new StringBuilder();
        foreach (var delta in deltas)
        {
            var chunk = new JsonObject
            {
                ["choices"] = new JsonArray
                {
                    new JsonObject { ["delta"] = new JsonObject { ["content"] = delta } },
                },
            };
            builder.Append("data: ").Append(chunk.ToJsonString()).Append('\n').Append('\n');
        }

        var final = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject { ["delta"] = new JsonObject(), ["finish_reason"] = "stop" },
            },
            ["usage"] = new JsonObject { ["prompt_tokens"] = 30, ["completion_tokens"] = 12 },
        };
        builder.Append("data: ").Append(final.ToJsonString()).Append('\n').Append('\n');
        builder.Append("data: [DONE]").Append('\n').Append('\n');
        return new MockResponse(builder.ToString(), Sse: true);
    }
}
