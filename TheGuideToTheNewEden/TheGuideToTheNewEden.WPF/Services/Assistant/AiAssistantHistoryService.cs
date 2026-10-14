using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>一条工具执行记录（落盘用；与界面 <c>ToolStep</c> 对应，但不含实时状态）。</summary>
public sealed class AiAssistantStep
{
    public string ToolName { get; set; } = string.Empty;

    public string ParamSummary { get; set; } = string.Empty;

    public string ResultSummary { get; set; } = string.Empty;

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

    /// <summary>本对话是否**每次都带上此前的问答**作为上下文（对话级开关，默认关；不限条数）。</summary>
    public bool UseContext { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public List<AiAssistantTurn> Turns { get; set; } = [];
}

/// <summary>
/// AI 助手的**对话历史**：存在 <c>%LocalAppData%\TheGuideToTheNewEden\Configs\AiAssistantHistory.json</c>。
/// <para>
/// 落盘策略与 <c>AiTranslationHistoryService</c> 一致：**按对话整体 upsert**（不是整份覆盖），
/// 页面的会话列表与弹窗共享同一份内存态，关掉应用再打开还在。
/// 确认挂起状态不入库（只记录"已执行"的结果）。
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
