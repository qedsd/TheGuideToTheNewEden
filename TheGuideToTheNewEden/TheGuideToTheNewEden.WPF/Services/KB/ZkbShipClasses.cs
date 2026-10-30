using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Models.KB;

namespace TheGuideToTheNewEden.WPF.Services.KB;

/// <summary>用本地 SDE 构建「舰船 → 分组 → MetaGroup → 具体舰船」的勾选树。</summary>
public static class ZkbShipTreeBuilder
{
    public static List<ShipFilterNode> Build()
    {
        var groups = InvGroupService.QueryShipGroups();
        var metaById = InvMetaGroupService.QueryAll().ToDictionary(p => p.MetaGroupID);
        var allTypes = InvTypeService.QueryShipTypeClasses();

        var root = new ShipFilterNode
        {
            Name = FindString("ZKBHomePage_Setting_Filter_ShipCategory"),
            IsLeaf = false,
        };

        foreach (var group in groups)
        {
            var groupTypes = allTypes
                .Where(p => p.GroupID == group.GroupID)
                .ToList();
            if (groupTypes.Count == 0)
            {
                continue;
            }

            var groupNode = new ShipFilterNode
            {
                Name = group.GroupName,
                Parent = root,
            };

            foreach (var metaGrouping in groupTypes
                .GroupBy(p => p.MetaGroupID)
                .OrderBy(p => p.Key == 0 ? int.MaxValue : p.Key))
            {
                var metaNode = new ShipFilterNode
                {
                    Name = metaGrouping.Key == 0
                        ? FindString("ZKBHomePage_FilterMeta_Unclassified")
                        : MetaLabel(metaGrouping.Key, metaById),
                    Parent = groupNode,
                };

                foreach (var shipType in metaGrouping.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    metaNode.Children.Add(new ShipFilterNode
                    {
                        Name = shipType.Name,
                        TypeId = shipType.TypeID,
                        IsLeaf = true,
                        Parent = metaNode,
                    });
                }

                groupNode.Children.Add(metaNode);
            }

            root.Children.Add(groupNode);
        }

        return [root];
    }

    private static string MetaLabel(int metaGroupId, IReadOnlyDictionary<int, InvMetaGroup> metaById)
    {
        var label = FindString($"ZKBHomePage_FilterMeta_{metaGroupId}");
        if (!string.Equals(label, $"ZKBHomePage_FilterMeta_{metaGroupId}", StringComparison.Ordinal))
        {
            return label;
        }

        return metaById.TryGetValue(metaGroupId, out var meta) && !string.IsNullOrWhiteSpace(meta.Name)
            ? meta.Name
            : metaGroupId.ToString();
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
