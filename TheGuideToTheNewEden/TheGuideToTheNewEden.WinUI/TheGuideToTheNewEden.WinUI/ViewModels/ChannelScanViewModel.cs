using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

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

        public ICommand StartCommand => new RelayCommand(() =>
        {
            
        });

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
