using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Esi;

/// <summary>URL 构造结果（<see cref="EsiGateway.BuildUrl"/> 的返回；错误非空时不要发请求）。</summary>
/// <param name="Url">完整 URL。</param>
/// <param name="Error">致命错误（缺必填参数等）。</param>
/// <param name="Notes">非致命提示（忽略了不认识的查询参数等），拼进结果正文。</param>
/// <param name="Language">要写进 <c>Accept-Language</c> 的语言（op 支持且未显式指定时自动给）。</param>
public sealed record EsiUrlResult(string Url, string? Error, IReadOnlyList<string> Notes, string? Language);

/// <summary>
/// ESI 通用网关：按操作索引直接调用 ESI（零端点代码），供 AI 助手的
/// <c>esi_read</c> / <c>esi_write</c> 使用。
/// <para>
/// 设计要点：
/// <list type="bullet">
///   <item><b>走 HTTP 而不是反射 EVEStandard</b>：库内没有路由/权限元数据（见 <see cref="EsiOperationIndex"/>），
///     而规格里有；直接 HTTP 也顺带覆盖了库可能滞后于规格的新端点。OAuth 仍由 <c>CharacterStore</c> /
///     <c>CharacterToolHelper</c> 负责（令牌刷新走库里的 SSO）。</item>
///   <item><b>服务器按角色的服务器选</b>：base URL 由 <c>GameServerSelectorService</c> 决定
///     （国服 <c>esi.evepc.163.com</c> / 欧服 <c>esi.evetech.net</c>），与 <c>CharacterStore</c>
///     加载角色用的那份 Auth 文件同源，不会出现"拿着国服令牌打欧服"。</item>
///   <item><b>不分页全量</b>：默认只取第 1 页并回报总页数；<c>allPages</c> 最多取设置里的页数
///     （默认 3）。整星域订单那种几百页的量级不能交给"一问一答"。</item>
///   <item><b>ESI 错误配额护栏</b>：连续 4xx/5xx 会被 ESI 临时封禁，因此读 <c>X-ESI-Error-Limit-Remain</c>，
///     低于阈值直接拒绝调用（而不是把配额打光）。</item>
///   <item>写操作一律经 <see cref="ITool.RequiresConfirmation"/> 挂起（见 <c>EsiWriteTool</c>），
///     本类只负责"确认之后真的执行"。</item>
/// </list>
/// </para>
/// </summary>
public static class EsiGateway
{
    public const string TranquilityBase = "https://esi.evetech.net";

    public const string SerenityBase = "https://esi.evepc.163.com";

    /// <summary>单次请求超时（秒）。</summary>
    private const int RequestTimeoutSeconds = 30;

    private static readonly HttpClient Http = CreateClient();

    private static readonly object LimitLock = new();
    private static int? _errorLimitRemain;
    private static DateTimeOffset? _errorLimitReset;

    /// <summary>当前服务器的 ESI 数据地址。</summary>
    public static string BaseUrl => GameServerSelectorService.Value == GameServerType.Serenity
        ? SerenityBase
        : TranquilityBase;

    /// <summary>上一次响应带来的 ESI 错误配额状态（诊断与护栏用）。</summary>
    public static (int? Remain, DateTimeOffset? Reset) ErrorLimit
    {
        get
        {
            lock (LimitLock)
            {
                return (_errorLimitRemain, _errorLimitReset);
            }
        }
    }

    // ==================================================================
    //  一、纯函数（探针可直接断言，不发请求）
    // ==================================================================

