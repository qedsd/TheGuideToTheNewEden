using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Models
{
    public class NavigationTokenItem
    {
        public string Title { get; set; }
        public object Data { get; set; }
        public NavigationTokenItem(string title, object data)
        {
            Title = title;
            Data = data;
        }
    }
}
