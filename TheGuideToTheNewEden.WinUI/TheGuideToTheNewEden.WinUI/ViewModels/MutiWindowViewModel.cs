using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class MutiWindowViewModel : BaseViewModel
    {
        private Core.Models.WindowPreviews.PreviewSetting setting;
        public Core.Models.WindowPreviews.PreviewSetting Setting
        {
            get => setting;
            set => SetProperty(ref setting, value);
        }
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "GamePreviewSetting.json");
        public MutiWindowViewModel() 
        {
            string json = System.IO.File.ReadAllText(Path);
            if(string.IsNullOrEmpty(json))
            {
                Setting = new Core.Models.WindowPreviews.PreviewSetting();
            }
            else
            {
                Setting = JsonConvert.DeserializeObject<Core.Models.WindowPreviews.PreviewSetting>(json);
            }
        }
    }
}
