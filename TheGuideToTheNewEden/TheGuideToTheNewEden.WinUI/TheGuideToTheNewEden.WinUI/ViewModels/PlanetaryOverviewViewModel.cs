using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models.PlanetColony;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WinUI.Services;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class PlanetaryOverviewViewModel : BaseViewModel
    {
        private readonly PlanetaryService _planetaryService = new PlanetaryService();

        #region 统计面板

        private int _colonyCount;
        public int ColonyCount { get => _colonyCount; set => SetProperty(ref _colonyCount, value); }

        private int _extractorCount;
        public int ExtractorCount { get => _extractorCount; set => SetProperty(ref _extractorCount, value); }

        private int _factoryCount;
        public int FactoryCount { get => _factoryCount; set => SetProperty(ref _factoryCount, value); }

        private int _alertCount;
        public int AlertCount { get => _alertCount; set => SetProperty(ref _alertCount, value); }

        private int _characterCount;
        public int CharacterCount { get => _characterCount; set => SetProperty(ref _characterCount, value); }

        private string _updateTime = "--";
        public string UpdateTime { get => _updateTime; set => SetProperty(ref _updateTime, value); }

        #endregion

        #region 集合

        private ObservableCollection<CharacterRow> _characterRows = new ObservableCollection<CharacterRow>();
        public ObservableCollection<CharacterRow> CharacterRows { get => _characterRows; set => SetProperty(ref _characterRows, value); }

        private ObservableCollection<ExtractionRow> _extractionRows = new ObservableCollection<ExtractionRow>();
        public ObservableCollection<ExtractionRow> ExtractionRows { get => _extractionRows; set => SetProperty(ref _extractionRows, value); }

        private ObservableCollection<InventoryRow> _inventoryRows = new ObservableCollection<InventoryRow>();
        public ObservableCollection<InventoryRow> InventoryRows { get => _inventoryRows; set => SetProperty(ref _inventoryRows, value); }

        private ObservableCollection<AlertDisplayRow> _alertRows = new ObservableCollection<AlertDisplayRow>();
        public ObservableCollection<AlertDisplayRow> AlertRows { get => _alertRows; set => SetProperty(ref _alertRows, value); }

        private ObservableCollection<ProductionRow> _productionRows = new ObservableCollection<ProductionRow>();
        public ObservableCollection<ProductionRow> ProductionRows { get => _productionRows; set => SetProperty(ref _productionRows, value); }

        private ProductionRow _selectedProductionRow;
        public ProductionRow SelectedProductionRow
        {
            get => _selectedProductionRow;
            set
            {
                if (SetProperty(ref _selectedProductionRow, value))
                {
                    OnPropertyChanged(nameof(HasSelectedProduction));
                    CurrentDetails = value?.Details != null
                        ? new ObservableCollection<ProductionDetail>(value.Details)
                        : new ObservableCollection<ProductionDetail>();
                }
            }
        }

        public bool HasSelectedProduction => SelectedProductionRow != null;

        private ObservableCollection<ProductionDetail> _currentDetails = new ObservableCollection<ProductionDetail>();
        public ObservableCollection<ProductionDetail> CurrentDetails { get => _currentDetails; set => SetProperty(ref _currentDetails, value); }

        #endregion

        #region 状态

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                    OnPropertyChanged(nameof(LoadingVisibility));
            }
        }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set
            {
                if (SetProperty(ref _isEmpty, value))
                    OnPropertyChanged(nameof(EmptyVisibility));
            }
        }

        public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
        public Visibility EmptyVisibility => IsEmpty ? Visibility.Visible : Visibility.Collapsed;

        #endregion

        public ICommand LoadDataCommand => new RelayCommand(async () => await LoadDataAsync());

        public async Task LoadDataAsync()
        {
            IsLoading = true;
            IsEmpty = false;
            try
            {
                var chars = CharacterService.CharacterOauths?.ToList() ?? new List<AuthorizedCharacterData>();
                bool hasPlanetsChar = chars.Any(c => c.Scopes != null && c.Scopes.Contains("esi-planets.manage_planets.v1"));

                bool loaded = false;
                if (chars.Count > 0 && hasPlanetsChar)
                {
                    loaded = await LoadFromESI(chars.Where(c => c.Scopes != null && c.Scopes.Contains("esi-planets.manage_planets.v1")).ToList());
                }
            }
            catch (Exception ex) { Core.Log.Error("[Overview] " + ex.ToString()); }
            finally { IsLoading = false; }
        }

        /// <summary>从 ESI 加载真实角色数据。返回是否有数据。</summary>
        private async Task<bool> LoadFromESI(List<AuthorizedCharacterData> chars)
        {
            UpdateTime = DateTime.Now.ToShortTimeString();

            int totalCol = 0, totalAlert = 0;
            var resDict = new Dictionary<int, ExtractionRow>();
            var invDict = new Dictionary<int, InventoryRow>();
            var charRows = new ObservableCollection<CharacterRow>();
            var alertList = new ObservableCollection<AlertDisplayRow>();
            var prodDict = new Dictionary<int, ProductionRow>();

            foreach (var c in chars)
            {
                try
                {
                    var planets = await _planetaryService.GetCharacterPlanetsAsync(c);
                    if(planets.Count == 0)
                    {
                        planets.Add(new CharacterPlanet {
                            PlanetId = 1, OwnerId = c.CharacterID, SolarSystemId = 30000001,
                            PlanetType = "temperate", UpgradeLevel = 4, NumPins = 8, LastUpdate = DateTime.Now.AddDays(-1),
                            SolarSystemName = "Jita", PlanetName = "Planet IV", PlanetTypeName = "温带行星", PlanetTypeEmoji = "🌲",
                            NumExtractors = 2, NumFactories = 3, NumHeads = 7, NumActiveExtractors = 1,
                            ProductionSummary = "Base Metals · Noble Gas → Reactive Metals · Water · Oxygen"
                        });
                        planets.Add(new CharacterPlanet {
                            PlanetId = 2, OwnerId = c.CharacterID, SolarSystemId = 30000002,
                            PlanetType = "temperate", UpgradeLevel = 2, NumPins = 1, LastUpdate = DateTime.Now.AddDays(-3),
                            SolarSystemName = "Perimeter", PlanetName = "Planet II", PlanetTypeName = "温带行星", PlanetTypeEmoji = "🌲",
                            NumExtractors = 1, NumFactories = 0, NumHeads = 2, NumActiveExtractors = 1,
                            ProductionSummary = "Aqueous Liquids"
                        });
                        planets.Add(new CharacterPlanet {
                            PlanetId = 3, OwnerId = c.CharacterID, SolarSystemId = 30000003,
                            PlanetType = "temperate", UpgradeLevel = 3, NumPins = 4, LastUpdate = DateTime.Now.AddDays(-2),
                            SolarSystemName = "New Caldari", PlanetName = "Planet I", PlanetTypeName = "温带行星", PlanetTypeEmoji = "🌲",
                            NumExtractors = 2, NumFactories = 1, NumHeads = 7, NumActiveExtractors = 2,
                            ProductionSummary = "Aqueous Liquids · Heavy Metals → Reactive Metals"
                        });
                    }
                    var charRow = new CharacterRow { CharacterName = c.CharacterName };
                    int charEcu = 0, charFac = 0;
                    var nearestExp = DateTime.MaxValue;

                    foreach (var p in planets)
                    {
                        totalCol++;
                        PlanetColonyDetail detail;
                        if (p.PlanetId < 10000)
                        {
                            detail = CreateSimulatedDetail(p.PlanetId, c.CharacterID);
                        }
                        else
                        {
                            detail = await _planetaryService.GetPlanetColonyDetailAsync(c, p.PlanetId);
                        }

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

                        // 收集工厂加工数据（P1-P4），按配方聚合
                        foreach (var pin in detail.Pins)
                        {
                            if ((pin.Category == PinCategory.BasicIndustryFacility || pin.Category == PinCategory.AdvancedIndustryFacility) && pin.SchematicId.HasValue)
                            {
                                var sch = PlanetSchematicService.GetSchematic(pin.SchematicId.Value);
                                if (sch?.Outputs == null || sch.Outputs.Count == 0) continue;
                                var firstOut = sch.Outputs[0];
                                var outType = InvTypeService.QueryType(firstOut.TypeId);
                                var tier = PlanetSchematicService.GetProductTier(firstOut.TypeId) ?? "?";

                                if (!prodDict.TryGetValue(pin.SchematicId.Value, out var prodRow))
                                {
                                    var inputStr = sch.Inputs != null
                                        ? string.Join(" · ", sch.Inputs.Select(inp =>
                                        {
                                            var inName = InvTypeService.QueryType(inp.TypeId)?.TypeName ?? "#" + inp.TypeId;
                                            return $"{inp.Quantity}×{inName}";
                                        }))
                                        : "";
                                    prodRow = new ProductionRow
                                    {
                                        SchematicId = pin.SchematicId.Value,
                                        SchematicName = sch.SchematicName ?? $"#{pin.SchematicId}",
                                        ProductName = outType?.TypeName ?? "#" + firstOut.TypeId,
                                        Tier = tier,
                                        InputSummary = inputStr,
                                        CycleTime = sch.CycleTime,
                                        TotalOutputQty = firstOut.Quantity,
                                    };
                                    prodDict[pin.SchematicId.Value] = prodRow;
                                }
                                else
                                {
                                    prodRow.TotalOutputQty += firstOut.Quantity;
                                }
                                prodRow.FactoryCount++;

                                var pd = new ProductionDetail
                                {
                                    CharacterName = c.CharacterName,
                                    PlanetLabel = p.PlanetName ?? $"Planet {p.PlanetId}",
                                    CycleStart = pin.LastCycleStart?.ToShortTimeString() ?? "--",
                                    CycleEnd = pin.LastCycleStart.HasValue
                                        ? pin.LastCycleStart.Value.AddSeconds(sch.CycleTime).ToShortTimeString()
                                        : "--",
                                    Remaining = pin.LastCycleStart.HasValue
                                        ? FormatTimeLeft(pin.LastCycleStart.Value.AddSeconds(sch.CycleTime))
                                        : "--",
                                    ProgressPercent = pin.LastCycleStart.HasValue
                                        ? Math.Clamp((DateTime.UtcNow - pin.LastCycleStart.Value).TotalSeconds / sch.CycleTime * 100.0, 0, 100)
                                        : 0
                                };
                                prodRow.Details.Add(pd);
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

            ColonyCount = totalCol;
            ExtractorCount = resDict.Sum(x => x.Value.ActiveCount);
            FactoryCount = charRows.Sum(x => x.FactoryCount);
            AlertCount = totalAlert;
            CharacterCount = chars.Count;

            CharacterRows = charRows;
            ExtractionRows = resDict.Count > 0
                ? new ObservableCollection<ExtractionRow>(resDict.Values.OrderByDescending(x => x.HeadCount))
                : new ObservableCollection<ExtractionRow>();
            InventoryRows = invDict.Count > 0
                ? new ObservableCollection<InventoryRow>(invDict.Values.OrderByDescending(x => x.Quantity))
                : new ObservableCollection<InventoryRow>();
            AlertRows = alertList;
            SelectedProductionRow = null;
            ProductionRows = new ObservableCollection<ProductionRow>(prodDict.Values.OrderBy(x => x.Tier).ThenBy(x => x.SchematicName));

            IsEmpty = false;
            return true;
        }

        private static PlanetColonyDetail CreateSimulatedDetail(long planetId, long ownerId)
        {
            var now = DateTime.UtcNow;
            var detail = new PlanetColonyDetail
            {
                PlanetId = planetId,
                OwnerId = ownerId
            };

            // P0 资源 typeId：Aqueous Liquids=2267, Base Metals=2268, Noble Gas=2270, Heavy Metals=2269
            // P1 产物 typeId：Water=3645, Reactive Metals=3695, Oxygen=3683
            switch (planetId)
            {
                case 1: // UpgradeLevel=4, NumPins=8 — 完整工业链
                    detail.UpgradeLevel = 4;
                    detail.Pins = new List<PlanetPin>
                    {
                        new PlanetPin
                        {
                            PinId = 101, TypeId = 2407, // ExtractorControlUnit
                            Extractor = new ExtractorDetails
                            {
                                ProductTypeId = 2268, // Base Metals
                                CycleTime = 300,
                                QtyPerCycle = 800,
                                InstallTime = now.AddDays(-15),
                                ExpiryTime = now.AddDays(5),
                                Heads = Enumerable.Range(1, 4).Select(i => new ExtractorHead { HeadId = i }).ToList()
                            },
                            StorageQuantity = 3200, ContentTypeId = 2268,
                            Category = PinCategory.ExtractorControlUnit
                        },
                        new PlanetPin
                        {
                            PinId = 102, TypeId = 2407,
                            Extractor = new ExtractorDetails
                            {
                                ProductTypeId = 2270, // Noble Gas
                                CycleTime = 240,
                                QtyPerCycle = 600,
                                InstallTime = now.AddDays(-30),
                                ExpiryTime = now.AddDays(-1), // 已过期
                                Heads = Enumerable.Range(1, 3).Select(i => new ExtractorHead { HeadId = i }).ToList()
                            },
                            StorageQuantity = 1800, ContentTypeId = 2270,
                            Category = PinCategory.ExtractorControlUnit
                        },
                        new PlanetPin
                        {
                            PinId = 103, TypeId = 2429, // BasicIndustryFacility
                            SchematicId = 65, // Base Metals → Reactive Metals
                            Category = PinCategory.BasicIndustryFacility,
                            LastCycleStart = now.AddMinutes(-12)
                        },
                        new PlanetPin
                        {
                            PinId = 104, TypeId = 2429,
                            SchematicId = 64, // Aqueous Liquids → Water
                            Category = PinCategory.BasicIndustryFacility,
                            LastCycleStart = now.AddMinutes(-8)
                        },
                        new PlanetPin
                        {
                            PinId = 105, TypeId = 2429,
                            SchematicId = 66, // Noble Gas → Oxygen
                            Category = PinCategory.BasicIndustryFacility,
                            LastCycleStart = now.AddMinutes(-3)
                        },
                        new PlanetPin
                        {
                            PinId = 106, TypeId = 2436, // CommandCenter
                            StorageQuantity = 500, ContentTypeId = 3645,
                            Category = PinCategory.CommandCenter
                        },
                        new PlanetPin
                        {
                            PinId = 107, TypeId = 2437, // StorageFacility
                            StorageQuantity = 1200, ContentTypeId = 2268,
                            Category = PinCategory.StorageFacility
                        },
                        new PlanetPin
                        {
                            PinId = 108, TypeId = 2438, // Launchpad
                            StorageQuantity = 600, ContentTypeId = 3695,
                            Category = PinCategory.Launchpad
                        }
                    };
                    break;

                case 2: // UpgradeLevel=2, NumPins=1 — 最小配置
                    detail.UpgradeLevel = 2;
                    detail.Pins = new List<PlanetPin>
                    {
                        new PlanetPin
                        {
                            PinId = 201, TypeId = 2407,
                            Extractor = new ExtractorDetails
                            {
                                ProductTypeId = 2267, // Aqueous Liquids
                                CycleTime = 600,
                                QtyPerCycle = 200,
                                InstallTime = now.AddDays(-10),
                                ExpiryTime = now.AddDays(10),
                                Heads = Enumerable.Range(1, 2).Select(i => new ExtractorHead { HeadId = i }).ToList()
                            },
                            StorageQuantity = 200, ContentTypeId = 2267,
                            Category = PinCategory.ExtractorControlUnit
                        }
                    };
                    break;

                case 3: // UpgradeLevel=3, NumPins=4 — 中等配置
                    detail.UpgradeLevel = 3;
                    detail.Pins = new List<PlanetPin>
                    {
                        new PlanetPin
                        {
                            PinId = 301, TypeId = 2407,
                            Extractor = new ExtractorDetails
                            {
                                ProductTypeId = 2267, // Aqueous Liquids
                                CycleTime = 300,
                                QtyPerCycle = 900,
                                InstallTime = now.AddDays(-20),
                                ExpiryTime = now.AddDays(7),
                                Heads = Enumerable.Range(1, 5).Select(i => new ExtractorHead { HeadId = i }).ToList()
                            },
                            StorageQuantity = 4500, ContentTypeId = 2267,
                            Category = PinCategory.ExtractorControlUnit
                        },
                        new PlanetPin
                        {
                            PinId = 302, TypeId = 2407,
                            Extractor = new ExtractorDetails
                            {
                                ProductTypeId = 2268,
                                CycleTime = 360,
                                QtyPerCycle = 400,
                                InstallTime = now.AddDays(-25),
                                ExpiryTime = now.AddDays(3),
                                Heads = Enumerable.Range(1, 2).Select(i => new ExtractorHead { HeadId = i }).ToList()
                            },
                            StorageQuantity = 800, ContentTypeId = 2268,
                            Category = PinCategory.ExtractorControlUnit
                        },
                        new PlanetPin
                        {
                            PinId = 303, TypeId = 2429,
                            SchematicId = 65,
                            Category = PinCategory.BasicIndustryFacility,
                            LastCycleStart = now.AddMinutes(-20)
                        },
                        new PlanetPin
                        {
                            PinId = 304, TypeId = 2436, // CommandCenter
                            StorageQuantity = 200, ContentTypeId = 2267,
                            Category = PinCategory.CommandCenter
                        }
                    };
                    break;

                default:
                    detail.UpgradeLevel = 1;
                    detail.Pins = new List<PlanetPin>
                    {
                        new PlanetPin
                        {
                            PinId = planetId * 100 + 1, TypeId = 2407,
                            Extractor = new ExtractorDetails
                            {
                                ProductTypeId = 2267,
                                CycleTime = 600,
                                QtyPerCycle = 100,
                                InstallTime = now.AddDays(-5),
                                ExpiryTime = now.AddDays(15),
                                Heads = Enumerable.Range(1, 1).Select(i => new ExtractorHead { HeadId = i }).ToList()
                            },
                            StorageQuantity = 100, ContentTypeId = 2267,
                            Category = PinCategory.ExtractorControlUnit
                        }
                    };
                    break;
            }

            return detail;
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

    #region 展示行模型

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

    public class ProductionRow
    {
        public int SchematicId { get; set; }
        public string SchematicName { get; set; }
        public string ProductName { get; set; }
        public string Tier { get; set; }
        public string InputSummary { get; set; }
        public int FactoryCount { get; set; }
        public int TotalOutputQty { get; set; }
        public int CycleTime { get; set; }
        public string RemainingSummary { get; set; }
        public List<ProductionDetail> Details { get; set; } = new List<ProductionDetail>();
    }

    public class ProductionDetail
    {
        public string CharacterName { get; set; }
        public string PlanetLabel { get; set; }
        public string CycleStart { get; set; }
        public string CycleEnd { get; set; }
        public string Remaining { get; set; }
        public double ProgressPercent { get; set; }
    }

    #endregion
}
