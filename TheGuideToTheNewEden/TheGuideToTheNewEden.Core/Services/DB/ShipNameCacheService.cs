using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class ShipNameCacheService
    {
        private static ShipNameCacheService _current;
        public static ShipNameCacheService Current
        {
            get
            {
                if(_current == null)
                {
                    _current = new ShipNameCacheService();
                }
                return _current;
            }
        }


        private Dictionary<string, InvTypeBase> _shipsDict;
        public void Init()
        {
            if (!_shipsDict.NotNullOrEmpty())
            {
                var shipGroupIds = DB.InvGroupService.QueryGroupIdOfCategory(new List<int> { 6 });
                _shipsDict = new Dictionary<string, InvTypeBase>();
                var items = DB.InvTypeService.QueryTypesOfGroupMainAndLocal(shipGroupIds);
                foreach(var item in items)
                {
                    if(_shipsDict.TryAdd(item.TypeName.ToLower(), item))
                    {
                        if (item.TypeName.EndsWith('级'))
                        {
                            _shipsDict.TryAdd(item.TypeName.Substring(0, item.TypeName.Length - 1).ToLower(), item);
                        }
                    }
                }
            }
        }

        public InvTypeBase Search(string name)
        {
            if (name.StartsWith('*'))
            {
                name = name.Substring(1);
            }
            if(_shipsDict.TryGetValue(name.ToLower(), out var type))
            {
                return type;
            }
            else
            {
                return null;
            }
        }
        public void Dispose()
        {
            _shipsDict.Clear();
            _shipsDict = null;
        }
    }
}
