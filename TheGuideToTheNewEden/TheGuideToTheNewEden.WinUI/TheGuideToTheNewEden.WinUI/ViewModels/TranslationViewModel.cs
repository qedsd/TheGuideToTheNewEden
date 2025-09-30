using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Extensions;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Interfaces;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using TheGuideToTheNewEden.WinUI.Wins;
using Windows.ApplicationModel.DataTransfer;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class TranslationViewModel: BaseViewModel
    {
        private List<DataBaseSearchItem> _dataBaseSearchItems;
        public List<DataBaseSearchItem> DataBaseSearchItems { get => _dataBaseSearchItems; set => SetProperty(ref _dataBaseSearchItems, value); }

        private DataBaseSearchItem _selectedDataBaseSearchItem;
        public DataBaseSearchItem SelectedDataBaseSearchItem 
        {
            get => _selectedDataBaseSearchItem;
            set
            {
                if(SetProperty(ref _selectedDataBaseSearchItem, value))
                {
                    ShowDetail(value);
                }
            }
        }

        private BitmapImage _searchDetailImg;
        public BitmapImage SearchDetailImg
        {
            get => _searchDetailImg;
            set
            {
                SetProperty(ref _searchDetailImg, value);
            }
        }

        private TranslationItem _translationResult;
        public TranslationItem TranslationResult
        {
            get => _translationResult;
            set
            {
                SetProperty(ref _translationResult, value);
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if(SetProperty(ref _searchText, value))
                {
                    SerachDataBase(value);
                }
            }
        }

        private string _fromLanguage;
        public string FromLanguage
        {
            get => _fromLanguage;
            set
            {
                if (SetProperty(ref _fromLanguage, value))
                {
                    ClientServiceHelper.GetRequiredService<TranslationSettingService>().SetFromLanguage(value);
                }
            }
        }

        private string _toLanguage;
        public string ToLanguage
        {
            get => _toLanguage;
            set
            {
                if (SetProperty(ref _toLanguage, value))
                {
                    ClientServiceHelper.GetRequiredService<TranslationSettingService>().SetToLanguage(value);
                }
            }
        }

        private bool _showEmptyResultTip = false;
        public bool ShowEmptyResultTip
        {
            get => _showEmptyResultTip;
            set => SetProperty(ref _showEmptyResultTip, value);
        }

        public List<string> Froms { get; set; }
        public List<string> Tos { get; set; }
        public TranslationViewModel()
        {
            FromLanguage = ClientServiceHelper.GetRequiredService<TranslationSettingService>().FromLanguage;
            ToLanguage = ClientServiceHelper.GetRequiredService<TranslationSettingService>().ToLanguage;
            Froms = ClientServiceHelper.GetRequiredService<TranslationSettingService>().GetFromLanguages();
            Tos = ClientServiceHelper.GetRequiredService<TranslationSettingService>().GetToLanguages();
        }

        private async void SerachDataBase(string text)
        {
            ShowEmptyResultTip = false;
            if (string.IsNullOrEmpty(text))
            {
                DataBaseSearchItems = null;
                return;
            }
            List<DataBaseSearchItem> items = new List<DataBaseSearchItem>();
            var types = await Core.Services.DB.InvTypeService.SearchAsync(text);
            if (types.NotNullOrEmpty())
            {
                items.AddRange(types);
            }
            var regions = await Core.Services.DB.MapRegionService.SearchAsync(text);
            if (regions.NotNullOrEmpty())
            {
                items.AddRange(regions);
            }
            var systems = await Core.Services.DB.MapSolarSystemService.SearchAsync(text);
            if (systems.NotNullOrEmpty())
            {
                items.AddRange(systems);
            }
            var stations = await Core.Services.DB.StaStationService.SearchAsync(text);
            if (stations.NotNullOrEmpty())
            {
                items.AddRange(stations);
            }
            DataBaseSearchItems = items;
            if (items == null || !items.Any())
            {
                ShowEmptyResultTip = true;
            }
        }

        private void ShowDetail(DataBaseSearchItem searchItem)
        {
            TranslationItem translationItem = null;
            if (searchItem != null)
            {
                switch (searchItem.Type)
                {
                    case DataBaseItemType.InvType:
                        {
                            SearchDetailImg = new BitmapImage(new Uri(Converters.GameImageConverter.GetImageUri(searchItem.ID, Converters.GameImageConverter.ImgType.Type, 64)));
                            translationItem = SearchType(searchItem);
                        }
                        break;
                    case DataBaseItemType.MapRegion:
                        {
                            translationItem = SearchRegion(searchItem);
                        }
                        break;
                    case DataBaseItemType.MapSolarSystem:
                        {
                            translationItem = SearchSystem(searchItem);
                        }
                        break;
                    case DataBaseItemType.StaStation:
                        {
                            translationItem = SearchStation(searchItem);
                        }
                        break;
                    default:
                        {
                            throw new NotImplementedException();
                        }
                }
            }
            if (translationItem != null)
            {
                translationItem.IsFromDataBase = true;
            }
            TranslationResult = translationItem;
        }

        private TranslationItem SearchType(DataBaseSearchItem searchItem)
        {
            TranslationItem translationItem = new TranslationItem()
            {
                ID = searchItem.ID,
                Query = searchItem.Name,
                QueryDescription = searchItem.Description,
            };
            if (searchItem.IsLocal)
            {
                var mainType = Core.Services.DB.InvTypeService.QueryType(searchItem.ID, false);
                if (mainType != null)
                {
                    translationItem.Translation = mainType.TypeName;
                    translationItem.TranslationDescription = mainType.Description;
                }
            }
            else
            {
                var localType = Core.Services.DB.LocalDbService.TranInvType(searchItem.ID);
                if (localType != null)
                {
                    translationItem.Translation = localType.TypeName;
                    translationItem.TranslationDescription = localType.Description;
                }
            }
            return translationItem;
        }
        private TranslationItem SearchRegion(DataBaseSearchItem searchItem)
        {
            TranslationItem translationItem = new TranslationItem()
            {
                ID = searchItem.ID,
                Query = searchItem.Name,
                QueryDescription = searchItem.Description,
            };
            if (searchItem.IsLocal)
            {
                var mainType = Core.Services.DB.MapRegionService.Query(searchItem.ID, false);
                if (mainType != null)
                {
                    translationItem.Translation = mainType.RegionName;
                }
            }
            else
            {
                var localType = Core.Services.DB.LocalDbService.TranMapRegion(searchItem.ID);
                if (localType != null)
                {
                    translationItem.Translation = localType.RegionName;
                }
            }
            return translationItem;
        }
        private TranslationItem SearchSystem(DataBaseSearchItem searchItem)
        {
            TranslationItem translationItem = new TranslationItem()
            {
                ID = searchItem.ID,
                Query = searchItem.Name,
                QueryDescription = searchItem.Description,
            };
            if (searchItem.IsLocal)
            {
                var mainType = Core.Services.DB.MapSolarSystemService.Query(searchItem.ID, false);
                if (mainType != null)
                {
                    translationItem.Translation = mainType.SolarSystemName;
                }
            }
            else
            {
                var localType = Core.Services.DB.LocalDbService.TranMapSolarSystem(searchItem.ID);
                if (localType != null)
                {
                    translationItem.Translation = localType.SolarSystemName;
                }
            }
            return translationItem;
        }
        private TranslationItem SearchStation(DataBaseSearchItem searchItem)
        {
            TranslationItem translationItem = new TranslationItem()
            {
                ID = searchItem.ID,
                Query = searchItem.Name,
                QueryDescription = searchItem.Description,
            };
            if (searchItem.IsLocal)
            {
                var mainType = Core.Services.DB.StaStationService.Query(searchItem.ID, false);
                if (mainType != null)
                {
                    translationItem.Translation = mainType.StationName;
                }
            }
            else
            {
                var localType = Core.Services.DB.LocalDbService.TranStaStation(searchItem.ID);
                if (localType != null)
                {
                    translationItem.Translation = localType.StationName;
                }
            }
            return translationItem;
        }

        public async void Translation()
        {
            ShowEmptyResultTip = false;
            ShowWaiting();
            try
            {
                var result = await ClientServiceHelper.GetRequiredService<ITranslationService>().Translate(SearchText, FromLanguage, ToLanguage);
                if(result != null)
                {
                    TranslationResult = new TranslationItem()
                    {
                        Query = result.Query,
                        Translation = result.Result,
                        From = result.From,
                        To = result.To,
                        IsFromDataBase = false
                    };
                }
                else
                {
                    throw new Exception("Translate Failed");
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                ShowError(ex.Message);
            }
            HideWaiting();
        }

        public ICommand PopWindowCommand => new RelayCommand(() =>
        {
            TranslationWindow translationWindow = new TranslationWindow();
            translationWindow.Activate();
        });
        public ICommand CopyTranslationCommand => new RelayCommand(() =>
        {
            if(TranslationResult != null)
            {
                DataPackage dataPackage = new DataPackage();
                dataPackage.SetText(TranslationResult?.Translation);
                Clipboard.SetContent(dataPackage);
                ShowSuccess(Helpers.ResourcesHelper.GetString("TranslationPage_CopySuccess"));
            }
            
        });
    }
}
