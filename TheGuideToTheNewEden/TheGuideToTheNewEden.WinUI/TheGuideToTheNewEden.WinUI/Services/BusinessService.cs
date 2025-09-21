using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Market;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class BusinessService
    {
        #region 倒货过滤清单
        private Dictionary<int, InvType> _filterdTypes = new Dictionary<int, InvType>();

        public void AddToFilter(IEnumerable<InvType> types)
        {
            if (types.NotNullOrEmpty())
            {
                List<InvType> changed = new List<InvType>();
                foreach(var type in types)
                {
                    if(_filterdTypes.TryAdd(type.TypeID, type))
                    {
                        changed.Add(type);
                    }
                }
                FilterChanged?.Invoke(changed, true);
            }
        }

        public void RemoveFromFilter(IEnumerable<InvType> types)
        {
            if (types.NotNullOrEmpty())
            {
                List<InvType> changed = new List<InvType>();
                foreach (var type in types)
                {
                    if (_filterdTypes.Remove(type.TypeID))
                    {
                        changed.Add(type);
                    }
                }
                FilterChanged?.Invoke(changed, false);
            }
        }
        public List<InvType> GetFilterTypes()
        {
            return _filterdTypes.Values.ToList();
        }
        public delegate void FilterChangedDelegate(List<InvType> types, bool isAdd);
        public event FilterChangedDelegate FilterChanged;
        #endregion

        #region 物品数量变化通知
        public void NotifyTypeCountChanged(List<(InvType type, int count)> types)
        {
            if (types.NotNullOrEmpty())
            {
                TypeCountChanged?.Invoke(types);
            }
        }
        public delegate void TypeCountChangedDelegate(List<(InvType type, int count)> types);
        public event TypeCountChangedDelegate TypeCountChanged;
        #endregion
    }
}
