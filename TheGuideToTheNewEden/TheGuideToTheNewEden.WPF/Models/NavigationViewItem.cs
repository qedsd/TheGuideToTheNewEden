using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WPF.Models
{
    internal class NavigationViewItem
    {
        public Type Type { get; set; }
        public string Title { get; set; }
        public NavigationViewItem() { }

        public NavigationViewItem(Type type)
        {
            Type = type;
            Title = Helpers.ResourcesHelper.GetString($"Navigation.{type.Name}");
        }
        public NavigationViewItem(Type type, string title)
        {
            Type = type;
            Title = title;
        }
    }
}
