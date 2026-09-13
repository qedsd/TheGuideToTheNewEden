using System.Net;
using System.Net.Http;

namespace TheGuideToTheNewEden.WPF.Services.Translation.Llm;

/// <summary>
/// 大模型协议适配器：只负责"怎么把一个对话请求发出去、怎么把回答读回来"，
/// 与翻译语义（术语注入、提示词、后校验、缓存）完全解耦。
/// <para>
/// 各协议只在四处不同：认证头、请求体字段、system 提示位置、流式帧格式——都在实现类里收敛。
/// 新增一个协议 = 加一个实现类 + 在 <see cref="ChatProtocolFactory"/> 注册一行。
/// </para>
/// </summary>
public interface IChatProtocol
{
    /// <summary>协议标识（持久化到设置里，勿随意改名）。</summary>
    string Key { get; }

    /// <summary>界面显示名的本地化键。</summary>
    string DisplayNameKey { get; }

    /// <summary>协议默认服务地址（界面上作为占位提示与"恢复默认"用）。</summary>
    string DefaultBaseUrl { get; }

    /// <summary>协议默认模型名。</summary>
    string DefaultModel { get; }

    /// <summary>构造一次 HTTP 请求（含 URL、认证头、JSON 体）。重试时会对同一个 <see cref="ChatRequest"/> 重新调用。</summary>
    HttpRequestMessage BuildRequest(ChatRequest request);

    /// <summary>解析一次性（非流式）响应体。</summary>
    ChatCompletion ParseResponse(string json);

    /// <summary>
    /// 解析流式响应里的一"行"，把增量文本返回（没有增量返回 null），
    /// 并把结束标记/token 用量写进 <paramref name="state"/>。
    /// 非数据行（空行、SSE 注释、<c>event:</c> 行）返回 null。
    /// </summary>
    string? ParseStreamLine(string line, ChatStreamState state);

    /// <summary>把 HTTP 错误响应归一化成一句可直接展示给用户的话。</summary>
    string DescribeError(HttpStatusCode statusCode, string? responseBody);
}
