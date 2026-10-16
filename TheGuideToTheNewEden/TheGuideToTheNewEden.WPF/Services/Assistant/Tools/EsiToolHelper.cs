using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.WPF.Services.Assistant.Esi;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>esi_* 四个工具共用：把模型给的 operationId 解析成操作，并做"只读/写"分工检查。</summary>
internal static class EsiToolHelper
{
    /// <summary>解析操作；错误非空时直接作为工具结果返回给模型。</summary>
    public static (EsiOperation? Operation, string? Error) Resolve(JsonElement args, bool requireWrite)
    {
        if (!EsiOperationIndex.IsReady)
        {
            return (null, $"ESI 操作索引不可用（{EsiOperationIndex.IndexPath} 缺失或损坏）。"
                          + "请运行 tools/esi/gen_esi_index.py 重新生成并重新构建程序。");
        }

        var key = ToolArgs.ReadString(args, "operation")?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return (null, "缺少参数 operation（operationId）。可以先用 esi_search 检索，例如 keyword=\"wallet\"。");
        }

        var operation = EsiOperationIndex.Find(key);
        if (operation is null)
        {
            return (null, $"没有找到操作「{key}」。请用 esi_search 检索后再用返回的 operationId 精确调用。");
        }

        if (requireWrite && !operation.Write)
        {
            return (null, $"{operation.Signature} 是只读操作，请用 esi_read（走 esi_write 会白让用户确认一次）。");
        }

        if (!requireWrite && operation.Write)
        {
            return (null, $"{operation.Signature} 是写操作（会先请用户确认），请用 esi_write 调用。");
        }

        return (operation, null);
    }
}
