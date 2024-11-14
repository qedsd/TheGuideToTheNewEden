using SqlSugar;
using SqlSugar.DistributedSystem.Snowflake;
using System.Collections.Generic;
using System.Xml.Linq;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.SystemCheck
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TheGuideToTheNewEden.Core.Config.DBPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "main.db");
            TheGuideToTheNewEden.Core.Config.InitDb();
            Core.Config.ClientId = "8d0da2b105324ead932f60f32b1a55fb";
            Check();
        }
        static void Check()
        {
            try
            {
                var systems = MapSolarSystemService.QueryAll().Where(p=>!p.IsSpecial());
                var systemDict = systems.ToDictionary(p => p.SolarSystemID);
                var esi = ESIService.GetDefaultEsi();
                var names = systems.Select(p => p.SolarSystemName).ToList();
                for(int i=0; i<names.Count;)
                {
                    if(names.Contains("ME-PQ"))
                    {

                    }
                    int start = i;
                    int end = i+200;
                    i = end;
                    List<string> checks = new List<string>();
                    if(end < names.Count)
                    {
                        checks = names.Skip(start).Take(end - start).ToList();
                    }
                    else
                    {
                        checks = names.Skip(start).ToList();
                    }
                    var resp = esi.Universe.IDs(checks).Result;
                    if(resp.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        if(resp.Data.Systems.Any())
                        {
                            foreach (var system in resp.Data.Systems)
                            {
                                if (!systemDict.Remove(system.Id))
                                {

                                }
                            }
                        }
                        else
                        {

                        }
                    }
                    else
                    {
                        throw new Exception(resp.Message);
                    }
                }
                if(!systemDict.Any())
                {
                    Console.WriteLine("全部名称一致");
                }
                else
                {
                    Console.WriteLine($"{systemDict.Count}个名称不一致");
                    Dictionary<int, string> esiNames = new Dictionary<int, string>();
                    var ids = systemDict.Keys.ToList();
                    for (int i = 0; i < ids.Count;)
                    {
                        int start = i;
                        int end = i + 200;
                        i = end;
                        List<int> checks = new List<int>();
                        if (end < ids.Count)
                        {
                            checks = ids.Skip(start).Take(end - start).ToList();
                        }
                        else
                        {
                            checks = ids.Skip(start).ToList();
                        }
                        var resp2 = esi.Universe.Names(checks).Result;
                        if (resp2.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            foreach (var data in resp2.Data)
                            {
                                esiNames.Add(data.Id, data.Name);
                            }
                        }
                        else
                        {
                            throw new Exception(resp2.Message);
                        }
                    }
                    Console.WriteLine("ID 本地名称 ESI名称");
                    foreach (var system in systemDict)
                    {
                        if(esiNames.TryGetValue(system.Key, out string name))
                        {
                            Console.WriteLine($"{system.Key} {system.Value} {name}");
                        }
                        else
                        {
                            Console.WriteLine($"{system.Key} {system.Value}");
                        }
                    }
                }
            }
            catch(Exception ex)
            {

            }

        }
    }
}
