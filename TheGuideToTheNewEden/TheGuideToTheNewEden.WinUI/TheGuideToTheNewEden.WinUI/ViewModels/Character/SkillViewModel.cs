using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.ViewModels.Character
{
    internal class SkillViewModel : BaseViewModel
    {
        public SkillViewModel() 
        {
            Init();
        }
        private async void Init()
        {
            var skills = await Core.Services.DB.InvGroupService.QuerySkillGroupsAsync();
        }
    }
}
