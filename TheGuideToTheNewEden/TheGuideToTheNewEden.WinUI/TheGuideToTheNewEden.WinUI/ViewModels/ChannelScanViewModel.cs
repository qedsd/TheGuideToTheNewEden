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

        private int _resultCount;
        public int ResultCount { get => _resultCount; set => SetProperty(ref _resultCount, value);}

        private ChannelScanConfig _config;
        public ChannelScanConfig Config { get => _config; set => SetProperty(ref _config, value); }


        private ObservableCollection<CharacterScanInfo> _scanInfos;
        public ObservableCollection<CharacterScanInfo> ScanInfos { get => _scanInfos; set => SetProperty(ref _scanInfos, value); }

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
                                    ScanInfos = datas.ToObservableCollection();
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
    }
}
