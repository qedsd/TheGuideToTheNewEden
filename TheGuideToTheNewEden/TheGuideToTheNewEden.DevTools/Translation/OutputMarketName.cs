using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Market;

namespace TheGuideToTheNewEden.DevTools.Translation
{
    internal static class OutputMarketName
    {
        public static async Task Start(string outputPath)
        {
            TheGuideToTheNewEden.Core.Config.NeedLocalization = true;
            var zhTypes = (await Core.Services.DB.InvTypeService.QueryMarketTypesAsync()).ToDictionary(p=>p.TypeID);

            #region 构建市场树
            TheGuideToTheNewEden.Core.Config.NeedLocalization = false;
            var marketItemsDict = new Dictionary<int, MarketItem>();
            var marketTypes = new List<MarketItem>();
            var rootGroup = (await Core.Services.DB.InvMarketGroupService.QueryRootGroupAsync()).Select(p => new MarketItem() { InvMarketGroup = p }).ToList();
            var subGroup = (await Core.Services.DB.InvMarketGroupService.QuerySubGroupAsync()).Select(p => new MarketItem() { InvMarketGroup = p }).ToList();
            var allGroup = rootGroup.ToList();
            allGroup.AddRange(subGroup);
            var allGroupDic = allGroup.ToDictionary(p => p.InvMarketGroup.MarketGroupID);//所有分组的字典
            //挨个遍历子分组找到其父节点
            foreach (var item in subGroup)
            {
                if (allGroupDic.TryGetValue((int)item.InvMarketGroup.ParentGroupID, out var parentGroup))
                {
                    if (parentGroup.Children == null)
                    {
                        parentGroup.Children = new List<MarketItem>();
                    }
                    parentGroup.Children.Add(item);
                    item.ParentGroup = parentGroup;
                }
            }
            var typeParentGroups = allGroup.Where(p => p.Children == null).ToList();
            var typeParentGroupsDic = typeParentGroups.ToDictionary(p => p.InvMarketGroup.MarketGroupID);
            var allMarketTypes = await Core.Services.DB.InvTypeService.QueryMarketTypesAsync();
            foreach (var marketType in allMarketTypes)
            {
                if (typeParentGroupsDic.TryGetValue((int)marketType.MarketGroupID, out var group))
                {
                    if (group.Children == null)
                    {
                        group.Children = new List<MarketItem>();
                    }
                    var typeMarketItem = new MarketItem()
                    {
                        InvType = marketType,
                        ParentGroup = group,
                    };
                    group.Children.Add(typeMarketItem);
                    marketItemsDict.Add(typeMarketItem.InvType.TypeID, typeMarketItem);
                    marketTypes.Add(typeMarketItem);
                }
            }
            #endregion

            List<InvType> enTypes = new List<InvType>();
            void addType(MarketItem marketItem)
            {
                if (marketItem.IsType)
                {
                    enTypes.Add(marketItem.InvType);
                }
                else if(marketItem.Children != null)
                {
                    if(marketItem.RealId == 4726)
                    {

                    }
                    foreach (var item in marketItem.Children)
                    {
                        addType(item);
                    }
                }
            }

            int[] filterGroup = new int[] { 2,1396 , 1954 ,3628};
            foreach(var root in rootGroup)
            {
                if(filterGroup.Contains(root.RealId))
                {
                    continue;
                }
                addType(root);
            }

            StringBuilder stringBuilder = new StringBuilder();
            foreach (var type in enTypes)
            {
                stringBuilder.Append(type.TypeName);
                stringBuilder.Append(',');
                stringBuilder.Append(zhTypes[type.TypeID].TypeName);
                stringBuilder.AppendLine();
            }

            File.WriteAllText(outputPath, stringBuilder.ToString());
        }
    }
}
