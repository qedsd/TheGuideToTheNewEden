using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class DEDViewModel:BaseViewModel
    {
        private List<DED> deds;
        public List<DED> DEDs
        {
            get => deds;
            set => SetProperty(ref deds, value);
        }
        private DED selectedDED;
        public DED SelectedDED
        {
            get => selectedDED;
            set => SetProperty(ref selectedDED, value);
        }
        private DED selectedListDED;
        public DED SelectedListDED
        {
            get => selectedListDED;
            set
            {
                if(SetProperty(ref selectedListDED, value))
                {
                    SelectedDED = value;
                }
            }
        }
        private int dedType;
        public int DEDType
        {
            get => dedType;
            set
            {
                if(SetProperty(ref dedType, value))
                {
                    LoadDEDs();
                }
            }
        }
        public DEDViewModel()
        {
            LoadDEDs();
        }
        private async void LoadDEDs()
        {
            DEDs = await Core.Services.DB.DEDService.QueryAllAsync(DEDType);
        }
    }
}
