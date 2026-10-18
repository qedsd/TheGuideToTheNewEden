using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>
/// 一条工具执行记录（落盘用；与界面 <c>ToolStep</c> 对应）。
/// <para>
/// 阶段 132 起这里不只是"显示用的摘要"：<see cref="CallId"/> / <see cref="ArgumentsJson"/> /
/// <see cref="ResultText"/> 是**重建上下文**所必需的（见 <see cref="AssistantContextBuilder"/>），
/// 少了它们就没法把此前的工具往返重放给模型。
/// </para>
/// </summary>
public sealed class AiAssistantStep
{
    /// <summary>工具调用 ID（重建历史时与 tool 消息配对）。旧数据可能为空 → 该步骤不参与重建。</summary>
    public string CallId { get; set; } = string.Empty;

    /// <summary>工具轮号（同一轮 = 同一条 assistant 消息里的多个调用）。旧数据默认 0（全归一轮，仍合法）。</summary>
    public int Round { get; set; }

    public string ToolName { get; set; } = string.Empty;

    /// <summary>人话参数摘要（界面显示）。</summary>
    public string ParamSummary { get; set; } = string.Empty;

    /// <summary>调用参数 JSON 原文（重建历史用；截断到 <see cref="AssistantContextBuilder.MaxStoredArgumentsChars"/>）。</summary>
    public string ArgumentsJson { get; set; } = string.Empty;

    /// <summary>结果摘要（界面显示，160 字符）。</summary>
    public string ResultSummary { get; set; } = string.Empty;

    /// <summary>回填给模型的结果全文（重建历史用；截断到 <see cref="AssistantContextBuilder.MaxStoredResultChars"/>）。</summary>
    public string ResultText { get; set; } = string.Empty;

    public bool Success { get; set; } = true;
}

/// <summary>一条助手问答（用户请求 + 回答 + 工具执行摘要）。</summary>
public sealed class AiAssistantTurn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime Time { get; set; } = DateTime.Now;

    /// <summary>用户请求原文。</summary>
    public string UserText { get; set; } = string.Empty;

    /// <summary>助手最终回答；失败/停止时为空。</summary>
    public string AnswerText { get; set; } = string.Empty;

    /// <summary>本次执行过的工具（按顺序）。</summary>
    public List<AiAssistantStep> Steps { get; set; } = [];

    /// <summary>模型 · 耗时 · token · 工具次数。</summary>
    public string Meta { get; set; } = string.Empty;

    public bool Success { get; set; } = true;

    /// <summary>失败原因（成功时为空）。</summary>
    public string? Error { get; set; }

    /// <summary>是否被用户停止。</summary>
    public bool Stopped { get; set; }
}

/// <summary>一个助手对话（多条问答 + 标题 + 是否带上下文）。</summary>
public sealed class AiAssistantSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>标题（取第一条请求的首行，可被用户重命名）。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 本对话是否**每次都带上此前的完整历史**（对话级开关，阶段 132 起默认开）。
    /// <para>
    /// 打开时带的是重建后的消息序列（含工具往返），不是"问 / 答"两段文本；
    /// **记忆只存在于单个对话**，不同对话之间不共享，也没有跨对话长期记忆。
    /// 超出 <c>Assistant.MaxContextChars</c> 预算时最旧的轮次整轮省略。
    /// </para>
    /// </summary>
    public bool UseContext { get; set; } = true;

    /// <summary>
    /// 内部迁移标记（阶段 132）：<c>null</c> = 这份对话来自"默认不带上下文"的旧版本。
    /// <para>
    /// 载入时若为 <c>null</c>，把 <see cref="UseContext"/> 一次性翻成 <c>true</c> 并打上标记——
    /// 旧文件里的 <c>false</c> 绝大多数是旧默认值而不是用户的选择。打过标记之后，用户在对话底栏
    /// 关掉它就是真的关掉（不会再被翻回来）。
    /// </para>
    /// </summary>
    public bool? ContextDefaultV2 { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public List<AiAssistantTurn> Turns { get; set; } = [];
}

