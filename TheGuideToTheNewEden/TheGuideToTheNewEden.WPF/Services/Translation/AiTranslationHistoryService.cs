using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>一条翻译记录（AI 翻译页"对话"里的一问一答）。</summary>
public sealed class AiTranslationTurn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime Time { get; set; } = DateTime.Now;

    /// <summary>源语言代码（auto = 自动判定；见 <see cref="TranslationLanguages"/>）。</summary>
    public string From { get; set; } = TranslationLanguages.Auto;

    /// <summary>目标语言代码。</summary>
    public string To { get; set; } = TranslationLanguages.Chinese;

    /// <summary>原文（已预清洗）。</summary>
    public string Original { get; set; } = string.Empty;

    /// <summary>译文；失败时为空。</summary>
    public string Translation { get; set; } = string.Empty;

    /// <summary>模型/耗时/token。</summary>
    public string Meta { get; set; } = string.Empty;

    public bool Success { get; set; } = true;

    /// <summary>失败原因（成功时为空）。</summary>
    public string? Error { get; set; }
}

/// <summary>一个翻译对话（多条记录 + 标题 + 是否带上下文）。</summary>
public sealed class AiTranslationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>标题（取第一条原文的首行，可被用户重命名）。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 本对话是否**每次翻译都带上之前的记录**作为上下文（对话级开关，默认关）。
    /// 打开后不做条数限制：此前所有翻译成功的记录都会按顺序带上。
    /// </summary>
    public bool UseContext { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public List<AiTranslationTurn> Turns { get; set; } = [];
}

/// <summary>
/// AI 翻译页的**对话历史**：存在 <c>%LocalAppData%\TheGuideToTheNewEden\Configs\AiTranslationHistory.json</c>，
/// 支持多个对话（像 AI 桌面端的会话列表），关掉应用再打开还在。
/// <para>
/// 落盘策略：**按对话整体 upsert**（不是整份覆盖），所以"页面 + 弹窗"两个实例同时开着也不会互相抹掉对方的会话；
/// 同时在两个实例里编辑**同一个**对话属于异常用法（没有做逐条合并）。容量上限见 <see cref="MaxSessions"/>/<see cref="MaxTurnsPerSession"/>。
/// </para>
/// </summary>
public static class AiTranslationHistoryService
{
    /// <summary>最多保留多少个对话（超出时丢弃最久未更新的空/旧对话）。</summary>
    public const int MaxSessions = 40;

    /// <summary>单个对话最多保留多少条记录。</summary>
    public const int MaxTurnsPerSession = 200;

    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static List<AiTranslationSession>? _sessions;

    /// <summary>历史文件路径。</summary>
    public static string FilePath => Path.Combine(SettingsService.DataPath, "Configs", "AiTranslationHistory.json");

    /// <summary>载入全部对话（按更新时间倒序；文件损坏时返回空表，不阻塞界面）。</summary>
    public static IReadOnlyList<AiTranslationSession> Load()
    {
        lock (Sync)
        {
            if (_sessions is not null)
            {
                return _sessions.OrderByDescending(s => s.UpdatedAt).ToList();
            }

            var sessions = new List<AiTranslationSession>();
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    sessions = JsonSerializer.Deserialize<List<AiTranslationSession>>(json, JsonOptions) ?? [];
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
            }

            _sessions = sessions;
            return _sessions.OrderByDescending(s => s.UpdatedAt).ToList();
        }
    }

    /// <summary>新增或覆盖一个对话（按 Id upsert）并落盘。</summary>
    public static void Save(AiTranslationSession session)
    {
        if (session is null || session.Turns.Count == 0)
        {
            // 没有任何记录的对话不入库（"新建对话"点开又不翻译就不该留下垃圾）
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

    /// <summary>当前对话数与记录数（设置页/界面提示用）。</summary>
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

        // 丢最久没更新的（空对话优先），保留最近用过的 MaxSessions 个
        var keep = sessions
            .OrderByDescending(s => s.Turns.Count > 0)
            .ThenByDescending(s => s.UpdatedAt)
            .Take(MaxSessions)
            .ToList();

        _sessions = keep;
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
