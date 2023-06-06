using ESI.NET.Models.SSO;
using Newtonsoft.Json;
using Syncfusion.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Models.Universe;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class StructureService
    {
        private static readonly string FilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "Structures.json");
        public static ObservableCollection<Structure> Structures { get; set; }
        public static void Init()
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var list = JsonConvert.DeserializeObject<List<Structure>>(json);
                Structures = list.ToObservableCollection();
            }
            if(Structures == null)
            {
                Structures = new ObservableCollection<Structure>();
            }
        }

        public static void Save()
        {
            string json = JsonConvert.SerializeObject(Structures);
            string folder = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(FilePath, json);
        }

        public static Structure GetStructure(long id)
        {
            return Structures.FirstOrDefault(p => p.Id == id);
        }

        public static List<Structure> GetStructuresOfRegion(int regionId)
        {
            return Structures.Where(p => p.RegionId == regionId).ToList();
        }
    }
}
