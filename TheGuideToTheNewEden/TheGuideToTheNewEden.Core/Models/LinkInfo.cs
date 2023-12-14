using Newtonsoft.Json.Linq;
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

        public string GetLangs(char symbol)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var c in Langs)
            {
                stringBuilder.Append(c);
                stringBuilder.Append(symbol);
            }
            if (stringBuilder.Length > 1)
            {
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
            }
            return stringBuilder.ToString();
        }
        public string GetPlatforms(char symbol)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var c in Platforms)
            {
                stringBuilder.Append(c);
                stringBuilder.Append(symbol);
            }
            if (stringBuilder.Length > 1)
            {
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
            }
            return stringBuilder.ToString();
        }
        public string GetCategories(char symbol)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var c in Categories)
            {
                stringBuilder.Append(c);
                stringBuilder.Append(symbol);
            }
            if (stringBuilder.Length > 1)
            {
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
            }
            return stringBuilder.ToString();
        }
    }
}