    /// <summary>拒绝路径匹配：设置里的黑名单（每行一条，支持 <c>*</c> 通配）。命中返回人话原因。</summary>
    public static string? DenyReason(EsiOperation operation, string? deniedPatterns)
    {
        if (string.IsNullOrWhiteSpace(deniedPatterns))
        {
            return null;
        }

        foreach (var raw in deniedPatterns.Split(['\n', '\r', ',', '，', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Matches(raw, operation.Signature) || Matches(raw, operation.Path) || Matches(raw, operation.Id))
            {
                return $"该操作被设置里的「禁止调用的路径」挡住（规则 {raw}）。要放行请到 设置 → AI 助手 修改。";
            }
        }

        return null;
    }

    private static bool Matches(string pattern, string text)
    {
        var normalized = text.Replace('\n', ' ').Trim();
        if (pattern.Contains('*'))
        {
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(
                normalized, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return normalized.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 构造请求 URL 并校验参数。路径参数缺失时报错（<c>character_id</c> / <c>corporation_id</c>
    /// 分别默认取当前角色与其军团）；查询参数只接受索引里声明过的名字，其余记为提示。
    /// </summary>
    public static EsiUrlResult BuildUrl(
        EsiOperation operation,
        string baseUrl,
        JsonElement path,
        JsonElement query,
        long characterId,
        long corporationId,
        string? language)
    {
        var notes = new List<string>();
        var url = new StringBuilder(baseUrl.TrimEnd('/')).Append(operation.Path);

        foreach (var param in operation.PathParams)
        {
            var value = ReadValue(path, param.Name);
            if (string.IsNullOrWhiteSpace(value))
            {
                value = param.Name switch
                {
                    "character_id" when characterId > 0 => characterId.ToString(),
                    "corporation_id" when corporationId > 0 => corporationId.ToString(),
                    _ => null,
                };
            }
            else if (EsiResponseEnricher.IsIdField(param.Name))
            {
                // 模型可能把富化后的 "吉他 (30000142)" 原样传回来 → 还原成 ID
                value = EsiResponseEnricher.UnwrapId(value) ?? value;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return new EsiUrlResult(string.Empty,
                    $"缺少路径参数 {param.Name}{DescribeParamHint(param)}。请在 path 里给出，例如 path: {{ \"{param.Name}\": 123 }}。",
                    notes, null);
            }

            url.Replace("{" + param.Name + "}", Uri.EscapeDataString(value));
        }

        var pending = url.ToString();
        if (pending.Contains('{'))
        {
            return new EsiUrlResult(string.Empty, $"路径模板未被完全替换：{pending}（索引与规格可能不一致，请重跑生成脚本）。", notes, null);
        }

        var queryBuilder = new List<string>();
        var declared = operation.Query.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        if (query.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in query.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    continue;
                }

                if (!declared.ContainsKey(property.Name))
                {
                    notes.Add($"忽略了不支持的查询参数 {property.Name}");
                    continue;
                }

                var text = property.Value.ValueKind == JsonValueKind.Array
                    ? string.Join(",", property.Value.EnumerateArray().Select(v => Scalar(v)))
                    : Scalar(property.Value);
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (EsiResponseEnricher.IsIdField(property.Name) && EsiResponseEnricher.UnwrapId(text) is { } id)
                {
                    // 同 path：把富化后的 "伏尔戈 (10000002)" 还原成 10000002
                    text = id;
                }

                queryBuilder.Add($"{Uri.EscapeDataString(property.Name)}={Uri.EscapeDataString(text)}");
            }
        }

        foreach (var param in operation.Query.Where(p => p.Required))
        {
            if (queryBuilder.All(q => !q.StartsWith(param.Name + "=", StringComparison.OrdinalIgnoreCase)))
            {
                return new EsiUrlResult(string.Empty,
                    $"缺少必填查询参数 {param.Name}{DescribeParamHint(param)}。请在 query 里给出。",
                    notes, null);
            }
        }

        if (queryBuilder.Count > 0)
        {
            pending += (pending.Contains('?') ? "&" : "?") + string.Join("&", queryBuilder);
        }

        return new EsiUrlResult(pending, null, notes, operation.Language ? language : null);
    }

    private static string DescribeParamHint(EsiParam param)
    {
        var hint = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(param.Desc))
        {
            hint.Append('（').Append(param.Desc).Append('）');
        }

        if (param.Enum is { Count: > 0 })
        {
            hint.Append("，取值：").Append(string.Join("/", param.Enum));
        }

        return hint.ToString();
    }

    /// <summary>HTTP 失败的归一化说明（403 会带上"要哪个 scope / 可能要军团职位"）。</summary>
    public static string DescribeFailure(HttpStatusCode status, string? body, EsiOperation operation)
    {
        var excerpt = Excerpt(body);
        var reason = status switch
        {
            HttpStatusCode.BadRequest => "参数错误（400）：ESI 认为请求参数不合法",
            HttpStatusCode.Unauthorized => "令牌无效或已过期（401）：请到「角色」页重新授权该角色",
            HttpStatusCode.Forbidden => operation.Corp
                ? "没有权限（403）：该军团端点需要相应 scope，且**该角色在军团里要有对应职位**"
                : "没有权限（403）：该角色缺少所需 scope（到 设置 → 权限选择 勾选后重新授权角色）",
            HttpStatusCode.NotFound => "找不到资源（404）：路径里的 ID 可能不存在，或该端点在本服务器上不可用",
            HttpStatusCode.UnprocessableEntity => "请求体不合法（422）：请检查字段名与取值范围",
            (HttpStatusCode)420 => "触发了 ESI 错误配额（420）：短时间内错误太多，稍后再试",
            HttpStatusCode.TooManyRequests => "请求过多（429）：稍后再试",
            HttpStatusCode.GatewayTimeout => "ESI 网关超时（504）：稍后重试",
            _ => status >= HttpStatusCode.InternalServerError
                ? $"ESI 服务端错误（{(int)status}）：稍后重试"
                : $"请求失败（{(int)status}）",
        };

        var scopes = operation.Scopes.Count > 0 ? $"\n所需 scope：{string.Join(", ", operation.Scopes)}" : string.Empty;
        return $"调用失败：{reason}。{scopes}{(excerpt.Length > 0 ? "\nESI 返回：" + excerpt : string.Empty)}";
    }

    /// <summary>把一个操作渲染成"给模型看的说明书"（<c>esi_describe</c> 用）。</summary>
    public static string Describe(EsiOperation operation)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"操作：{operation.Id}");
        builder.AppendLine($"请求：{operation.Signature}");
        if (!string.IsNullOrWhiteSpace(operation.Summary))
        {
            builder.AppendLine($"说明：{operation.Summary}{(string.IsNullOrWhiteSpace(operation.Tag) ? string.Empty : $"（{operation.Tag}）")}");
        }

