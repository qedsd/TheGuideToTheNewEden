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
        private ObservableCollection<Structure> _searchStructures;
        public ObservableCollection<Structure> SearchStructures
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
                SetSelectedCharacter(value);
                SetProperty(ref _selectedCharacter, value);
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
        }
        private void SetSelectedCharacter(AuthorizedCharacterData characterData)
        {
            EsiClient.SetCharacterData(characterData);
            if(characterData != null)
            {
                GetStructures();
            }
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
        private async void GetStructures()
        {
            Window?.ShowWaiting();
            if (!SelectedCharacter.IsTokenValid())
            {
                if (!await SelectedCharacter.RefreshTokenAsync())
                {
                    Window?.HideWaiting();
                    Window?.ShowError("Token已过期，尝试刷新失败");
                    return;
                }
            }
            var assets = await GetStructuresByAssets();
            Window?.HideWaiting();
        }
        private async Task<List<Core.Models.Universe.Structure>> GetStructuresByAssets()
        {
            List<ESI.NET.Models.Assets.Item> assetsItems = new List<ESI.NET.Models.Assets.Item>();
            while (true)
            {
                var resp = await EsiClient.Assets.ForCharacter();
                if (resp != null)
                {
                    if (resp.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        if (!resp.Data.NotNullOrEmpty())
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
                var items = assetsItems.Where(p => p.LocationType == "structure").ToList();
                if(items.NotNullOrEmpty())
                {
                    return await GetStructures(items.Select(p=>p.LocationId).ToList());
                }
            }
            return null;
        }
        private async Task<List<Core.Models.Universe.Structure>> GetStructures(List<long> ids)
        {
            List<Structure> list = new List<Structure>();
            foreach (var id in ids)
            {
                var s = await GetStructure(id);
                if(s != null)
                {
                    list.Add(s);
                }
            }
            return list;
        }
        private async Task<Core.Models.Universe.Structure> GetStructure(long id)
        {
            var resp = await Core.Services.ESIService.Current.EsiClient.Universe.Structure(12121);
            if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return new Structure()
                {
                    Id = id,
                    Name = resp.Data.Name,
                    SolarSystemId = resp.Data.SolarSystemId
                };
            }
            else
            {
                return null;
            }
        }
    }
}
