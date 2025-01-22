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
            Config.PropertyChanged += Config_PropertyChanged;
        }

        private void Config_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            SaveConfig();
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
                    #region 构建忽略id表
                    HashSet<int> ignoredCharacterIds = new HashSet<int>();
                    HashSet<int> ignoredCorpIds = new HashSet<int>();
                    HashSet<int> ignoredAllianceIds = new HashSet<int>();
                    var ignoredCharacters = Config.Ignoreds.Where(p => p.GetCategory() == IdName.CategoryEnum.Character);
                    var ignoredCorps = Config.Ignoreds.Where(p => p.GetCategory() == IdName.CategoryEnum.Corporation);
                    var ignoredAlliances = Config.Ignoreds.Where(p => p.GetCategory() == IdName.CategoryEnum.Alliance);
                    if (ignoredCharacters.NotNullOrEmpty())
                    {
                        ignoredCharacterIds = ignoredCharacters.Select(p => p.Id).ToHashSet2();
                    }
                    if (ignoredCorps.NotNullOrEmpty())
                    {
                        ignoredCorpIds = ignoredCorps.Select(p => p.Id).ToHashSet2();
                    }
                    if (ignoredAlliances.NotNullOrEmpty())
                    {
                        ignoredAllianceIds = ignoredAlliances.Select(p => p.Id).ToHashSet2();
                    }
                    #endregion

                    var allNamesDatas = await Core.Services.IDNameService.GetByNames(namesAfterFiltered);
                    if (allNamesDatas.NotNullOrEmpty())
                    {
                        var characterNames = allNamesDatas.Where(p => p.GetCategory() == Core.DBModels.IdName.CategoryEnum.Character).ToArray();
                        if(characterNames.NotNullOrEmpty())
                        {
                            int start = 0;
                            int length = characterNames.Length > 1000 ? 1000 : characterNames.Length;
                            while (true)
                            {
                                var affiliationResult = await ESIService.Current.EsiClient.Character.Affiliation(characterNames.Skip(start).Take(length).Select(p => p.Id).ToArray());
                                if (affiliationResult?.StatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    var datas = await Task.Run(() =>
                                    {
                                        List<ESI.NET.Models.Character.Affiliation> affiliations = new List<ESI.NET.Models.Character.Affiliation>();
                                        if (!Config.ShowIgnoredInResultDetail && !Config.ShowIgnoredInResultStatistics)//统计、详细均不显示忽略后的则可不获取IdName
                                        {
                                            foreach (var affiliation in affiliationResult.Data)
                                            {
                                                if (ignoredCharacterIds.Contains(affiliation.CharacterId) ||
                                                    ignoredCorpIds.Contains(affiliation.CorporationId) ||
                                                    ignoredAllianceIds.Contains(affiliation.AllianceId))
                                                {
                                                    continue;//忽略
                                                }
                                                else
                                                {
                                                    affiliations.Add(affiliation);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            affiliations = affiliationResult.Data;
                                        }
                                        List<CharacterScanInfo> newInfos = Core.Helpers.ThreadHelper.Run(affiliations, (affiliation) =>
                                        {
                                            return CharacterScanInfo.Create(affiliation.CharacterId, affiliation.CorporationId, affiliation.AllianceId);
                                        }).Where(p=>p != null).ToList();
                                        return newInfos;
                                    });
                                    if (datas.NotNullOrEmpty())
                                    {
                                        #region 统计
                                        var corpGroups = datas.GroupBy(p => p.Corporation.Id).ToList();
                                        List<Tuple<Core.DBModels.IdName, int>> statisticsCorporation = new List<Tuple<Core.DBModels.IdName, int>>(corpGroups.Count);
                                        foreach (var group in corpGroups.OrderByDescending(p => p.Count()))
                                        {
                                            if (ignoredCorpIds.Contains(group.Key))
                                            {
                                                continue;
                                            }
                                            var corp = group.First().Corporation;
                                            Core.DBModels.IdName idName = new Core.DBModels.IdName(corp.Id, corp.Name, Core.DBModels.IdName.CategoryEnum.Corporation);
                                            int countAfterFilter = 0;//剔除忽略后的实际数量
                                            if(!Config.ShowIgnoredInResultStatistics)
                                            {
                                                foreach (var data in group)
                                                {
                                                    if (ignoredCharacterIds.Contains(data.Id))
                                                    {
                                                        continue;
                                                    }
                                                    else
                                                    {
                                                        countAfterFilter++;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                countAfterFilter = group.Count();
                                            }
                                            statisticsCorporation.Add(new Tuple<Core.DBModels.IdName, int>(idName, countAfterFilter));
                                        }
                                        StatisticsCorporation = statisticsCorporation;

                                        var allianceGroups = datas.GroupBy(p => p.Alliance.Id).ToList();
                                        List<Tuple<Core.DBModels.IdName, int>> statisticsAlliance = new List<Tuple<Core.DBModels.IdName, int>>(allianceGroups.Count);
                                        foreach (var group in allianceGroups.OrderByDescending(p => p.Count()))
                                        {
                                            if(ignoredAllianceIds.Contains(group.Key))
                                            {
                                                continue;
                                            }
                                            var item = group.First().Alliance;
                                            Core.DBModels.IdName idName = new Core.DBModels.IdName(item.Id, item.Name, Core.DBModels.IdName.CategoryEnum.Alliance);
                                            int countAfterFilter = 0;//剔除忽略后的实际数量
                                            if (!Config.ShowIgnoredInResultStatistics)
                                            {
                                                foreach (var data in group)
                                                {
                                                    if (ignoredCharacterIds.Contains(data.Id))
                                                    {
                                                        continue;
                                                    }
                                                    else
                                                    {
                                                        countAfterFilter++;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                countAfterFilter = group.Count();
                                            }
                                            statisticsAlliance.Add(new Tuple<Core.DBModels.IdName, int>(idName, countAfterFilter));
                                        }
                                        StatisticsAlliance = statisticsAlliance;
                                        #endregion
                                        #region 排序
                                        var datasDic = datas.ToDictionary(p => p.Name);
                                        ObservableCollection<CharacterScanInfo> characterScanInfos = new ObservableCollection<CharacterScanInfo>();
                                        foreach (var name in namesAfterFiltered)
                                        {
                                            if (datasDic.TryGetValue(name, out var characterScanInfo))
                                            {
                                                characterScanInfos.Add(characterScanInfo);
                                            }
                                        }
                                        #endregion
                                        #region ZKB
                                        if (Config.GetZKB)
                                        {
                                            IList<CharacterScanInfo> needGetZKBDatas;
                                            if (!Config.ShowIgnoredInResultDetail)
                                            {
                                                needGetZKBDatas = new List<CharacterScanInfo>();
                                                foreach (var data in characterScanInfos)
                                                {
                                                    if (ignoredCharacterIds.Contains(data.Character.Id) ||
                                                        ignoredCorpIds.Contains(data.Corporation.Id) ||
                                                        ignoredAllianceIds.Contains(data.Alliance.Id))
                                                    {
                                                        continue;//忽略
                                                    }
                                                    else
                                                    {
                                                        needGetZKBDatas.Add(data);
                                                        if(needGetZKBDatas.Count == (int)Config.MaxZKB)
                                                        {
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                needGetZKBDatas = characterScanInfos.Count > (int)Config.MaxZKB ? characterScanInfos.Take((int)Config.MaxZKB).ToList() : characterScanInfos;//限制zkb获取数据量
                                            }
                                                
                                            await Task.Run(() =>
                                            {
                                                Core.Helpers.ThreadHelper.Run(needGetZKBDatas, (data) =>
                                                {
                                                    return data.GetZKBInfo();
                                                });
                                            });
                                        }
                                        #endregion
                                        ScanInfos = characterScanInfos;
                                        ResultCount = ScanInfos.Count;
                                    }
                                    int found = start + length;
                                    int remain = characterNames.Length - found;
                                    if (remain > 0)
                                    {
                                        start += length;
                                        length = remain > 1000 ? 1000 : remain;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                else
                                {
                                    break;
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
            SaveConfig();
            Window?.ShowSuccess(Helpers.ResourcesHelper.GetString("ChannelScanPage_Setting_AddIgnoredSuccessful"));
            IsAddingIgnore = false;
        });

        public ICommand DeleteIgnoreCommand => new RelayCommand<IdName>((item) =>
        {
            Config.Ignoreds.Remove(item);
            SaveConfig();
        });

        public void AddIgnore(IdName idName)
        {
            if(idName.Id > 0)
            {
                if (Config.Ignoreds.FirstOrDefault(p => p.Id != -1 && p.Id == idName.Id || p.Name == idName.Name) != null)
                {
                    Window?.ShowError(Helpers.ResourcesHelper.GetString("ChannelScanPage_Setting_AddIgnoredSame"));
                    return;
                }
                Config.Ignoreds.Add(idName);
                SaveConfig();
                Window?.ShowSuccess(Helpers.ResourcesHelper.GetString("ChannelScanPage_Setting_AddIgnoredSuccessful"));
            }
        }

        public async void ReloadZKBInfo(CharacterScanInfo characterScanInfo)
        {
            int index = ScanInfos.IndexOf(characterScanInfo);
            ScanInfos.RemoveAt(index);
            Window?.ShowWaiting();
            var clone = new CharacterScanInfo()
            {
                Character = characterScanInfo.Character,
                Corporation = characterScanInfo.Corporation,
                Alliance = characterScanInfo.Alliance,
                Faction = characterScanInfo.Faction
            };
            await Task.Run(()=> clone.GetZKBInfo());
            ScanInfos.Insert(index, clone);
            Window?.HideWaiting();
        }

        private void SaveConfig()
        {
            Services.Settings.ChannelScanSettingService.Save(Config);
        }
    }
}
