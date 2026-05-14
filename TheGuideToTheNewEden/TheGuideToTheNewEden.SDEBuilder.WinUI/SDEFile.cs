using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.SDEBuilder.WinUI
{
    public class SDEFile:INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private bool? _checked = true;
        public bool? Checked
        {
            get => _checked;
            set
            {
                _checked = value;
                NotifyPropertyChanged(nameof(Checked));
            }
        }

        public string File {  get; set; }

        public SDEFile(string file)
        {
            File = file;
        }
    }
}
