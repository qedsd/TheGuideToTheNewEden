using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core.Models.Market;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class MarketTreeControl : UserControl
    {
        public List<MarketItem> MarketItems { get; set; }
        public MarketTreeControl()
        {
            this.InitializeComponent();
            Init();
        }
        private async void Init()
        {
            var rootGroup = (await Core.Services.DB.InvMarketGroupService.QueryRootGroupAsync()).Select(p => new MarketItem() { InvMarketGroup = p }).ToList();
            var subGroup = (await Core.Services.DB.InvMarketGroupService.QuerySubGroupAsync()).Select(p => new MarketItem() { InvMarketGroup = p }).ToList();
            var allGroup = rootGroup.ToList();
            allGroup.AddRange(subGroup);
            var allGroupDic = allGroup.ToDictionary(p => p.InvMarketGroup.MarketGroupID);//所有分组的字典
            //挨个遍历子分组找到其父节点
            foreach(var item in subGroup)
            {
                if(allGroupDic.TryGetValue((int)item.InvMarketGroup.ParentGroupID, out var parentGroup))
                {
                    if(parentGroup.Children == null)
                    {
                        parentGroup.Children = new List<MarketItem>();
                    }
                    parentGroup.Children.Add(item);
                }
            }
            var typeParentGroups = allGroup.Where(p=>p.Children == null).ToList();
            var typeParentGroupsDic = typeParentGroups.ToDictionary(p => p.InvMarketGroup.MarketGroupID);
            var marketTypes = await Core.Services.DB.InvTypeService.QueryMarketTypesAsync();
            foreach(var marketType in marketTypes)
            {
                if(typeParentGroupsDic.TryGetValue((int)marketType.MarketGroupID,out var group))
                {
                    if(group.Children == null)
                    {
                        group.Children = new List<MarketItem>();
                    }
                    group.Children.Add(new MarketItem()
                    {
                        InvType = marketType,
                    });
                }
            }
        }
    }
}