/// <summary>
/// AI 助手的**对话历史**：存在 <c>%LocalAppData%\TheGuideToTheNewEden\Configs\AiAssistantHistory.json</c>。
/// <para>
/// 落盘策略与 <c>AiTranslationHistoryService</c> 一致：**按对话整体 upsert**（不是整份覆盖），
/// 页面的会话列表与弹窗共享同一份内存态，关掉应用再打开还在。
/// 确认挂起状态不入库（只记录"已执行/已拒绝"的结果步骤，见 <see cref="AiAssistantStep"/>）。
/// 这里存的就是**同一个对话的记忆**——不同对话之间不共享，也没有跨对话的长期记忆。
/// </para>
/// </summary>
public static class AiAssistantHistoryService
{
    /// <summary>最多保留多少个对话。</summary>
    public const int MaxSessions = 40;

    /// <summary>单个对话最多保留多少条记录。</summary>
    public const int MaxTurnsPerSession = 200;

    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static List<AiAssistantSession>? _sessions;

    /// <summary>历史文件路径。</summary>
    public static string FilePath => Path.Combine(SettingsService.DataPath, "Configs", "AiAssistantHistory.json");

    /// <summary>载入全部对话（按更新时间倒序；文件损坏时返回空表，不阻塞界面）。</summary>
    public static IReadOnlyList<AiAssistantSession> Load()
    {
        lock (Sync)
        {
            if (_sessions is not null)
            {
                return _sessions.OrderByDescending(s => s.UpdatedAt).ToList();
            }

            var sessions = new List<AiAssistantSession>();
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    sessions = JsonSerializer.Deserialize<List<AiAssistantSession>>(json, JsonOptions) ?? [];
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                sessions = [];
            }

            foreach (var session in sessions)
            {
                if (session.Id == Guid.Empty)
                {
                    session.Id = Guid.NewGuid();
                }

                session.Turns ??= [];
                foreach (var turn in session.Turns)
                {
                    turn.Steps ??= [];
                }

                // 阶段 132：上下文语义变了（同一对话内**默认**带上完整历史，含工具往返；记忆仅限单对话）。
                // 旧文件里的 UseContext=false 大多是"旧默认值"而不是用户的选择，因此一次性翻成 true 并打标记；
                // 之后用户在对话底栏关掉它会被尊重（标记已存在，不再动）。
                if (session.ContextDefaultV2 is not true)
                {
                    session.UseContext = true;
                    session.ContextDefaultV2 = true;
                }
            }

            _sessions = sessions;
            return _sessions.OrderByDescending(s => s.UpdatedAt).ToList();
        }
    }

    /// <summary>新增或覆盖一个对话（按 Id upsert）并落盘。</summary>
    public static void Save(AiAssistantSession session)
    {
        if (session is null || session.Turns.Count == 0)
        {
            // 没有记录的对话不入库
            return;
        }

        session.UpdatedAt = DateTime.Now;
        if (session.Turns.Count > MaxTurnsPerSession)
        {
            session.Turns.RemoveRange(0, session.Turns.Count - MaxTurnsPerSession);
        }

        lock (Sync)
        {
            var sessions = _sessions ?? [];
            var index = sessions.FindIndex(s => s.Id == session.Id);
            if (index >= 0)
            {
                sessions[index] = session;
            }
            else
            {
                sessions.Add(session);
            }

            _sessions = sessions;
            TrimLocked();
            WriteLocked();
        }
    }

    /// <summary>删除一个对话。</summary>
    public static void Delete(Guid sessionId)
    {
        lock (Sync)
        {
            var sessions = _sessions ?? [];
            if (sessions.RemoveAll(s => s.Id == sessionId) > 0)
            {
                _sessions = sessions;
                WriteLocked();
            }
        }
    }

    /// <summary>清空全部对话。</summary>
    public static void Clear()
    {
        lock (Sync)
        {
            _sessions = [];
            WriteLocked();
        }
    }

    /// <summary>当前对话数与记录数。</summary>
    public static (int Sessions, int Turns) Count()
    {
        var sessions = Load();
        return (sessions.Count, sessions.Sum(s => s.Turns.Count));
    }

    private static void TrimLocked()
    {
        var sessions = _sessions ?? [];
        if (sessions.Count <= MaxSessions)
        {
            return;
        }

        _sessions = sessions
            .OrderByDescending(s => s.Turns.Count > 0)
            .ThenByDescending(s => s.UpdatedAt)
            .Take(MaxSessions)
            .ToList();
    }

    private static void WriteLocked()
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(_sessions ?? [], JsonOptions));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
