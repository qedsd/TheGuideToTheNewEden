using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WPF.Models
{
    internal class NavigationViewItem : INotifyPropertyChanged
    {
        public Type Type { get; set; }
        public string Title { get; set; }
        public NavigationViewItem() { }

        public NavigationViewItem(Type type)
        {
            Type = type;
            Title = $"Navigation.{type.Name}";
            Core.Services.LanguageSelectorService.AppLanguageChanged += LanguageSelectorService_AppLanguageChanged;
        }
        public NavigationViewItem(Type type, string title)
        {
            Type = type;
            Title = title;
            Core.Services.LanguageSelectorService.AppLanguageChanged += LanguageSelectorService_AppLanguageChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void LanguageSelectorService_AppLanguageChanged(object sender, Core.Services.LanguageSelectorService.AppLanguage e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }
}
