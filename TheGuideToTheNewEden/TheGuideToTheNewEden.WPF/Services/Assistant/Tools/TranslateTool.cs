using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>中英互译（本地词库优先、AI 源兜底；方向自动/英中互译）。</summary>
public sealed class TranslateTool : ITool
{
    public string Name => "translate";

    public string DisplayNameKey => "AssistantTool_translate";

    public string Description =>
        "中英互译。优先用内置离线的 EVE 专有名词词库（物品/星系/空间站名，免费、只认专有名词），" +
        "词库未命中或要整句翻译时用已配置的 AI 源。适合「Rifter 是什么」「把这段英文翻成中文」；" +
        "译文以工具返回为准。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["text"] = new JsonObject { ["type"] = "string", ["description"] = "要翻译的文本" },
            ["direction"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray { "auto", "zh_en", "en_zh" },
                ["description"] = "默认 auto 自动判定",
            },
        },
        ["required"] = new JsonArray { "text" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var text = ToolArgs.ReadString(args, "text")?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ToolResult("请输入要翻译的内容。");
        }

        var direction = ToolArgs.ReadString(args, "direction") ?? "auto";

        ITranslationProvider? provider = null;
        if (TranslationService.LocalDatabase.IsAvailable)
        {
            provider = TranslationService.LocalDatabase;
        }
        else if (TranslationService.Ai.IsAvailable)
        {
            provider = TranslationService.Ai;
        }

        if (provider is null)
        {
            return new ToolResult(
                "翻译不可用：本地化数据库未载入且 AI 翻译源未配置。" +
                "请先在「设置 → 一般」启用本地化数据库，或在「设置 → AI 助手」配置端点。");
        }

        var (from, to) = direction switch
        {
            "zh_en" => (TranslationLanguages.Chinese, TranslationLanguages.English),
            "en_zh" => (TranslationLanguages.English, TranslationLanguages.Chinese),
            _ => (TranslationLanguages.Auto, TranslationLanguages.Auto),
        };

        var outcome = await provider.TranslateAsync(new TranslationRequest(text, from, to), ct);
        if (!outcome.Success)
        {
            return new ToolResult(outcome.ErrorMessage ?? "翻译失败。");
        }

        var translation = outcome.Items.FirstOrDefault()?.Translation;
        if (string.IsNullOrWhiteSpace(translation))
        {
            return new ToolResult("没有匹配的译文（本地词库只认 EVE 专有名词；通用句子请配置 AI 翻译源）。");
        }

        var builder = new StringBuilder();
        builder.Append(translation);
        if (outcome.GlossaryHits is { Count: > 0 })
        {
            builder.AppendLine();
            builder.Append("术语：" + string.Join("、", outcome.GlossaryHits.Take(6)));
        }

        return new ToolResult(
            builder.ToString(),
            ParamSummary: ToolArgs.Truncate(text, 40));
    }
}
