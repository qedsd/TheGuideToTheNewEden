using ESI.NET;
using ESI.NET.Models.Skills;
using Microsoft.IdentityModel.Tokens;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class SkillPage : Page, ICharacterPage
    {
        private BaseWindow _window;
        private ESI.NET.Models.Skills.SkillDetails _skillDetails;
        public SkillPage()
        {
            this.InitializeComponent();
            Loaded += SkillPage_Loaded;
        }
        protected override void OnNavigatedTo(NavigationEventArgs e) 
        {
            _skillDetails = e.Parameter as ESI.NET.Models.Skills.SkillDetails;
        }

        private void SkillPage_Loaded(object sender, RoutedEventArgs e)
        {
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            if(!_isLoaded)
            {
                Init();
                _isLoaded = true;
            }
        }

        private async void Init()
        {
            var skills = await Core.Services.DB.InvGroupService.QuerySkillGroupsAsync();
            if(_skillDetails != null && _skillDetails.Skills.NotNullOrEmpty())
            {
                var skillsDic = _skillDetails.Skills.ToDictionary(p => p.SkillId);
                var invTypes = await Core.Services.DB.InvTypeService.QueryTypesAsync(_skillDetails.Skills.Select(p=>p.SkillId).ToList());
                if(invTypes.NotNullOrEmpty())
                {
                    var invTypesDic = invTypes.ToDictionary(p => p.TypeID);
                    foreach (var skillGroup in skills)
                    {
                        skillGroup.Skills = new List<Core.Models.Character.SkillItem>();
                        foreach (var skill in skillGroup.SkillIds)
                        {
                            if (skillsDic.TryGetValue(skill, out var value))
                            {
                                string invTypeName = string.Empty;
                                if(invTypesDic.TryGetValue(skill, out var invType))
                                {
                                    invTypeName = invType.TypeName;
                                }
                                skillGroup.Skills.Add(new Core.Models.Character.SkillItem()
                                {
                                    Skill = value,
                                    InvType = invType,
                                });
                            }
                        }
                    }
                }
                
            }
            ListView_Skills.ItemsSource = skills;
        }

        private bool _isLoaded = false;
        public void Clear()
        {
            ListView_Skills.ItemsSource = null;
            _isLoaded = false;
        }
    }
}
