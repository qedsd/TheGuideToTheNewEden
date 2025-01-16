using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.Core.Models.CharacterScan;
using System.Collections.ObjectModel;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class ChannelScanViewModel : BaseViewModel
    {
        private string _namesStr;
        public string NamesStr
        {
            get => _namesStr;
            set => SetProperty(ref _namesStr, value);
        }

        private bool _isSetting;
        public bool IsSetting
        {
            get => _isSetting;
            set => SetProperty(ref _isSetting, value);
        }

        private bool _isAddingIgnore;
        public bool IsAddingIgnore
        {
            get => _isAddingIgnore;
            set => SetProperty(ref _isAddingIgnore, value);
        }

        private string _addingIgnoreID;
        public string AddingIgnoreID
        {
            get => _addingIgnoreID;
            set => SetProperty(ref _addingIgnoreID, value);
        }

        private string _addingIgnoreName;
        public string AddingIgnoreName
        {
            get => _addingIgnoreName;
            set => SetProperty(ref _addingIgnoreName, value);
        }

        private int _addingIgnoreCategory;
        public int AddingIgnoreCategory
        {
            get => _addingIgnoreCategory;
            set => SetProperty(ref _addingIgnoreCategory, value);
        }

        private int _resultCount;
        public int ResultCount { get => _resultCount; set => SetProperty(ref _resultCount, value);}

        private ChannelScanConfig _config;
        public ChannelScanConfig Config { get => _config; set => SetProperty(ref _config, value); }


        private ObservableCollection<CharacterScanInfo> _scanInfos;
        public ObservableCollection<CharacterScanInfo> ScanInfos { get => _scanInfos; set => SetProperty(ref _scanInfos, value); }

        public List<Tuple<Core.DBModels.IdName,int>> _statisticsCorporation;
        public List<Tuple<Core.DBModels.IdName, int>> StatisticsCorporation { get => _statisticsCorporation; set => SetProperty(ref _statisticsCorporation, value); }
        public List<Tuple<Core.DBModels.IdName, int>> _statisticsAlliance;
        public List<Tuple<Core.DBModels.IdName, int>> StatisticsAlliance { get => _statisticsAlliance; set => SetProperty(ref _statisticsAlliance, value); }

        public ChannelScanViewModel()
        {
            Config = Services.Settings.ChannelScanSettingService.GetChannelScanConfig();
        }

        public ICommand StartCommand => new RelayCommand(async() =>
        {
            Window?.ShowWaiting();
            try
            {
                ResultCount = 0;
                var names = GetNames(NamesStr);
                var namesAfterFiltered = GetFilteredNames(names);
                if (namesAfterFiltered.NotNullOrEmpty())
                {
                    var allNamesDatas = await Core.Services.IDNameService.GetByNames(namesAfterFiltered);
                    if (allNamesDatas.NotNullOrEmpty())
                    {
                        var characterNames = allNamesDatas.Where(p => p.GetCategory() == Core.DBModels.IdName.CategoryEnum.Character).ToArray();
                        if(characterNames.NotNullOrEmpty())
                        {
                            var affiliationResult = await ESIService.Current.EsiClient.Character.Affiliation(characterNames.Select(p => p.Id).ToArray());
                            if (affiliationResult?.StatusCode == System.Net.HttpStatusCode.OK)
                            {
                                var datas = await Task.Run(() =>
                                {
                                    List<CharacterScanInfo> newInfos = new List<CharacterScanInfo>();
                                    foreach(var affiliation in affiliationResult.Data)
                                    {
                                        var info = CharacterScanInfo.Create(affiliation.CharacterId, affiliation.CorporationId, affiliation.AllianceId);
                                        if(info != null)
                                        {
                                            newInfos.Add(info);
                                        }
                                    }
                                    return newInfos;
                                });
                                if(datas.NotNullOrEmpty())
                                {
                                    //统计
                                    var corpGroups = datas.Select(p => p.Corporation).GroupBy(p => p.Id).ToList();
                                    List<Tuple<Core.DBModels.IdName, int>> statisticsCorporation = new List<Tuple<Core.DBModels.IdName, int>>(corpGroups.Count);
                                    foreach(var group in corpGroups.OrderByDescending(p=>p.Count()))
                                    {
                                        var corp = group.First();
                                        Core.DBModels.IdName idName = new Core.DBModels.IdName(corp.Id, corp.Name, Core.DBModels.IdName.CategoryEnum.Corporation);
                                        statisticsCorporation.Add(new Tuple<Core.DBModels.IdName, int>(idName, group.Count()));
                                    }
                                    StatisticsCorporation = statisticsCorporation;

                                    var allianceGroups = datas.Select(p => p.Alliance).GroupBy(p => p.Id).ToList();
                                    List<Tuple<Core.DBModels.IdName, int>> statisticsAlliance = new List<Tuple<Core.DBModels.IdName, int>>(allianceGroups.Count);
                                    foreach (var group in allianceGroups.OrderByDescending(p => p.Count()))
                                    {
                                        var item = group.First();
                                        Core.DBModels.IdName idName = new Core.DBModels.IdName(item.Id, item.Name, Core.DBModels.IdName.CategoryEnum.Alliance);
                                        statisticsAlliance.Add(new Tuple<Core.DBModels.IdName, int>(idName, group.Count()));
                                    }
                                    StatisticsAlliance = statisticsAlliance;

                                    // 排序
                                    var datasDic = datas.ToDictionary(p => p.Name);
                                    ObservableCollection<CharacterScanInfo> characterScanInfos = new ObservableCollection<CharacterScanInfo>();
                                    foreach(var name in namesAfterFiltered)
                                    {
                                        if(datasDic.TryGetValue(name, out var characterScanInfo))
                                        {
                                            characterScanInfos.Add(characterScanInfo);
                                        }
                                    }
                                    ScanInfos = characterScanInfos;
                                    ResultCount = ScanInfos.Count;
                                }
                            }
                        }
                    }
                    else
                    {
                        Window?.ShowError(Helpers.ResourcesHelper.GetString("ChannelScanPage_NoValidName"));
                    }
                }
                else
                {
                    Window?.ShowError(Helpers.ResourcesHelper.GetString("ChannelScanPage_NoValidName"));
                }
            }
            catch(Exception ex)
            {
                Window?.ShowError(ex.Message);
                Core.Log.Error(ex);
            }
            finally
            {
                Window?.HideWaiting();
            }
        });

        private List<string> GetNames(string str)
        {
            if(string.IsNullOrEmpty(str)) return null;
            List<string> strings = new List<string>();
            var names = str.Split("\r");
            foreach (var p in names)
            {
                string name = p.Replace("\n", "");
                if (!string.IsNullOrEmpty(name))
                {
                    strings.Add(name);
                }
            }
            return strings;
        }
        private List<string> GetFilteredNames(List<string> names)
        {
            if (names.NotNullOrEmpty())
            {
                if (Config.Ignoreds.NotNullOrEmpty())
                {
                    var ignoredCharacters = Config.Ignoreds.Where(p => p.GetCategory() == Core.DBModels.IdName.CategoryEnum.Character).ToArray();
                    if (ignoredCharacters.NotNullOrEmpty())
                    {
                        List<string> afterFiltered = new List<string>();
                        var ignoredCharactersHashSet = ignoredCharacters.Select(p => p.Name).ToHashSet2();
                        foreach (var name in names)
                        {
                            if (!ignoredCharactersHashSet.Contains(name))
                            {
                                afterFiltered.Add(name);
                            }
                        }
                        return afterFiltered;
                    }
                    else
                    {
                        return names;
                    }
                }
                else
                {
                    return names;
                }
            }
            else
            {
                return names;
            }
        }

        public ICommand SettingCommand => new RelayCommand(() =>
        {
            IsSetting = true;
        });
        public ICommand HideSettingCommand => new RelayCommand(() =>
        {
            IsSetting = false;
        });
        public ICommand AddIgnoreCommand => new RelayCommand(() =>
        {
            AddingIgnoreID = string.Empty;
            AddingIgnoreName = string.Empty;
            IsAddingIgnore = true;
        });
        public ICommand CancelAddIgnoreCommand => new RelayCommand(() =>
        {
            IsAddingIgnore = false;
        });
        public ICommand ConfirmAddIgnoreCommand => new RelayCommand(() =>
        {
            int id = -1;
            if(!string.IsNullOrEmpty(AddingIgnoreID))
            {
                if(!int.TryParse(AddingIgnoreID, out id))
                {
                    Window?.ShowError(Helpers.ResourcesHelper.GetString("ChannelScanPage_Setting_AddIgnoredIdInvalid"));
                    return;
                }
            }
            if (string.IsNullOrEmpty(AddingIgnoreName))
            {
                Window?.ShowError(Helpers.ResourcesHelper.GetString("ChannelScanPage_Setting_AddIgnoredNameInvalid"));
                return;
            }
            if (Config.Ignoreds.FirstOrDefault(p=>p.Id != -1 && p.Id == id || p.Name == AddingIgnoreName) != null)
            {
                Window?.ShowError(Helpers.ResourcesHelper.GetString("ChannelScanPage_Setting_AddIgnoredSame"));
                return;
            }
            Core.DBModels.IdName.CategoryEnum category = Core.DBModels.IdName.CategoryEnum.Character;
            switch(AddingIgnoreCategory)
            {
                case 0:category = Core.DBModels.IdName.CategoryEnum.Character;break;
                case 1: category = Core.DBModels.IdName.CategoryEnum.Corporation; break;
                case 2: category = Core.DBModels.IdName.CategoryEnum.Alliance; break;
            }
            Config.Ignoreds.Add(new Core.DBModels.IdName(id, AddingIgnoreName, category));
            Window?.ShowSuccess(Helpers.ResourcesHelper.GetString("ChannelScanPage_Setting_AddIgnoredSuccessful"));
            IsAddingIgnore = false;
        });

        public ICommand DeleteIgnoreCommand => new RelayCommand<IdName>((item) =>
        {
            Config.Ignoreds.Remove(item);
        });
    }
}
