using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models.Character;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace TheGuideToTheNewEden.UWP.Controls
{
    public sealed partial class SkillTree : UserControl
    {
        public SkillTree()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty GetSkillGroup = DependencyProperty.Register
            (
            //name    要注册的依赖项对象的名称
            "SkillGroup",
            //propertyType    该属性的类型，作为类型参考
            typeof(SkillGroup),
            //ownerType    正在注册依赖项属性的所有者类型，作为类型参考
            typeof(UserControl),
            //defaultMetadata    属性元数据实例。这可以包含一个 PropertyChangedCallback 实现引用。
            new PropertyMetadata(0, new PropertyChangedCallback(SetSkillGroup))
            );

        private static void SetSkillGroup(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SkillTree skillTree = (SkillTree)d;
            SkillGroup skillGroupTree = e.NewValue as SkillGroup;
            if (skillGroupTree != null)
            {
                skillTree.TextBlock_GroupName.Text = skillGroupTree.GroupName;
                if (skillGroupTree.Skills != null && skillGroupTree.Skills.Count!=0)
                {
                    int trainedSkill = 0;
                    foreach (var temp in skillGroupTree.Skills)
                    {
                        if (temp.Trained_skill_level == 5)
                            trainedSkill++;
                    }
                    skillTree.TextBlock_TrainedSkill.Text = trainedSkill.ToString();
                    skillTree.TextBlock_TotalSkill.Text = skillGroupTree.Skills.Count.ToString();
                    skillTree.TextBlock_TrainedSP.Text = skillGroupTree.Skills.Sum(p => p.Skillpoints_in_skill).ToString();
                    skillTree.ListBox_MasterSkill.ItemsSource = skillGroupTree.Skills;
                }
            }
        }
        public SkillGroup SkillGroup
        {
            get { return (SkillGroup)GetValue(GetSkillGroup); }

            set { SetValue(GetSkillGroup, value); }
        }

        private void StackPanel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (FontIcon.Glyph == "\xE011")
            {
                FontIcon.Glyph = "\xE010";
                ListBox_MasterSkill.Visibility = Visibility.Visible;
            }
            else
            {
                FontIcon.Glyph = "\xE011";
                ListBox_MasterSkill.Visibility = Visibility.Collapsed;
            }
        }
    }
}
