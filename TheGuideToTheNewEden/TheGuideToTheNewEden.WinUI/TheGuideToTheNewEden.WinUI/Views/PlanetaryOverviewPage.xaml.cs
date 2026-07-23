using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.PlanetColony;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WinUI.Services;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class PlanetaryOverviewPage : Page
    {
        private readonly PlanetaryService _planetaryService = new PlanetaryService();

        public PlanetaryOverviewPage()
        {
            this.InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            EmptyPanel.Visibility = Visibility.Collapsed;
            try
            {
                var chars = CharacterService.CharacterOauths?.ToList() ?? new List<AuthorizedCharacterData>();
                bool hasPlanetsChar = chars.Any(c => c.Scopes != null && c.Scopes.Contains("esi-planets.manage_planets.v1"));

                bool loaded = false;
                if (chars.Count > 0 && hasPlanetsChar)
                {
                    loaded = await LoadFromESI(chars.Where(c => c.Scopes != null && c.Scopes.Contains("esi-planets.manage_planets.v1")).ToList());
                }
                if (!loaded)
                {
                    LoadFromLocalDB();
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Overview] " + ex.ToString()); }
            finally { LoadingOverlay.Visibility = Visibility.Collapsed; }
        }

        /// <summary>从 ESI 加载真实角色数据。返回是否有数据。</summary>
        private async Task<bool> LoadFromESI(List<AuthorizedCharacterData> chars)
        {
            sUpd.Text = DateTime.Now.ToShortTimeString();

            int totalCol = 0, totalAlert = 0;
            var resDict = new Dictionary<int, ExtractionRow>();
            var invDict = new Dictionary<int, InventoryRow>();
            var charRows = new ObservableCollection<CharacterRow>();
            var alertList = new ObservableCollection<AlertDisplayRow>();

            foreach (var c in chars)
            {
                try
                {
                    var planets = await _planetaryService.GetCharacterPlanetsAsync(c);
                    var charRow = new CharacterRow { CharacterName = c.CharacterName };
                    int charEcu = 0, charFac = 0;
                    var nearestExp = DateTime.MaxValue;

                    foreach (var p in planets)
                    {
                        totalCol++;
                        var detail = await _planetaryService.GetPlanetColonyDetailAsync(c, p.PlanetId);
                        int ecus = detail.Pins.Count(x => x.Extractor != null);
                        int facs = detail.Pins.Count(x => x.Category == PinCategory.BasicIndustryFacility || x.Category == PinCategory.AdvancedIndustryFacility);
                        charEcu += ecus; charFac += facs;

                        foreach (var pin in detail.Pins)
                        {
                            if (pin.Extractor != null)
                            {
                                var ex = pin.Extractor;
                                if (!resDict.ContainsKey(ex.ProductTypeId))
                                {
                                    var invt = InvTypeService.QueryType(ex.ProductTypeId);
                                    var tier = PlanetSchematicService.GetProductTier(ex.ProductTypeId) ?? "P0";
                                    resDict[ex.ProductTypeId] = new ExtractionRow { ResourceName = invt?.TypeName ?? "#" + ex.ProductTypeId, Tier = tier };
                                }
                                resDict[ex.ProductTypeId].HeadCount += ex.NumHeads;
                                resDict[ex.ProductTypeId].TotalQty += ex.QtyPerCycle;
                                if (ex.IsExpired) resDict[ex.ProductTypeId].ExpiredCount++;
                                else resDict[ex.ProductTypeId].ActiveCount++;
                                if (!ex.IsExpired && ex.ExpiryTime < nearestExp) nearestExp = ex.ExpiryTime;
                            }
                            if (pin.StorageQuantity > 0 && pin.ContentTypeId.HasValue)
                            {
                                int iid = pin.ContentTypeId.Value;
                                if (!invDict.ContainsKey(iid))
                                {
                                    var invt = InvTypeService.QueryType(iid);
                                    invDict[iid] = new InventoryRow { ItemName = invt?.TypeName ?? "#" + iid, Tier = PlanetSchematicService.GetProductTier(iid) ?? "?", Quantity = 0 };
                                }
                                invDict[iid].Quantity += pin.StorageQuantity;
                            }
                        }
                    }

                    charRow.ExtractorCount = charEcu;
                    charRow.FactoryCount = charFac;
                    charRow.PlanetRatio = planets.Count + "/6";
                    charRow.NearestExpiry = nearestExp < DateTime.MaxValue ? FormatTimeLeft(nearestExp) : "--";
                    charRow.StatusText = charEcu > 0 ? "运行中" : "待机";
                    charRows.Add(charRow);

                    var alerts = await _planetaryService.GenerateAlertsAsync(c);
                    totalAlert += alerts.Count;
                    foreach (var a in alerts)
                        alertList.Add(new AlertDisplayRow { CharacterName = a.CharacterName, PlanetName = a.PlanetName, Message = a.Message, SeverityName = a.Severity.ToString(), TypeName = a.Type == AlertType.ExtractorExpiring ? "到期预警" : "已到期" });
                }
                catch { }
            }

            if (totalCol == 0) return false;

            sCol.Text = totalCol.ToString();
            sEcu.Text = resDict.Sum(x => x.Value.ActiveCount).ToString();
            sFac.Text = charRows.Sum(x => x.FactoryCount).ToString();
            sAlert.Text = totalAlert.ToString();
            sChars.Text = chars.Count.ToString();

            CharGrid.ItemsSource = charRows;
            if (resDict.Count > 0) ExtractionGrid.ItemsSource = new ObservableCollection<ExtractionRow>(resDict.Values.OrderByDescending(x => x.HeadCount));
            if (invDict.Count > 0) InventoryGrid.ItemsSource = new ObservableCollection<InventoryRow>(invDict.Values.OrderByDescending(x => x.Quantity));
            if (alertList.Count > 0) AlertGrid.ItemsSource = alertList;

            EmptyPanel.Visibility = Visibility.Collapsed;
            return true;
        }

        /// <summary>从本地 SDE 数据库模拟 PI 概览数据（当 ESI 无数据时的回退）</summary>
        private void LoadFromLocalDB()
        {
            try
            {
                // 1) 获取 PI 产品等级
                var tiers = PlanetSchematicService.GetAllProductTiers();
                if (tiers.Count == 0)
                {
                    // 再试一次确保缓存初始化
                    var dummy = PlanetSchematicService.GetSchematic(74);
                    tiers = PlanetSchematicService.GetAllProductTiers();
                }
                if (tiers.Count == 0)
                {
                    EmptyPanel.Visibility = Visibility.Visible;
                    return;
                }

                // 2) 角色总览
                var charRows = new ObservableCollection<CharacterRow>();
                int totalEcu = 0, totalFac = 0;

                // 3) 资源采集：从 planetResources 汇总
                var resDict = new Dictionary<int, ExtractionRow>();
                try
                {
                    var allRes = PlanetResourcesService.GetAll();
                    if (allRes != null && allRes.Count > 0)
                    {
                        foreach (var g in allRes.GroupBy(x => x.TypeId))
                        {
                            var invt = InvTypeService.QueryType(g.Key, false);
                            var tier = PlanetSchematicService.GetProductTier(g.Key) ?? "P0";
                            resDict[g.Key] = new ExtractionRow
                            {
                                ResourceName = invt?.TypeName ?? "#" + g.Key,
                                Tier = tier,
                                HeadCount = g.Count(),
                                TotalQty = g.Sum(x => x.AmountPerCycle),
                                ActiveCount = g.Count()
                            };
                            totalEcu += g.Count();
                        }
                    }
                }
                catch (Exception ex) { }

                // 4) 库存
                var invDict = new Dictionary<int, InventoryRow>();
                var schematics = PlanetSchematicService.GetAll();

                // P0
                foreach (var kv in resDict)
                {
                    invDict[kv.Key] = new InventoryRow
                    {
                        ItemName = kv.Value.ResourceName,
                        Tier = kv.Value.Tier,
                        Quantity = kv.Value.TotalQty * 100
                    };
                }
                // P1-P4
                foreach (var s in schematics)
                {
                    if (s.Outputs == null) continue;
                    foreach (var o in s.Outputs)
                    {
                        if (!invDict.ContainsKey(o.TypeId))
                        {
                            var invt = InvTypeService.QueryType(o.TypeId, false);
                            var tier = PlanetSchematicService.GetProductTier(o.TypeId) ?? "?";
                            long simQty = 0;
                            if (s.Inputs != null && s.Inputs.Count > 0)
                            {
                                long minInput = long.MaxValue;
                                foreach (var inp in s.Inputs)
                                {
                                    if (resDict.TryGetValue(inp.TypeId, out var ext))
                                        minInput = Math.Min(minInput, ext.TotalQty / Math.Max(inp.Quantity, 1) * o.Quantity);
                                }
                                if (minInput < long.MaxValue) simQty = minInput;
                            }
                            if (simQty == 0)
                                simQty = tier switch { "P1" => 50000, "P2" => 8000, "P3" => 1200, "P4" => 200, _ => 0 };
                            invDict[o.TypeId] = new InventoryRow
                            {
                                ItemName = invt?.TypeName ?? "#" + o.TypeId,
                                Tier = tier,
                                Quantity = simQty
                            };
                        }
                    }
                }
                totalFac = schematics.Count(s => s.Inputs != null && s.Inputs.Count > 0);

                charRows.Add(new CharacterRow
                {
                    CharacterName = "本地模拟",
                    PlanetRatio = $"{resDict.Count}/6",
                    ExtractorCount = totalEcu,
                    FactoryCount = totalFac,
                    NearestExpiry = "--",
                    StatusText = "模拟"
                });

                sCol.Text = resDict.Count.ToString();
                sEcu.Text = totalEcu.ToString();
                sFac.Text = totalFac.ToString();
                sAlert.Text = "0";
                sChars.Text = "1";
                sUpd.Text = "本地模拟";
                EmptyPanel.Visibility = Visibility.Collapsed;

                CharGrid.ItemsSource = charRows;
                if (resDict.Count > 0)
                    ExtractionGrid.ItemsSource = new ObservableCollection<ExtractionRow>(resDict.Values.OrderByDescending(x => x.TotalQty));
                if (invDict.Count > 0)
                    InventoryGrid.ItemsSource = new ObservableCollection<InventoryRow>(invDict.Values.OrderByDescending(x => x.Quantity));
            }
            catch (Exception ex)
            {
                EmptyPanel.Visibility = Visibility.Visible;
            }
        }

        private static string FormatTimeLeft(DateTime expiry)
        {
            double h = (expiry - DateTime.UtcNow).TotalHours;
            if (h <= 0) return "到期";
            if (h < 1) return Math.Ceiling(h * 60) + "m";
            if (h < 24) return (int)h + "h" + Math.Ceiling((h % 1) * 60) + "m";
            return (int)(h / 24) + "d";
        }
    }

    public class ExtractionRow
    {
        public string ResourceName { get; set; }
        public string Tier { get; set; }
        public int ExtractorCount { get; set; }
        public int HeadCount { get; set; }
        public int TotalQty { get; set; }
        public int ActiveCount { get; set; }
        public int ExpiredCount { get; set; }
    }

    public class InventoryRow
    {
        public string ItemName { get; set; }
        public string Tier { get; set; }
        public long Quantity { get; set; }
    }

    public class CharacterRow
    {
        public string CharacterName { get; set; }
        public string PlanetRatio { get; set; }
        public int ExtractorCount { get; set; }
        public int FactoryCount { get; set; }
        public string NearestExpiry { get; set; }
        public string StatusText { get; set; }
    }

    public class AlertDisplayRow
    {
        public string CharacterName { get; set; }
        public string PlanetName { get; set; }
        public string TypeName { get; set; }
        public string Message { get; set; }
        public string SeverityName { get; set; }
    }
}
