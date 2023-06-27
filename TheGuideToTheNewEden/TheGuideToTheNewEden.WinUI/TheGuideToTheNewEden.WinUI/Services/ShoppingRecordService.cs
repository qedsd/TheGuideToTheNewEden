using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Market;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class ShoppingRecordService
    {
        private static ShoppingRecordService current;
        public static ShoppingRecordService Current
        {
            get
            {
                if(current == null)
                {
                    current = new ShoppingRecordService();
                }
                return current;
            }
        }
        private static readonly string Folder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "ShoppingRecords");
        public ObservableCollection<string> Files = new ObservableCollection<string>();
        public ShoppingRecordService()
        {
            if(System.IO.Directory.Exists(Folder))
            {
                var files = System.IO.Directory.GetFiles(Folder);
                foreach(var file in files)
                {
                    Files.Add(file);
                }
            }
            else
            {
                Directory.CreateDirectory(Folder);
            }
        }
        public string Add(IEnumerable<ScalperShoppingItem> items)
        {
            string date = DateTime.Now.ToString("yyyy.MM.dd");
            int i = 1;
            string path;
            while (true)
            {
                var name = date + "_" + i++ + ".json";
                path = Path.Combine(Folder, name);
                if (!File.Exists(path))
                {
                    break;
                }
            }
            var json = JsonConvert.SerializeObject(items);
            File.WriteAllText(path, json);
            Files.Add(path);
            return path;
        }

        public void Remove(string file)
        {
            if(File.Exists(file))
            {
                File.Delete(file);
            }
            Files.Remove(file);
        }

        public List<ScalperShoppingItem> Load(string file)
        {
            if(File.Exists(file))
            {
                return JsonConvert.DeserializeObject<List<ScalperShoppingItem>>(File.ReadAllText(file));
            }
            else
            {
                return null;
            }
        }
    }
}
