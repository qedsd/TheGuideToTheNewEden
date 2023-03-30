using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.GamePreviews
{
    public class ProcessInfo : ObservableObject
    {
        public IntPtr MainWindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        private bool running;
        public bool Running
        {
            get=>running; set => SetProperty(ref running,value);
        }
    }
}