        builder.AppendLine(operation.Scopes.Count > 0
            ? $"权限：{string.Join(", ", operation.Scopes)}{(operation.Corp ? "（还需军团职位）" : string.Empty)}"
            : "权限：无需授权（匿名端点）");

        if (operation.PathParams.Count > 0)
        {
            builder.AppendLine("路径参数（写在 path 里）：");
            foreach (var param in operation.PathParams)
            {
                builder.AppendLine($"  - {param.Name}（{param.Type}{(param.Required ? "，必填" : string.Empty)}）"
                                   + DefaultHint(param, param.Name is "character_id" or "corporation_id"));
            }
        }

        if (operation.Query.Count > 0)
        {
            builder.AppendLine("查询参数（写在 query 里）：");
            foreach (var param in operation.Query)
            {
                builder.AppendLine($"  - {param.Name}（{param.Type}{(param.Required ? "，必填" : string.Empty)}）"
                                   + DefaultHint(param, false));
            }
        }

        if (operation.Body is { } body)
        {
            builder.AppendLine(DescribeBody(body));
        }

        builder.AppendLine(operation.Pageable
            ? "分页：支持 page 参数；默认只取第 1 页，allPages=true 可多取（受设置里的页数上限约束）。"
            : "分页：该端点不分页。");

        var example = operation.Write
            ? $"esi_write {{ \"operation\": \"{operation.Id}\"{(operation.Body is null ? string.Empty : ", \"body\": { … }")} }}"
            : $"esi_read {{ \"operation\": \"{operation.Id}\" }}";
        builder.Append($"调用：{example}");
        if (operation.Write)
        {
            builder.Append("\n注意：这是写操作，调用前会先请用户确认（确认卡片会显示本次将执行的动作）。");
        }

        return builder.ToString();
    }

    private static string DescribeBody(EsiBody body)
    {
        var builder = new StringBuilder();
        var shape = body.Type == "array"
            ? $"array<{body.Item ?? "object"}>"
            : body.Type;
        builder.AppendLine($"请求体（body，{shape}{(body.Required ? "，必填" : string.Empty)}）"
                           + (body.MinItems is not null || body.MaxItems is not null
                               ? $"（元素数 {body.MinItems ?? 0}–{body.MaxItems?.ToString() ?? "不限"}）"
                               : string.Empty)
                           + "：");

        foreach (var prop in body.Props ?? body.ItemProps ?? [])
        {
            builder.AppendLine($"  - {prop.Name}（{prop.Type}{(prop.Item is null ? string.Empty : $"<{prop.Item}>")}"
                               + $"{(prop.Required ? "，必填" : string.Empty)}）"
                               + (prop.Enum is { Count: > 0 } ? $" 取值：{string.Join("/", prop.Enum)}" : string.Empty)
                               + (string.IsNullOrWhiteSpace(prop.Desc) ? string.Empty : $" {prop.Desc}"));
        }

        return builder.ToString().TrimEnd();
    }

    private static string DefaultHint(EsiParam param, bool isId)
    {
        var parts = new List<string>();
        var def = param.DefaultText;
        if (!string.IsNullOrWhiteSpace(def))
        {
            parts.Add($"默认 {def}");
        }

        if (isId)
        {
            parts.Add("省略 = 当前角色/其军团");
        }

        if (param.Enum is { Count: > 0 })
        {
            parts.Add($"取值 {string.Join("/", param.Enum)}");
        }

        if (param.Min is not null || param.Max is not null)
        {
            parts.Add($"范围 {param.Min?.ToString() ?? "不限"}–{param.Max?.ToString() ?? "不限"}");
        }

        if (!string.IsNullOrWhiteSpace(param.Desc))
        {
            parts.Add(param.Desc!);
        }

        return parts.Count == 0 ? string.Empty : $"　{string.Join("；", parts)}";
    }

    // ==================================================================
    //  二、调用
    // ==================================================================

    /// <summary>
    /// 执行一个操作。返回给模型的文本（成功 = 头行 + 整形后的正文；失败 = 归一化原因）。
    /// 除取消外不抛异常。
    /// </summary>
    public static async Task<string> InvokeAsync(
        EsiOperation operation,
        ToolContext ctx,
        JsonElement args,
        bool allowWrite,
        CancellationToken ct)
    {
        if (!AiAssistantSettingService.EsiEnabled)
        {
            return "ESI 通用网关已在设置里关闭（设置 → AI 助手 → ESI 全量访问）。";
        }

        if (operation.Write && !allowWrite)
        {
            return $"{operation.Signature} 是写操作，请改用 esi_write（它会先请用户确认）。";
        }

        if (allowWrite && !AiAssistantSettingService.EsiAllowWrites)
        {
            return "ESI 写操作已在设置里关闭（设置 → AI 助手 → 允许 ESI 写操作）。只读端点仍可用 esi_read。";
        }

        var denied = DenyReason(operation, AiAssistantSettingService.EsiDeniedPaths);
        if (denied is not null)
        {
            return denied;
        }

        // 角色：需要授权的端点必须解析出角色；匿名端点也允许带上角色（有的端点对登录用户返回更多）
        var characterName = ToolArgs.ReadString(args, "characterName");
        CharacterContext? characterCtx = null;
        if (operation.Auth || !string.IsNullOrWhiteSpace(characterName))
        {
            var (resolved, error) = await CharacterToolHelper.ResolveAsync(ctx, characterName, ct);
            if (error is not null)
            {
                return operation.Auth ? $"该端点需要角色授权，但{error}" : error;
            }

            characterCtx = resolved;
        }

        var character = characterCtx?.Character ?? ctx.Character;
        var granted = EsiOperationIndex.GetGrantedScopes(character);
        var missing = EsiOperationIndex.MissingScopes(operation, granted);
        if (missing.Count > 0)
        {
            return $"角色「{character!.CharacterName}」缺少该端点所需的权限：{string.Join(", ", missing)}。"
                   + "请到 设置 → 权限选择 勾选这些 scope，然后到「角色」页重新授权该角色。";
        }

        var guard = ErrorLimitGuard();
        if (guard is not null)
        {
            return guard;
        }

        var path = ReadObject(args, "path");
        var query = ReadObject(args, "query");
        var language = Config.NeedLocalization ? "zh" : "en";
        var built = BuildUrl(operation, BaseUrl, path, query, character?.CharacterID ?? 0, character?.CorporationID ?? 0, language);
        if (built.Error is not null)
        {
            return built.Error;
        }

        string? bodyJson = null;
        if (operation.Body is not null && args.ValueKind == JsonValueKind.Object && args.TryGetProperty("body", out var bodyElement)
            && bodyElement.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            // 写请求体里的 ID 字段同样做一次还原（模型常把富化后的 "名称 (id)" 原样带回来）
            bodyJson = EsiResponseEnricher.UnwrapIdsInJson(bodyElement.GetRawText());
        }
        else if (operation.Body is { Required: true })
        {
            return $"该操作需要请求体 body（{DescribeBodyShape(operation.Body)}）。请先用 esi_describe 查看字段，再在 body 里给出。";
        }

        var maxChars = Math.Clamp(AiAssistantSettingService.EsiMaxResultChars, 500, 12000);
        var pages = 1;
        var allPages = ToolArgs.ReadBool(args, "allPages", false) && operation.Pageable;
        if (allPages)
        {
            pages = Math.Clamp(AiAssistantSettingService.EsiMaxPages, 1, 10);
        }

        var notes = new List<string>(built.Notes);
        var output = new StringBuilder();
        var pageNumber = ReadPage(args);
        var totalPages = 1;
        var firstPage = true;
        var enrichedFields = 0;
        var enrichTruncated = false;

        for (var i = 0; i < pages; i++)
        {
            ct.ThrowIfCancellationRequested();
            var pageIndex = pageNumber + i;
            var url = WithPage(built.Url, operation, allPages ? pageIndex : pageNumber);

            HttpStatusCode status;
            bool success;
            string payload;
            var pageTotal = 1;
            try
            {
                using var request = new HttpRequestMessage(new HttpMethod(operation.Verb), url);
                request.Headers.Accept.ParseAdd("application/json");
                request.Headers.UserAgent.ParseAdd("TheGuideToTheNewEden-AI-Assistant/1.0");
                if (built.Language is not null)
                {
                    request.Headers.AcceptLanguage.ParseAdd(built.Language);
                }

                if (characterCtx is not null)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", characterCtx.Character.Token);
                }

                if (bodyJson is not null)
                {
                    request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
                }

                // 状态码/分页头/正文都在 using 里读完，出块后只用局部变量（Dispose 后不再碰响应对象）
                using var response = await Http
                    .SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
                    .ConfigureAwait(false);
                payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                ObserveErrorLimit(response);
                status = response.StatusCode;
                success = response.IsSuccessStatusCode;
                pageTotal = ReadTotalPages(response);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return $"{operation.Signature} 请求超时（超过 {RequestTimeoutSeconds} 秒）。";
            }
            catch (HttpRequestException ex)
            {
                return $"{operation.Signature} 请求失败：{ex.Message}";
            }

            if (firstPage)
            {
                totalPages = pageTotal;
                output.Append(operation.Signature).Append(" → ").Append((int)status);
                if (operation.Pageable)
                {
                    output.Append($"（第 {pageNumber} 页");
                    if (totalPages > 1)
                    {
                        output.Append($"/共 {totalPages} 页，可用 page 参数继续");
                        if (allPages)
                        {
                            output.Append($"，本次取到第 {Math.Min(pageNumber + pages - 1, totalPages)} 页");
                        }
                    }

                    output.Append('）');
                }

                output.AppendLine();
                firstPage = false;
            }
            else
            {
                output.AppendLine().Append("---- 第 ").Append(pageIndex).Append(" 页 ----");
            }

            if (!success)
            {
                return output.Append(DescribeFailure(status, payload, operation)).ToString();
            }

            var itemCount = EsiJsonShaper.CountItems(payload);
            if (itemCount > 0)
            {
                notes.Add($"本页 {itemCount} 条");
            }

            // ID → 名称：ESI 返回大量裸 ID，不富化的话"查任意 ESI"给出的答案没法读（阶段 119）
            if (AiAssistantSettingService.EsiEnrichNames)
            {
                var enriched = await EsiResponseEnricher.EnrichAsync(payload, ct).ConfigureAwait(false);
                payload = enriched.Json;
                enrichedFields += enriched.Resolved;
                enrichTruncated |= enriched.Truncated;
            }

            output.AppendLine(EsiJsonShaper.Shape(payload, maxChars - output.Length > 400 ? maxChars - output.Length : 400));

            if (!allPages || pageIndex >= totalPages || output.Length >= maxChars)
            {
                break;
            }
        }

        if (enrichedFields > 0)
        {
            notes.Add($"已把 {enrichedFields} 个 ID 补成「名称 (id)」");
        }

        if (enrichTruncated)
        {
            notes.Add($"ID 太多，只解析了前 {EsiResponseEnricher.MaxIdsPerResponse} 个（其余保持原值）");
        }

        if (notes.Count > 0)
        {
            output.AppendLine().Append("（").Append(string.Join("；", notes.Distinct())).Append('）');
        }

        return output.ToString().TrimEnd();
    }

    private static string DescribeBodyShape(EsiBody? body)
        => body is null ? "无" : body.Type == "array" ? $"array<{body.Item ?? "object"}>" : body.Type;

    private static int ReadPage(JsonElement args)
    {
        var query = ReadObject(args, "query");
        var page = ReadValue(query, "page");
        if (int.TryParse(page, out var value))
        {
            return Math.Max(1, value);
        }

        return Math.Max(1, ToolArgs.ReadInt(args, "page", 1));
    }

    private static string WithPage(string url, EsiOperation operation, int page)
    {
        if (!operation.Pageable || page <= 1)
        {
            return url;
        }

        if (url.Contains("page=", StringComparison.OrdinalIgnoreCase))
        {
            return System.Text.RegularExpressions.Regex.Replace(
                url, @"([?&])page=\d+", $"$1page={page}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return url + (url.Contains('?') ? "&" : "?") + "page=" + page;
    }

    private static int ReadTotalPages(HttpResponseMessage? response)
    {
        if (response is null)
        {
            return 1;
        }

        if (response.Headers.TryGetValues("X-Pages", out var values)
            && int.TryParse(values.FirstOrDefault(), out var pages))
        {
            return Math.Max(1, pages);
        }

        return 1;
    }

    private static void ObserveErrorLimit(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-ESI-Error-Limit-Remain", out var remainValues)
            || !int.TryParse(remainValues.FirstOrDefault(), out var remain))
        {
            return;
        }

        DateTimeOffset? reset = null;
        if (response.Headers.TryGetValues("X-ESI-Error-Limit-Reset", out var resetValues)
            && int.TryParse(resetValues.FirstOrDefault(), out var seconds))
        {
            reset = DateTimeOffset.UtcNow.AddSeconds(seconds);
        }

        lock (LimitLock)
        {
            _errorLimitRemain = remain;
            _errorLimitReset = reset;
        }
    }

    /// <summary>错误配额护栏：剩得太少就直接拒绝（否则会被 ESI 封一段时间）。</summary>
    private static string? ErrorLimitGuard()
    {
        var floor = AiAssistantSettingService.EsiErrorLimitFloor;
        if (floor <= 0)
        {
            return null;
        }

        lock (LimitLock)
        {
            if (_errorLimitRemain is not { } remain || remain > floor)
            {
                return null;
            }

            var reset = _errorLimitReset is { } time
                ? $"，约 {Math.Max(0, (int)(time - DateTimeOffset.UtcNow).TotalSeconds)} 秒后重置"
                : string.Empty;
            return $"ESI 错误配额只剩 {remain} 次（阈值 {floor}）{reset}，已暂停调用以免被服务端封禁。"
                   + "请稍后重试，或在 设置 → AI 助手 调整阈值。";
        }
    }

    private static string ReadValue(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return Scalar(value);
    }

    private static string Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
        _ => string.Empty,
    };

    private static JsonElement ReadObject(JsonElement args, string name)
        => args.ValueKind == JsonValueKind.Object
           && args.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static string Excerpt(string? body)
    {
        var text = (body ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        return text.Length <= 300 ? text : text[..299] + "…";
    }

    private static HttpClient CreateClient() => new()
    {
        Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds),
    };
}
