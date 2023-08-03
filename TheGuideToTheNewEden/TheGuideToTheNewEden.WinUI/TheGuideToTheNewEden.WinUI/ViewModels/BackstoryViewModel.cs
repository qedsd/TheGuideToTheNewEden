using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class BackstoryViewModel:BaseViewModel
    {
        private List<Backstory> backstories;
        public List<Backstory> Backstories
        {
            get => backstories;
            set => SetProperty(ref backstories, value);
        }
        private Backstory selectedBackstory;
        public Backstory SelectedBackstory
        {
            get => selectedBackstory;
            set
            {
                if(SetProperty(ref selectedBackstory, value))
                {
                    BackstoryContent = value == null ? null : BackstoryService.QueryBackstoryContent(value.Id);
                }
            }
        }
        private Backstory selectedListBackstory;
        public Backstory SelectedListBackstory
        {
            get => selectedListBackstory;
            set
            {
                if (SetProperty(ref selectedListBackstory, value))
                {
                    SelectedBackstory = value;
                }
            }
        }
        private int backstoryType;
        public int BackstoryType
        {
            get => backstoryType;
            set
            {
                if (SetProperty(ref backstoryType, value))
                {
                    Load();
                }
            }
        }

        private BackstoryContent backstoryContent;
        public BackstoryContent BackstoryContent
        {
            get => backstoryContent;
            set
            {
                if(SetProperty(ref backstoryContent, value))
                {
                    BackstoryContentEn = value?.Content_En;
                    BackstoryContentZh = value?.Content_Zh;
                }
            }
        }
        private string backstoryContentEn;
        public string BackstoryContentEn
        {
            get => backstoryContentEn;
            set
            {
                SetProperty(ref backstoryContentEn, value);
            }
        }
        private string backstoryContentZh;
        public string BackstoryContentZh
        {
            get => backstoryContentZh;
            set
            {
                SetProperty(ref backstoryContentZh, value);
            }
        }
        public BackstoryViewModel()
        {
            Load();
        }
        private async void Load()
        {
            Backstories = await BackstoryService.QueryBackstoryAsync(BackstoryType);
        }
    }
}
