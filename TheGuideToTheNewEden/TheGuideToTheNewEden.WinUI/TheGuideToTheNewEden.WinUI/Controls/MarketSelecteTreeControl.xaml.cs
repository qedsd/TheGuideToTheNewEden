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
using TheGuideToTheNewEden.Core.DBModels;
using ESI.NET.Models.SSO;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Services;
using System.Collections.ObjectModel;
using TheGuideToTheNewEden.Core.Extensions;
using CommunityToolkit.WinUI.UI.Controls.TextToolbarSymbols;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Models;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class MarketSelecteTreeControl : UserControl
    {
        private Dictionary<int, SelectableMarketItem> _marketItemsDict;
        private List<SelectableMarketItem> _marketItems;
        private List<SelectableMarketItem> _marketTypes;
        private List<InvType> _types;
        public MarketSelecteTreeControl()
        {
            this.InitializeComponent();
            Init();
        }

        private async void Init()
        {
            _marketItemsDict = new Dictionary<int, SelectableMarketItem>();
            _marketTypes = new List<SelectableMarketItem>();
            var rootGroup = (await Core.Services.DB.InvMarketGroupService.QueryRootGroupAsync()).Select(p => new SelectableMarketItem() { InvMarketGroup = p }).ToList();
            var subGroup = (await Core.Services.DB.InvMarketGroupService.QuerySubGroupAsync()).Select(p => new SelectableMarketItem() { InvMarketGroup = p }).ToList();
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
                        parentGroup.Children = new List<SelectableMarketItem>();
                    }
                    parentGroup.Children.Add(item);
                    item.ParentGroup = parentGroup;
                }
            }
            var typeParentGroups = allGroup.Where(p=>p.Children == null).ToList();
            var typeParentGroupsDic = typeParentGroups.ToDictionary(p => p.InvMarketGroup.MarketGroupID);
            var marketTypes = await Core.Services.DB.InvTypeService.QueryMarketTypesAsync();
            _types = marketTypes;
            foreach (var marketType in marketTypes)
            {
                if(typeParentGroupsDic.TryGetValue((int)marketType.MarketGroupID,out var group))
                {
                    if(group.Children == null)
                    {
                        group.Children = new List<SelectableMarketItem>();
                    }
                    var typeMarketItem = new SelectableMarketItem()
                    {
                        InvType = marketType,
                        ParentGroup = group,
                    };
                    group.Children.Add(typeMarketItem);
                    _marketItemsDict.Add(typeMarketItem.InvType.TypeID, typeMarketItem);
                    _marketTypes.Add(typeMarketItem);
                }
            }
            TreeView_Types.ItemsSource = rootGroup;
            _marketItems = rootGroup;
            
        }

        private void TreeView_Types_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            var type = args.InvokedItem as SelectableMarketItem;
            if(type != null)
            {
                if(type.Selected == null)
                {
                    type.Selected = true;
                }
                else
                {
                    type.Selected = !type.Selected;
                }
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = (sender as TextBox).Text;
            if (string.IsNullOrEmpty(text))
            {
                SerachList.ItemsSource = null;
                SerachList.Visibility = Visibility.Collapsed;
            }
            else
            {
                SerachList.ItemsSource = _marketTypes.Where(p => p.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
                SerachList.Visibility = Visibility.Visible;
            }
        }

        private void SerachList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as SelectableMarketItem;
            item.Selected = !item.Selected;
        }
    }
}
