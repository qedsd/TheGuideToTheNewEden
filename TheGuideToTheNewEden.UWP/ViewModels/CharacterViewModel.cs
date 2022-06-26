using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.UWP.Core.Models.Character;

namespace TheGuideToTheNewEden.UWP.ViewModels
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class CharacterViewModel : ObservableObject
    {
        public ObservableCollection<CharacterOauth> CharacterOauths = Services.CharacterService.CharacterOauths;
        private CharacterOauth characterOauth;
        public CharacterOauth CharacterOauth
        {
            get => characterOauth;
            set
            {
                SetProperty(ref characterOauth, value);
            }
        }
        public Core.Enums.GameServerType GameServerType { get; set; } =  Services.GameServerSelectorService.GameServerType;
        public long SP { get; set; }
        public double ISK { get; set; }
        public int LP { get; set; }
        public CharacterViewModel()
        {
            CharacterOauth = CharacterOauths.FirstOrDefault();
            CharacterOauths.CollectionChanged += CharacterOauths_CollectionChanged;
        }

        private void CharacterOauths_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(e.NewItems?.Count > 0)
            {
                CharacterOauth = e.NewItems[0] as CharacterOauth;
            }
            else if(e.OldItems?.Count > 0)
            {
                if(e.OldItems[0] == CharacterOauth)
                {
                    CharacterOauth = CharacterOauths.FirstOrDefault();
                }
            }
        }

        public ICommand AddCommand => new RelayCommand(() =>
        {
            Services.CharacterService.GetAuthorizeByBrower();
        });

        public ICommand RemoveCommand => new RelayCommand<Windows.UI.Xaml.Controls.MenuFlyoutItem>(async(item) =>
        {
            if(item != null)
            {
                await Services.CharacterService.RemoveAsync(item.DataContext as CharacterOauth);
            }
        });
    }
}
