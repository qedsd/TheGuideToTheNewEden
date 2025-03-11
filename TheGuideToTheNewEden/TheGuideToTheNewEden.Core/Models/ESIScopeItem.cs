using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    public class ESIScopeItem : ObservableObject
    {
        private bool selected;
        public bool Selected
        {
            get => selected;
            set => SetProperty(ref selected, value);
        }

        public string Scope {  get; set; }
    }
}
