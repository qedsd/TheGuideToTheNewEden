using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.Market
{
    public class MarketItem:ObservableObject
    {
        public int Id { get => IsType ? InvType.TypeID : -InvMarketGroup.MarketGroupID; }
        public string Name
        {
            get
            {
                if(InvMarketGroup != null)
                {
                    return InvMarketGroup.MarketGroupName;
                }
                else
                {
                    return InvType.TypeName;
                }
            }
        }
        public string Desc
        {
            get
            {
                if (InvMarketGroup != null)
                {
                    return InvMarketGroup.Description;
                }
                else
                {
                    return InvType.Description;
                }
            }
        }
        public bool IsType
        {
            get => InvType != null;
        }
        public bool IsGroup
        {
            get => InvMarketGroup != null;
        }
        public InvMarketGroup InvMarketGroup { get; set; }
        public MarketItem ParentGroup { get; set; }
        public InvType InvType { get; set; }
        public List<MarketItem> Children { get; set; }
    }
    public class SelectableMarketItem : MarketItem
    {
        private bool? _selected = false;
        public bool? Selected
        {
            get => _selected;
            set
            {
                if(SetProperty(ref _selected, value))
                {
                    ParentGroup?.UpdateStatusFromChild();
                    UpdateChildStatus();
                }
            }
        }
        public new List<SelectableMarketItem> Children { get; set; }
        public new SelectableMarketItem ParentGroup { get; set; }
        private bool _lock = false;
        public void UpdateStatusFromChild()
        {
            if (_lock) return;
            //若子节点有部分选中状态
            if(Children.Count(p => p.Selected == null) > 0)
            {
                Selected = null;
            }
            else
            {
                int selectedChilds = Children.Count(p => p.Selected == true);
                if (selectedChilds == 0)
                {
                    Selected = false;
                }
                else if (selectedChilds == Children.Count)
                {
                    Selected = true;
                }
                else
                {
                    Selected = null;
                }
            }
        }
        public void UpdateChildStatus()
        {
            if (Selected != null && Children != null)
            {
                _lock = true;
                foreach (var child in Children)
                {
                    child.Selected = Selected;
                }
                _lock = false;
            }
        }
    }
}
