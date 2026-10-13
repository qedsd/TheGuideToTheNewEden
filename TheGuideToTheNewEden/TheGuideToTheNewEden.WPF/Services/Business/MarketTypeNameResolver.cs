using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>
/// 物品名 → 本地 SDE 类型 的解析器。原先内联在 <see cref="AppraisalService"/> 里，
/// 比价（<see cref="CompareMarketService"/>）需要同一套规则，故抽成公共类；估价页改为委托本类，**解析逻辑逐字不变**。
/// <para>
/// 粘贴文本可能是英文名（国际服）或中文名（国服/本地化客户端），按 4 级解析：
/// ① 主库精确（英文 SDE 原名）→ ② 本地化库精确（中文名）→ ③ 主库模糊（唯一命中才采用）
/// → ④ 本地化库模糊（中文模糊，同样只接受唯一命中或忽略大小写恰好相等），避免张冠李戴。
/// </para>
/// 纯本地库查询（不联网）；逐个名称内部 try/catch，单个名称异常不影响其余名称。
/// </summary>
public static class MarketTypeNameResolver
{
    /// <summary>
    /// 批量解析（名称按忽略大小写去重）。返回 <c>名称 → InvType</c>，查不到的为 <c>null</c>
    /// （调用方据此把该输入行归入"未识别"）。
    /// </summary>
    public static Dictionary<string, InvType?> Resolve(IEnumerable<string> names)
    {
        var result = new Dictionary<string, InvType?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name) || result.ContainsKey(name))
            {
                continue;
            }

            result[name] = ResolveOne(name);
        }

        return result;
    }

    /// <summary>解析单个名称；查不到返回 <c>null</c>。</summary>
    public static InvType? ResolveOne(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        InvType? type = null;
        try
        {
            // 1) 主库精确（SDE 原名，一般是英文）
            var types = Core.Services.DB.InvTypeService.QueryByName(name, isLike: false);
            if (types is { Count: > 0 })
            {
                type = types.FirstOrDefault(p => string.Equals(p.TypeName, name, StringComparison.OrdinalIgnoreCase)) ?? types[0];
            }
            // 2) 本地化库精确（中文客户端复制的中文名）
            else if (Core.Config.NeedLocalization)
            {
                var local = Core.Services.DB.LocalDbService.QueryInvTypes(name).FirstOrDefault();
                if (local is not null)
                {
                    type = Core.Services.DB.InvTypeService.QueryType(local.TypeID);
                }
            }

            // 3) 模糊兜底：主库唯一命中或忽略大小写恰好相等才采用
            if (type is null)
            {
                types = Core.Services.DB.InvTypeService.QueryByName(name, isLike: true);
                if (types is { Count: 1 })
                {
                    type = types[0];
                }
                else if (types is { Count: > 1 })
                {
                    type = types.FirstOrDefault(p => string.Equals(p.TypeName, name, StringComparison.OrdinalIgnoreCase));
                }
            }

            // 4) 本地化库模糊兜底：主库按英文名 Contains 查中文输入恒为空，中文模糊走本地化库
            if (type is null && Core.Config.NeedLocalization)
            {
                var locals = Core.Services.DB.LocalDbService.SearchInvType(name);
                if (locals is { Count: 1 })
                {
                    type = Core.Services.DB.InvTypeService.QueryType(locals[0].TypeID);
                }
                else if (locals is { Count: > 1 })
                {
                    var exactLocal = locals.FirstOrDefault(p => string.Equals(p.TypeName, name, StringComparison.OrdinalIgnoreCase));
                    if (exactLocal is not null)
                    {
                        type = Core.Services.DB.InvTypeService.QueryType(exactLocal.TypeID);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return type;
    }
}
