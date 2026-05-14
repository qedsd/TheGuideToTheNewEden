using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TheGuideToTheNewEden.Core.Models.Channel
{
    public class ChannelListener : ObservableObject
    {
        public string Name { get; set; }

        private bool _running;
        public bool Running
        {
            get => _running;
            set => SetProperty(ref _running, value);
        }

        public ChannelListener(string name)
        {
            Name = name;
        }
    }
}
