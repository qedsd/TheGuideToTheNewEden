using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WPF.Services
{
    public class SettingService : IService
    {
        public void Init()
        {
            Core.Services.Settings.SettingService.Load();
        }

        public void Dispose()
        {
            
        }
    }
}
