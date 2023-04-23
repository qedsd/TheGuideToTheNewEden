using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models.Clone;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace TheGuideToTheNewEden.UWP.Controls
{
    public sealed partial class CloneControl : UserControl
    {
        public CloneControl()
        {
            this.InitializeComponent();
        }
        private void StackPanel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (FontIcon.Glyph == "\xE011")
            {
                FontIcon.Glyph = "\xE010";
                ListBox_Implants.Visibility = Visibility.Visible;
            }
            else
            {
                FontIcon.Glyph = "\xE011";
                ListBox_Implants.Visibility = Visibility.Collapsed;
            }
        }

        private async void ListBox_Implants_Tapped(object sender, TappedRoutedEventArgs e)
        {
            //CloneImplant cloneImplant = ((ListBox)sender).SelectedItem as CloneImplant;
            //if (cloneImplant == null)
            //    return;
            //// 创建一个 CoreApplicationView，即新的应用视图。
            //var applicationView = CoreApplication.CreateNewView();

            //// 一个应用视图有自己的 Id，稍后我们创建应用视图的时候，需要记录这个 Id。
            //int newViewId = 0;

            //// 使用新应用视图的 CoreDispatcher 线程调度模型来执行新视图中的操作。
            //await applicationView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            //{
            //    // 在新的应用视图中，我们将新的窗口内容设置为 Killmail_detail 页面。
            //    Frame frame = new Frame();
            //    //if((Application.Current as App).Market_Page)
            //    frame.Navigate(typeof(Market_Page), cloneImplant.Id.ToString());
            //    //frame.Navigate(typeof(Market_Page));
            //    //frame.Navigate(typeof(LpToMarketTransition), typeId);
            //    Window.Current.Content = frame;
            //    Window.Current.Activate();

            //    // 记录新应用视图的 Id，这样才能稍后切换。
            //    newViewId = ApplicationView.GetForCurrentView().Id;
            //});

            //// 使用刚刚记录的新应用视图 Id 显示新的应用视图。
            //var viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
        }

        public static readonly DependencyProperty GetJumpClone = DependencyProperty.Register
            (
            //name    要注册的依赖项对象的名称
            "JumpClone",
            //propertyType    该属性的类型，作为类型参考
            typeof(JumpClone),
            //ownerType    正在注册依赖项属性的所有者类型，作为类型参考
            typeof(UserControl),
            //defaultMetadata    属性元数据实例。这可以包含一个 PropertyChangedCallback 实现引用。
            new PropertyMetadata(0, new PropertyChangedCallback(SetJumpClone))
            );

        private static void SetJumpClone(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CloneControl cloneControl = (CloneControl)d;
            JumpClone jumpClone = e.NewValue as JumpClone;
            cloneControl.TextBlock_CloneName.Text = jumpClone.Name == null ? "未命名" : jumpClone.Name;
            cloneControl.TextBlock_LocationName.Text = jumpClone.Location_Name == null ? "未命名" : jumpClone.Location_Name;
            cloneControl.TextBlock_ImplantCount.Text = jumpClone.Implants == null ? "0" : jumpClone.Implants.Count.ToString();
            cloneControl.ListBox_Implants.ItemsSource = jumpClone.CloneImplant;
        }

        public JumpClone JumpClone
        {
            get { return (JumpClone)GetValue(GetJumpClone); }

            set { SetValue(GetJumpClone, value); }
        }
    }
}
