﻿using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Helpers;

namespace TheGuideToTheNewEden.WhomholeCrawler
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            TheGuideToTheNewEden.Core.Config.DBPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "main.db");
            TheGuideToTheNewEden.Core.Config.WormholeDBPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "wormhole.db");
            TheGuideToTheNewEden.Core.Config.InitDb();
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await WebView2.EnsureCoreWebView2Async();
            //GetWormholes();
            //GetCaves();
            //CreateWormholePortalDB();
            CreateWormholeDB();
        }
        
        private void NavigateTo(string uri)
        {
            WebView2.Source = new Uri(uri);
        }
        
        /// <summary> 
        /// Unicode字符串转为正常字符串 
        /// </summary> 
        /// <param name="srcText"></param> 
        /// <returns></returns> 
        public static string UnicodeToString(string srcText)
        {
            return Regex.Unescape(srcText);
        }

        #region 洞
        private List<WormholeModel> wormholeModels = new List<WormholeModel>();
        private async void GetWormholes()
        {
            var solarSystems = await Core.Services.DB.MapSolarSystemService.QueryWormholesAsync();
            foreach(var solarSystem in solarSystems)
            {
                await GetWormholeDetail(solarSystem.SolarSystemName);
            }
            string json = JsonConvert.SerializeObject(wormholeModels);
            System.IO.File.WriteAllText("wormholes.json", json);
        }
        private async void WebView2_NavigationCompleted_WormholeDetail(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                WormholeModel wormholeModel = new WormholeModel();
                var html = await WebView2.CoreWebView2.ExecuteScriptAsync("document.body.innerText");
                var baseInfo = html.Split("\\n")[1];
                wormholeModel.Name = baseInfo.Substring(0, 7);
                wormholeModel.Class = int.Parse(baseInfo.Split(' ')[1]);
                if (baseInfo.Contains(')'))
                {
                    wormholeModel.Phenomena = baseInfo.Substring(baseInfo.IndexOf(')') + 1).Trim();
                }
                else
                {
                    string classTempStr = $"Class {wormholeModel.Class}";
                    wormholeModel.Phenomena = baseInfo.Substring(baseInfo.IndexOf(classTempStr) + classTempStr.Length).Trim();
                    wormholeModel.Phenomena = wormholeModel.Phenomena == "System" ? null : wormholeModel.Phenomena;
                }
                var caveInfo = html.Split("\\n")[4];
                if(caveInfo.Contains("statics + possible wandering"))
                {
                    //var caveTos = html.Split("\\n")[4].Split("statics + possible wandering");
                    //if (caveTos.Length > 0)
                    //{
                    //    var caves1 = caveTos[0].Split('>', ',');
                    //    var statics = SplitCave(caves1);
                    //    StringBuilder stringBuilder = new StringBuilder();
                    //    foreach (var item in statics)
                    //    {
                    //        stringBuilder.Append(item);
                    //        stringBuilder.Append(',');
                    //    }
                    //    stringBuilder.Remove(0, stringBuilder.Length - 1);
                    //    wormholeModel.Statics = stringBuilder.ToString();
                    //    if (caveTos.Length > 1)
                    //    {
                    //        var caves2 = caveTos[1].Split('>', ',');
                    //        var wanderings = SplitCave(caves2);
                    //        stringBuilder.Clear();
                    //        foreach (var item in wanderings)
                    //        {
                    //            stringBuilder.Append(item);
                    //            stringBuilder.Append(',');
                    //        }
                    //        stringBuilder.Remove(0, stringBuilder.Length - 1);
                    //        wormholeModel.Wanderings = stringBuilder.ToString();
                    //    }
                    //}
                    var caveTos = caveInfo.Split("statics + possible wandering");
                    wormholeModel.Statics = GetCaveTo(caveTos[0]);
                    wormholeModel.Wanderings = GetCaveTo(caveTos[1]);
                }
                else if (caveInfo.Contains("static + possible wandering"))
                {
                    var caveTos = caveInfo.Split("static + possible wandering");
                    wormholeModel.Statics = GetCaveTo(caveTos[0]);
                    wormholeModel.Wanderings = GetCaveTo(caveTos[1]);
                }
                else if (caveInfo.Contains("static"))
                {
                    wormholeModel.Statics = GetCaveTo(caveInfo.Replace("static", string.Empty));
                }
                else if(caveInfo.Contains("statics"))
                {
                    wormholeModel.Statics = GetCaveTo(caveInfo.Replace("statics", string.Empty));
                }
                else if(caveInfo.Contains("possible wandering"))
                {
                    wormholeModel.Wanderings = GetCaveTo(caveInfo.Replace("possible wandering", string.Empty));
                }
                wormholeModels.Add(wormholeModel);
            }
            catch(Exception ex)
            {

            }
        }
        private string GetCaveTo(string str)
        {
            var caves1 = str.Split('>', ',');
            var statics = SplitCave(caves1);
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var item in statics)
            {
                stringBuilder.Append(item);
                stringBuilder.Append(',');
            }
            stringBuilder.Remove(stringBuilder.Length - 1,1);
            return stringBuilder.ToString();
        }
        private async Task GetWormholeDetail(string name)
        {
            WebView2.NavigationCompleted -= WebView2_NavigationCompleted_WormholeDetail;
            WebView2.NavigationCompleted -= WebView2_NavigationCompleted_Caves;
            WebView2.NavigationCompleted += WebView2_NavigationCompleted_WormholeDetail;
            int count = wormholeModels.Count;
            NavigateTo($"http://anoik.is/systems/{name}");
            while (wormholeModels.Count == count)
            {
                await Task.Delay(1000);
            }
        }
        #endregion

        #region 洞口
        private async void GetCaves()
        {
            string[] names = new string[]
            {
                "H121","C125","O883","M609","L614","S804","N110","J244","Z060","F353",
                "Z647","D382","O477","Y683","N062","R474","B274","A239","E545","F135",
                "V301","I182","N968","T405","N770","A982","D845","U210","K346","F135",
                "P060","N766","C247","X877","H900","U574","S047","N290","K329",
                "Y790","D364","M267","E175","H296","V753","D792","C140","Z142",
                "Q317","G024","L477","Z457","V911","W237","B520","D792","C140","C391","C248","Z142",
                "Z971","R943","X702","O128","M555","B041","A641","R051","V283","T458",
                "Z971","R943","X702","O128","N432","U319","B449","N944","S199","M164",
                "Z971","R943","X702","O128","N432","U319","B449","N944","S199","L031",
                "Q063","V898","E587",
                "E004","L005","Z006","M001","C008","G008","Q003","A009","S877","B735","V928","C414","R259"
            };
            foreach (var name in names)
            {
                await GetCaveDetail(name);
            }
            string json = JsonConvert.SerializeObject(caveModels);
            System.IO.File.WriteAllText("caves.json", json);
        }
        private List<CaveModel> caveModels = new List<CaveModel>();
        private List<string> SplitCave(string[] strings)
        {
            if (strings != null && strings.Length != 0 && strings.Length % 2 == 0)
            {
                List<string> list = new List<string>();
                for (int i = 0; i < strings.Length; i += 2)
                {
                    list.Add(strings[i].Trim());
                }
                return list;
            }
            else
            {
                return null;
            }
        }
        private async Task GetCaveDetail(string name)
        {
            WebView2.NavigationCompleted -= WebView2_NavigationCompleted_WormholeDetail;
            WebView2.NavigationCompleted -= WebView2_NavigationCompleted_Caves;
            WebView2.NavigationCompleted += WebView2_NavigationCompleted_Caves;
            int count = caveModels.Count;
            NavigateTo($"http://anoik.is/wormholes/{name}");
            while(caveModels.Count == count)
            {
                await Task.Delay(1000);
            }
        }
        private async void WebView2_NavigationCompleted_Caves(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                var html = await WebView2.CoreWebView2.ExecuteScriptAsync("document.body.innerText");
                var s = html.Split("\\n");

                var destination = s[2].Split("\\t")[2];
                var appearsIn = s[3].Split("\\t")[2];
                appearsIn = appearsIn == "(unknown)" ? null : appearsIn;
                var lifetime = float.Parse(s[4].Split("\\t")[2].Replace(" hours", string.Empty));
                var maxMassPerJump = long.Parse(s[5].Split("\\t")[2].Replace(" kg", string.Empty).Replace(",", string.Empty));
                var maxMassPerJumpNote = UnicodeToString(s[5].Split("\\t")[3]);
                maxMassPerJumpNote = maxMassPerJumpNote.Trim(')', '(');
                var totalJumpMass = long.Parse(s[6].Split("\\t")[2].Replace(" kg", string.Empty).Replace(",", string.Empty));
                var totalJumpMassNote = UnicodeToString(s[6].Split("\\t")[3]);
                var respawn = s[7].Split("\\t")[2];
                respawn = respawn == "(unknown)" ? null : respawn;
                var massRegen = long.Parse(s[8].Split("\\t")[2].Replace(" kg", string.Empty).Replace(",", string.Empty));
                caveModels.Add(new CaveModel()
                {
                    Name = s[1].Trim(),
                    Destination = destination,
                    AppearsIn = appearsIn,
                    Lifetime = lifetime,
                    MaxMassPerJump = maxMassPerJump,
                    MaxMassPerJumpNote = maxMassPerJumpNote,
                    TotalJumpMass = totalJumpMass,
                    TotalJumpMassNote = totalJumpMassNote,
                    Respawn = respawn,
                    MassRegen = massRegen,
                });
            }
            catch (Exception ex)
            {

            }
        }
        #endregion

        #region 生成洞口数据库
        private List<WormholePortal> GetWormholePortals()
        {
            var text = System.IO.File.ReadAllText("caves.json");
            var items = JsonConvert.DeserializeObject<List<CaveModel>>(text);
            int id = 0;
            List<WormholePortal> wormholePortals = new List<WormholePortal>(items.Count);
            foreach (var item in items)
            {
                wormholePortals.Add(new WormholePortal()
                {
                    Id = id++,
                    Name = item.Name,
                    Lifetime = item.Lifetime,
                    MaxMassPerJump = item.MaxMassPerJump,
                    MaxMassPerJumpNote = item.MaxMassPerJumpNote,
                    TotalJumpMass = item.TotalJumpMass,
                    TotalJumpMassNote = item.TotalJumpMassNote,
                    Respawn = item.Respawn,
                    MassRegen = item.MassRegen,
                    Destination = GetClasses(item.Destination),
                    AppearsIn = GetClasses(item.AppearsIn),
                });
            }
            return wormholePortals;
        }
        private async void CreateWormholePortalDB()
        {
            await Core.Services.DB.WormholeService.InsertAsync(GetWormholePortals());
        }
        private string GetClasses(string str)
        {
            if(string.IsNullOrEmpty(str))
            {
                return null;
            }
            StringBuilder stringBuilder = new StringBuilder();
            foreach(var item in str.Split(','))
            {
                stringBuilder.Append(GetClassId(item.Trim()));
                stringBuilder.Append(",");
            }
            stringBuilder.Remove(stringBuilder.Length - 1, 1);
            return stringBuilder.ToString();
        }
        private int GetClassId(string className)
        {
            switch(className)
            {
                case "Class 1": return 1;
                case "Class 2": return 2;
                case "Class 3": return 3;
                case "Class 4": return 4;
                case "Class 5": return 5;
                case "Class 6": return 6;
                case "Class 12 (Thera)": return 12;
                case "Class 13 (Shattered Frigate)": return 13;
                case "Class 14 (Sentinel Drifter)": return 14;
                case "Class 15 (Barbican Drifter)": return 15;
                case "Class 16 (Vidette Drifter)": return 16;
                case "Class 17 (Conflux Drifter)": return 17;
                case "Class 18 (Redoubt Drifter)": return 18;
                case "Highsec": return 100;
                case "Lowsec": return 101;
                case "Nullsec": return 102;
                default:throw new Exception();
            }
        }
        #endregion

        #region 生成虫洞
        private async void CreateWormholeDB()
        {
            var text = System.IO.File.ReadAllText("wormholes.json");
            var items = JsonConvert.DeserializeObject<List<WormholeModel>>(text);
            var phenomenas = items.Where(p=>p.Phenomena != null).Select(p => p.Phenomena).Distinct().ToList();
            var portals = GetWormholePortals();
            List<Wormhole> wormholes = new List<Wormhole>();
            foreach(var item in items)
            {
                wormholes.Add(new Wormhole()
                {
                    Id = Core.Services.DB.MapSolarSystemService.Query(item.Name).SolarSystemID,
                    Name = item.Name,
                    Class = item.Class,
                    Phenomena = string.IsNullOrEmpty(item.Phenomena) ? -1 : phenomenas.IndexOf(item.Phenomena),
                    Statics = GetPortalIds(item.Statics),
                    Wanderings = GetPortalIds(item.Wanderings),
                });
            }
            string GetPortalIds(string str)
            {
                StringBuilder stringBuilder = new StringBuilder();
                if (string.IsNullOrEmpty(str))
                {
                    return null;
                }
                foreach(var item in str.Split(','))
                {
                    var p = portals.FirstOrDefault(p => p.Name == item);
                    if(p != null)
                    {
                        stringBuilder.Append(p.Id);
                        stringBuilder.Append(',');
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
                return stringBuilder.ToString();
            }

            await Core.Services.DB.WormholeService.InsertAsync(wormholes);
        }
        #endregion
    }
}
