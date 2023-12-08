using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    public class LinkInfo
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string ShortDescription { get; set; }
        public string Description { get; set; }
        public string[] Langs { get; set; }
        public string[] Platforms { get; set; }
        public string[] Categories { get; set; }
        public string IconUrl { get; set; }
    }
}
