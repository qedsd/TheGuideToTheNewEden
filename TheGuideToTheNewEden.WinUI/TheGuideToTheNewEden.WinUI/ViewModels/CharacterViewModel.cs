using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.WinUI.Helpers;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class CharacterViewModel: BaseViewModel
    {
        public ICommand AddCommand => new RelayCommand(async() =>
        {
            AuthHelper.RegistyProtocol();
            var result = await AuthHelper.WaitingAuthAsync();
        });
    }
}
