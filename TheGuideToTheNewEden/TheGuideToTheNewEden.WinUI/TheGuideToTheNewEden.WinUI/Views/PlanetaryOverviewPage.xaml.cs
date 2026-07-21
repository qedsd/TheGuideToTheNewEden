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
            try
            {
                var chars = CharacterService.CharacterOauths?.ToList() ?? new List<AuthorizedCharacterData>();
                if (chars.Count == 0) { EmptyPanel.Visibility = Visibility.Visible; return; }

                sChars.Text = chars.Count.ToString();
                sUpd.Text = DateTime.Now.ToShortTimeString();

                int totalCol = 0, totalAlert = 0;
                var resDict = new Dictionary<int, ExtractionRow>();
                var invDict = new Dictionary<int, InventoryRow>();
                var charRows = new ObservableCollection<CharacterRow>();
                var alertList = new ObservableCollection<AlertDisplayRow>();

                foreach (var c in chars)
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
                                    resDict[ex.ProductTypeId] = new ExtractionRow { ResourceName = invt?.TypeName ?? "#" + ex.ProductTypeId, Tier = "P0" };
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
                                    invDict[iid] = new InventoryRow { ItemName = invt?.TypeName ?? "#" + iid, Tier = GetTier(iid), Quantity = 0 };
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

                sCol.Text = totalCol.ToString();
                sEcu.Text = resDict.Sum(x => x.Value.ActiveCount).ToString();
                sFac.Text = charRows.Sum(x => x.FactoryCount).ToString();
                sAlert.Text = totalAlert.ToString();

                CharGrid.ItemsSource = charRows;
                if (resDict.Count > 0) ExtractionGrid.ItemsSource = new ObservableCollection<ExtractionRow>(resDict.Values.OrderByDescending(x => x.HeadCount));
                if (invDict.Count > 0) InventoryGrid.ItemsSource = new ObservableCollection<InventoryRow>(invDict.Values.OrderByDescending(x => x.Quantity));
                if (alertList.Count > 0) AlertGrid.ItemsSource = alertList;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Overview] " + ex.Message); }
            finally { LoadingOverlay.Visibility = Visibility.Collapsed; }
        }

        private static string FormatTimeLeft(DateTime expiry)
        {
            double h = (expiry - DateTime.UtcNow).TotalHours;
            if (h <= 0) return "到期";
            if (h < 1) return Math.Ceiling(h * 60) + "m";
            if (h < 24) return (int)h + "h" + Math.Ceiling((h % 1) * 60) + "m";
            return (int)(h / 24) + "d";
        }

        private static string GetTier(int id)
        {
            if (id >= 2267 && id <= 2300) return "P0";
            if (id >= 3691 && id <= 3719) return "P1";
            if (id >= 3721 && id <= 3767) return "P2";
            if (id >= 3770 && id <= 3820) return "P3";
            if (id >= 3825 && id <= 3830) return "P4";
            return "?";
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
