using CommunityToolkit.Mvvm.Input;
using ESI.NET.Logic;
using ESI.NET.Models.SSO;
using Microsoft.IdentityModel.Tokens;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Models.Mail;
using TheGuideToTheNewEden.Core.Models.Wallet;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.Core.Extensions;
using ESI.NET;
using Newtonsoft.Json.Linq;
using ESI.NET.Enumerations;
using Microsoft.Extensions.Options;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Dialogs;
using TheGuideToTheNewEden.WinUI.Converters;
using Vanara.PInvoke;
using ESI.NET.Models.Character;
using TheGuideToTheNewEden.WinUI.Views;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class CharactersViewModel: BaseViewModel
    {
        private ObservableCollection<CharacterViewModel> _characters;
        public ObservableCollection<CharacterViewModel> Characters
        {
            get => _characters;
            set => SetProperty(ref _characters, value); 
        }

        private int _charactersCount;
        public int CharactersCount { get => _charactersCount; set => SetProperty(ref _charactersCount, value); }

        private string _totalSP;
        public string TotalSP { get => _totalSP; set => SetProperty(ref _totalSP, value); }

        private string _totalISK;
        public string TotalISK { get => _totalISK; set => SetProperty(ref _totalISK, value); }

        private string _totalLP;
        public string TotalLP { get => _totalLP; set => SetProperty(ref _totalLP, value); }

        public CharactersViewModel()
        {
            Init();
        }
        private async void Init()
        {
            var characterDatas = Services.CharacterService.CharacterOauths;
            if (characterDatas.NotNullOrEmpty())
            {
                var vms = characterDatas.Select(p => new CharacterViewModel(p)).ToList();
                Window?.ShowWaiting();
                await Core.Helpers.ThreadHelper.RunAsync(vms, (c) =>
                {
                    c.Init();
                });
                Window.HideWaiting();
                Characters = vms.ToObservableCollection();
            }
            else
            {
                Characters = new ObservableCollection<CharacterViewModel>();
            }
            Characters.Add(new CharacterViewModel());
            Calstatistic();
        }
        public ICommand AddCommand => new RelayCommand(async() =>
        {
            if (GameServerSelectorService.Value == Core.Enums.GameServerType.Tranquility)
            {
                if (!AuthHelper.RegistyProtocol())
                {
                    Window.ShowError(Helpers.ResourcesHelper.GetString("CharacterPage_RegistyProtocol"));
                    return;
                }
                var result = await AddTranquilityAuthDialog.ShowAsync(Window.Content.XamlRoot);
                if(result != null)
                {
                    Window.ShowSuccess(Helpers.ResourcesHelper.GetString("CharacterPage_AddSuccess"));
                    var vm = new CharacterViewModel(result);
                    Window.ShowWaiting();
                    await Task.Run(()=>vm.Init());
                    Characters.Insert(Characters.Count - 1, vm);
                    Calstatistic();
                    Window.HideWaiting();
                }
            }
            else
            {
                var result = await AddSerenityAuthDialog.ShowAsync(Window.Content.XamlRoot);
                if (result != null)
                {
                    Window.ShowSuccess(Helpers.ResourcesHelper.GetString("CharacterPage_AddSuccess"));
                    
                }
            }
            
        });

        public ICommand RemoveCommand => new RelayCommand<CharacterViewModel>((character) =>
        {
            Services.CharacterService.Remove(character.SelectedCharacter);
            Characters.Remove(character);
            Calstatistic();
            Window.ShowSuccess("已删除角色");
        });

        public ICommand ShowCommand => new RelayCommand<CharacterViewModel>((character) =>
        {
            Services.NavigationService.NavigateTo(new CharacterPage(character), character.SelectedCharacter.CharacterName);
        });

        private void Calstatistic()
        {
            var cs = Characters.Take(Characters.Count - 1).ToList();
            CharactersCount = Characters.Count - 1;
            TotalSP = cs.Sum(p => p.Skill.TotalSp).ToString("N0");
            TotalISK = cs.Sum(p => p.CharacterWallet).ToString("N2");
            TotalLP = cs.Sum(p => p.LoyaltyPoints.Sum(p2=>p2.LoyaltyPoints)).ToString("N0");
        }
    }
}
