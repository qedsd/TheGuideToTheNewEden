using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models.Universe;
using TheGuideToTheNewEden.Core.Extensions;
using ESI.NET;
using ESI.NET.Enumerations;
using Microsoft.Extensions.Options;
using ESI.NET.Models.SSO;
using Microsoft.UI.Xaml.Media.Imaging;
using CommunityToolkit.WinUI.UI.Controls.TextToolbarSymbols;
using SqlSugar;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class StructuresSettingViewModel:BaseViewModel
    {
        private ObservableCollection<Structure> _structures;
        public ObservableCollection<Structure> Structures
        {
            get => _structures;
            set => SetProperty(ref _structures, value);
        }
        private List<Structure> _selectedStructures;
        public List<Structure> SelectedStructures
        {
            get => _selectedStructures;
            set => SetProperty(ref _selectedStructures, value);
        }
        private List<Structure> _searchStructures;
        public List<Structure> SearchStructures
        {
            get => _searchStructures;
            set => SetProperty(ref _searchStructures, value);
        }
        private List<Structure> _selectedSearchStructures;
        public List<Structure> SelectedSearchStructures
        {
            get => _selectedSearchStructures;
            set => SetProperty(ref _selectedSearchStructures, value);
        }

        private double _addStructureId;
        public double AddStructureId
        {
            get => _addStructureId;
            set => SetProperty(ref _addStructureId, value);
        }

        private bool _searchPublic = true;
        public bool SearchPublic
        {
            get => _searchPublic;
            set => SetProperty(ref _searchPublic, value);
        }

        private bool _searchAsset = true;
        public bool SearchAsset
        {
            get => _searchAsset;
            set => SetProperty(ref _searchAsset, value);
        }

        private bool _searchClone = true;
        public bool SearchClone
        {
            get => _searchClone;
            set => SetProperty(ref _searchClone, value);
        }

        public EsiClient EsiClient;
        private ObservableCollection<AuthorizedCharacterData> _characters;
        public ObservableCollection<AuthorizedCharacterData> Characters
        {
            get => _characters;
            set => SetProperty(ref _characters, value);
        }
        private AuthorizedCharacterData _selectedCharacter;
        public AuthorizedCharacterData SelectedCharacter
        {
            get => _selectedCharacter;
            set
            {
                if(SetProperty(ref _selectedCharacter, value))
                {
                    SetSelectedCharacter(value);
                }
            }
        }

        public StructuresSettingViewModel()
        {
            Characters = Services.CharacterService.CharacterOauths;
            Structures = Services.StructureService.Structures;
            IOptions<EsiConfig> config = Options.Create(new EsiConfig()
            {
                EsiUrl = Core.Config.DefaultGameServer == Core.Enums.GameServerType.Tranquility ? "https://esi.evetech.net/" : "https://esi.evepc.163.com/",
                DataSource = Core.Config.DefaultGameServer == Core.Enums.GameServerType.Tranquility ? DataSource.Tranquility : DataSource.Singularity,
                ClientId = Core.Config.ClientId,
                SecretKey = "Unneeded",
                CallbackUrl = Core.Config.ESICallback,
                UserAgent = "TheGuideToTheNewEden",
            });
            EsiClient = new ESI.NET.EsiClient(config);
            SelectedCharacter = Characters.FirstOrDefault();
        }
        private void SetSelectedCharacter(AuthorizedCharacterData characterData)
        {
            EsiClient.SetCharacterData(characterData);
        }
        public ICommand AddCommand => new RelayCommand(() =>
        {
            if(SelectedSearchStructures.NotNullOrEmpty())
            {
                foreach(var structure in SelectedSearchStructures)
                {
                    Structures.Add(structure);
                }
                Services.StructureService.Save();
            }
        });
        public ICommand AddIDCommand => new RelayCommand(async() =>
        {
            Window?.ShowWaiting();
            if (AddStructureId > 0)
            {
                var s = await GetStructure((long)AddStructureId);
                if(s != null)
                {
                    Window.ShowSuccess($"添加成功：{s.Name}");
                    Structures.Add(s);
                    Services.StructureService.Save();
                }
                else
                {
                    Window.ShowError("请输入有效ID");
                }
            }
            else
            {
                Window.ShowError("请输入有效ID");
            }
            Window?.HideWaiting();
        });
        public ICommand RemoveCommand => new RelayCommand(() =>
        {
            if(SelectedStructures.NotNullOrEmpty())
            {
                foreach(var item in SelectedStructures)
                {
                    Structures.Remove(item);
                }
                Services.StructureService.Save();
            }
        });
        public ICommand SearchCommand => new RelayCommand(() =>
        {
            GetStructures();
        });
        private async void GetStructures()
        {
            Window?.ShowWaiting();
            if (SearchPublic || SearchAsset || SearchClone)
            {
                if (SelectedCharacter != null)
                {
                    if (!SelectedCharacter.IsTokenValid())
                    {
                        if (!await SelectedCharacter.RefreshTokenAsync())
                        {
                            Window?.HideWaiting();
                            Window?.ShowError("Token已过期，尝试刷新失败");
                            return;
                        }
                    }
                }
                else
                {
                    Window?.ShowError("请选择角色");
                    Window?.HideWaiting();
                    return;
                }
                SearchStructures = await GetAllStructures();
            }
            else
            {
                Window?.ShowError("请选择至少一种获取建筑ID方式");
            }
            Window?.HideWaiting();
        }
        private async Task<List<Core.Models.Universe.Structure>> GetAllStructures()
        {
            List<long> ids = new List<long>();
            if (SearchPublic)
            {
                var ids1 = await GetStructureIdsByPublic();
                if (ids1.NotNullOrEmpty())
                {
                    ids.AddRange(ids1);
                }
            }
            if (SearchAsset)
            {
                var ids3 = await GetStructuresByAssets();
                if (ids3.NotNullOrEmpty())
                {
                    ids.AddRange(ids3);
                }
            }
            if (SearchClone)
            {
                var ids2 = await GetStructureIdsByClone();
                if (ids2.NotNullOrEmpty())
                {
                    ids.AddRange(ids2);
                }
            }
            return await GetStructures(ids.Distinct().ToList());
        }
        private async Task<List<long>> GetStructuresByAssets()
        {
            List<ESI.NET.Models.Assets.Item> assetsItems = new List<ESI.NET.Models.Assets.Item>();
            int page = 1;
            while (true)
            {
                var resp = await EsiClient.Assets.ForCharacter(page++);
                if (resp != null)
                {
                    if (resp.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        if (resp.Data.NotNullOrEmpty())
                        {
                            assetsItems.AddRange(resp.Data);
                        }
                        if (resp.Data.Count != 1000)
                        {
                            break;
                        }
                    }
                    else
                    {
                        Window?.ShowError(resp.Message);
                        Core.Log.Error(resp.Message);
                        break;
                    }
                }
            }
            if(assetsItems.Any())
            {
                var items = assetsItems.Where(p => p.LocationId > 70000000).ToList();
                if(items.NotNullOrEmpty())
                {
                    return items.Select(p => p.LocationId).Distinct().ToList();
                }
            }
            return null;
        }
        private async Task<List<long>> GetStructureIdsByPublic()
        {
            var resp = await EsiClient.Universe.Structures();
            if (resp != null)
            {
                if (resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return resp.Data.ToList();
                }
                else
                {
                    Window?.ShowError(resp.Message);
                    Core.Log.Error(resp.Message);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
        private async Task<List<long>> GetStructureIdsByClone()
        {
            var resp = await EsiClient.Clones.List();
            if (resp != null)
            {
                if (resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    List<long> ids = new List<long>();
                    if (resp.Data.HomeLocation.LocationType == "structure")
                    {
                        ids.Add(resp.Data.HomeLocation.LocationId);
                    }
                    if(resp.Data.JumpClones.NotNullOrEmpty())
                    {
                        var structureClones = resp.Data.JumpClones.Where(p => p.LocationType == "structure").ToList();
                        if(structureClones.NotNullOrEmpty())
                        {
                            ids.AddRange(structureClones.Select(p => p.LocationId));
                        }
                    }
                    return ids;
                }
                else
                {
                    Window?.ShowError(resp.Message);
                    Core.Log.Error(resp.Message);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
        private async Task<List<Core.Models.Universe.Structure>> GetStructures(List<long> ids)
        {
            var result = await Core.Helpers.ThreadHelper.RunAsync(ids, GetStructure);
            var data =  result?.Where(p=>p!=null).ToList();
            if(data.NotNullOrEmpty())
            {
                var systems = await Core.Services.DB.MapSolarSystemService.QueryAsync(data.Select(p=>p.SolarSystemId).Distinct().ToList());
                if(systems.NotNullOrEmpty())
                {
                    var dic = systems.ToDictionary(p => p.SolarSystemID);
                    foreach(var d in data)
                    {
                        if(dic.TryGetValue(d.SolarSystemId, out var value))
                        {
                            d.RegionId = value.RegionID;
                            d.SolarSystemName = value.SolarSystemName;
                        }
                    }
                    var regions = await Core.Services.DB.MapRegionService.QueryAsync(data.Select(p=>p.RegionId).Distinct().ToList());
                    if(regions.NotNullOrEmpty())
                    {
                        var dic2 = regions.ToDictionary(p => p.RegionID);
                        foreach (var d in data)
                        {
                            if (dic2.TryGetValue(d.RegionId, out var value))
                            {
                                d.RegionName = value.RegionName;
                            }
                        }
                    }
                }
            }
            return data;
        }
        private async Task<Core.Models.Universe.Structure> GetStructure(long id)
        {
            var resp = await EsiClient.Universe.Structure(id);
            if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var system = await Core.Services.DB.MapSolarSystemService.QueryAsync(resp.Data.SolarSystemId);
                if (system != null)
                {
                    var region = await Core.Services.DB.MapRegionService.QueryAsync(system.RegionID);
                    if(region != null)
                    {
                        return new Structure()
                        {
                            Id = id,
                            Name = resp.Data.Name,
                            SolarSystemId = resp.Data.SolarSystemId,
                            SolarSystemName = system.SolarSystemName,
                            RegionId = region.RegionID,
                            RegionName = region.RegionName,
                            CharacterId = SelectedCharacter.CharacterID
                        };
                    }
                }
                return new Structure()
                {
                    Id = id,
                    Name = resp.Data.Name,
                    SolarSystemId = resp.Data.SolarSystemId,
                    CharacterId = SelectedCharacter.CharacterID
                };
            }
            else
            {
                return null;
            }
        }
    }
}
