using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class MarketStarService
    {
        private static MarketStarService current;
        public static MarketStarService Current
        {
            get
            {
                if (current == null)
                {
                    current = new MarketStarService();
                }
                return current;
            }
        }

        public MarketStarService()
        {
            InitStaredTypes();
        }

        private HashSet<int> StaredTypeIds;
        private string StaredFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "StaredMarketInvType.json");
        private void InitStaredTypes()
        {
            if (System.IO.File.Exists(StaredFile))
            {
                string json = System.IO.File.ReadAllText(StaredFile);
                if (!string.IsNullOrEmpty(json))
                {
                    var list = JsonConvert.DeserializeObject<List<int>>(json);
                    if (list != null)
                    {
                        StaredTypeIds = list.ToHashSet2();
                        return;
                    }
                }
            };
            StaredTypeIds = new HashSet<int>();
        }
        private void Save()
        {
            var folder = System.IO.Path.GetDirectoryName(StaredFile);
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }
            var json = JsonConvert.SerializeObject(StaredTypeIds.ToList());
            System.IO.File.WriteAllText(StaredFile, json);
        }

        public List<int> GetIds()
        {
            return StaredTypeIds.ToList();
        }
        public bool Remove(int id)
        {
            if (StaredTypeIds.Remove(id))
            {
                Save();
                OnStaredTypeIdsChanged?.Invoke(id, false);
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool Add(int id)
        {
            if(StaredTypeIds.Add(id))
            {
                Save();
                OnStaredTypeIdsChanged?.Invoke(id, true);
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool IsStared(int id)
        {
            return StaredTypeIds.Contains(id);
        }

        public delegate void StaredTypeIdsChangedEventHandler(int id, bool isAdd);
        public event StaredTypeIdsChangedEventHandler OnStaredTypeIdsChanged;
    }
}
