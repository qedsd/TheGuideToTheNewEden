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

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class MarketTreeControl : UserControl
    {
        public List<MarketItem> MarketItems { get; set; }
        private Dictionary<string, InvType> _typesDic;
        private List<InvType> _types;
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
            _types = marketTypes;
            foreach (var marketType in marketTypes)
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
            //_typesDic = marketTypes.ToDictionary(p => p.TypeName);
            TreeView_Types.ItemsSource = rootGroup;
        }
        private void TreeView_Types_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            SelectedInvType = args.InvokedItem as InvType;
        }
        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            SelectedInvType = args.SelectedItem as InvType;
            Search_AutoSuggestBox.Text = SelectedInvType.TypeName;
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if(SelectedInvType?.TypeName == sender.Text)
            {
                return;
            }
            if(string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = null;
            }
            else
            {
                sender.ItemsSource = _types.Where(p => p.TypeName.Contains(sender.Text)).ToList();
            }
        }

        public static readonly DependencyProperty SelectedInvTypeProperty
            = DependencyProperty.Register(
                nameof(SelectedInvType),
                typeof(InvType),
                typeof(MarketTreeControl),
                new PropertyMetadata(null, new PropertyChangedCallback(SelectedInvTypePropertyChanged)));

        public InvType SelectedInvType
        {
            get => (InvType)GetValue(SelectedInvTypeProperty);
            set => SetValue(SelectedInvTypeProperty, value);
        }
        private static void SelectedInvTypePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as MarketTreeControl).OnSelectedInvTypeChanged?.Invoke(e.NewValue as  InvType);
        }

        public delegate void SelectedInvTypeChangedEventHandel(InvType selectedInvType);
        private SelectedInvTypeChangedEventHandel OnSelectedInvTypeChanged;
    }
}
