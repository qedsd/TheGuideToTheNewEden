using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentIcons.WinUI;

namespace TheGuideToTheNewEden.WinUI.Models
{
    public class SettingItem : INotifyPropertyChanged
    {
        public FluentIcon Icon { get; set; }
        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }
        public string Description { get; set; }
        public Type PageType { get; set; }
        private object _instance;

        public event PropertyChangedEventHandler PropertyChanged;

        public SettingItem() { }
        public SettingItem(string title, string desc, FluentIcons.Common.Icon icon, Type pageTyp)
        {
            Title = title;
            Description = desc;
            PageType = pageTyp;
            Icon = new FluentIcon()
            {
                Icon = icon,
                IconSize = FluentIcons.Common.IconSize.Resizable,
                IconVariant = FluentIcons.Common.IconVariant.Regular,
            };
        }
        public object GetInstance()
        {
            return _instance ?? Activator.CreateInstance(PageType);
        }
    }
}
