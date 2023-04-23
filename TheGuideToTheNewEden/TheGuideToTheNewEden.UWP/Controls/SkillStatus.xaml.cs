using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
    public sealed partial class SkillStatus : UserControl
    {
        static SolidColorBrush defaultBrush = new SolidColorBrush()
        {
            Color = Windows.UI.Colors.LightGray
        };
        static SolidColorBrush activeBrush = new SolidColorBrush()
        {
            Color = Windows.UI.Colors.White
        };
        static SolidColorBrush inactiveBrush = new SolidColorBrush()
        {
            Color = Windows.UI.Colors.MediumSeaGreen
        };
        public SkillStatus()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty GetActiveSkillLevel = DependencyProperty.Register
            (
            //name    要注册的依赖项对象的名称
            "ActiveSkillLevel",
            //propertyType    该属性的类型，作为类型参考
            typeof(int),
            //ownerType    正在注册依赖项属性的所有者类型，作为类型参考
            typeof(UserControl),
            //defaultMetadata    属性元数据实例。这可以包含一个 PropertyChangedCallback 实现引用。
            new PropertyMetadata(0, new PropertyChangedCallback(SetActiveSkillLevel))
            );

        private static void SetActiveSkillLevel(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SkillStatus skillStatus = (SkillStatus)d;
            switch ((int)e.NewValue)
            {
                case 0:
                    {
                    }
                    break;
                case 1:
                    {
                        skillStatus.Rectangle1.Fill = activeBrush;
                    }
                    break;
                case 2:
                    {
                        skillStatus.Rectangle1.Fill = activeBrush;
                        skillStatus.Rectangle2.Fill = activeBrush;
                    }
                    break;
                case 3:
                    {
                        skillStatus.Rectangle1.Fill = activeBrush;
                        skillStatus.Rectangle2.Fill = activeBrush;
                        skillStatus.Rectangle3.Fill = activeBrush;
                    }
                    break;
                case 4:
                    {
                        skillStatus.Rectangle1.Fill = activeBrush;
                        skillStatus.Rectangle2.Fill = activeBrush;
                        skillStatus.Rectangle3.Fill = activeBrush;
                        skillStatus.Rectangle4.Fill = activeBrush;
                    }
                    break;
                case 5:
                    {
                        skillStatus.Rectangle1.Fill = activeBrush;
                        skillStatus.Rectangle2.Fill = activeBrush;
                        skillStatus.Rectangle3.Fill = activeBrush;
                        skillStatus.Rectangle4.Fill = activeBrush;
                        skillStatus.Rectangle5.Fill = activeBrush;
                    }
                    break;
            }
        }

        public static readonly DependencyProperty GetTrainedSkillLevel = DependencyProperty.Register
            (
            //name    要注册的依赖项对象的名称
            "TrainedSkillLevel",
            //propertyType    该属性的类型，作为类型参考
            typeof(int),
            //ownerType    正在注册依赖项属性的所有者类型，作为类型参考
            typeof(UserControl),
            //defaultMetadata    属性元数据实例。这可以包含一个 PropertyChangedCallback 实现引用。
            new PropertyMetadata(0, new PropertyChangedCallback(SetTrainedSkillLevel))
            );

        private static void SetTrainedSkillLevel(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SkillStatus skillStatus = (SkillStatus)d;
            switch ((int)e.NewValue)
            {
                case 0:
                    {
                        skillStatus.Rectangle1.Fill = defaultBrush;
                        skillStatus.Rectangle2.Fill = defaultBrush;
                        skillStatus.Rectangle3.Fill = defaultBrush;
                        skillStatus.Rectangle4.Fill = defaultBrush;
                        skillStatus.Rectangle5.Fill = defaultBrush;
                    }
                    break;
                case 1:
                    {
                        skillStatus.Rectangle1.Fill = inactiveBrush;
                        skillStatus.Rectangle2.Fill = defaultBrush;
                        skillStatus.Rectangle3.Fill = defaultBrush;
                        skillStatus.Rectangle4.Fill = defaultBrush;
                        skillStatus.Rectangle5.Fill = defaultBrush;
                    }
                    break;
                case 2:
                    {
                        skillStatus.Rectangle1.Fill = inactiveBrush;
                        skillStatus.Rectangle2.Fill = inactiveBrush;
                        skillStatus.Rectangle3.Fill = defaultBrush;
                        skillStatus.Rectangle4.Fill = defaultBrush;
                        skillStatus.Rectangle5.Fill = defaultBrush;
                    }
                    break;
                case 3:
                    {
                        skillStatus.Rectangle1.Fill = inactiveBrush;
                        skillStatus.Rectangle2.Fill = inactiveBrush;
                        skillStatus.Rectangle3.Fill = inactiveBrush;
                        skillStatus.Rectangle4.Fill = defaultBrush;
                        skillStatus.Rectangle5.Fill = defaultBrush;
                    }
                    break;
                case 4:
                    {
                        skillStatus.Rectangle1.Fill = inactiveBrush;
                        skillStatus.Rectangle2.Fill = inactiveBrush;
                        skillStatus.Rectangle3.Fill = inactiveBrush;
                        skillStatus.Rectangle4.Fill = inactiveBrush;
                        skillStatus.Rectangle5.Fill = defaultBrush;
                    }
                    break;
                case 5:
                    {
                        skillStatus.Rectangle1.Fill = inactiveBrush;
                        skillStatus.Rectangle2.Fill = inactiveBrush;
                        skillStatus.Rectangle3.Fill = inactiveBrush;
                        skillStatus.Rectangle4.Fill = inactiveBrush;
                        skillStatus.Rectangle5.Fill = inactiveBrush;
                    }
                    break;
            }
        }

        public int TrainedSkillLevel
        {
            get { return (int)GetValue(GetTrainedSkillLevel); }

            set { SetValue(GetTrainedSkillLevel, value); }
        }
        public int ActiveSkillLevel
        {
            get { return (int)GetValue(GetActiveSkillLevel); }

            set { SetValue(GetActiveSkillLevel, value); }
        }

    }
}
