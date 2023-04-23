using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Models
{
    internal class ToolItem
    {
        public ToolItem() { }
        public ToolItem(string title, string description, Type pageType)
        {
            Title = title;
            Description = description;
            PageType = pageType;
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public Type PageType { get; set; }
    }
}
