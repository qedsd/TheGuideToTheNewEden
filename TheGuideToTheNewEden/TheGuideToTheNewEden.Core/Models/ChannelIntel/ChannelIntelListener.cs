using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.ChannelIntel
{
    public class ChannelIntelListener : ObservableObject
    {
        public string Name {  get; set; }

        private bool _running;
        public bool Running
        {
            get => _running;
            set => SetProperty(ref _running, value);
        }

        public ChannelIntelListener(string name)
        {
            Name = name;
        }
    }
}
