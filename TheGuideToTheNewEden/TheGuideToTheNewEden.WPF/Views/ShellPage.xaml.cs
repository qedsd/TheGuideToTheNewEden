using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TheGuideToTheNewEden.WPF.Models;
using TheGuideToTheNewEden.WPF.Views.NavigationViews;

namespace TheGuideToTheNewEden.WPF.Views
{
    public partial class ShellPage : UserControl, IDisposable
    {
        private List<NavigationViewItem> _navigationViewItems;
        public ShellPage()
        {
            InitMenu();
            Initialized += ShellPage_Initialized;
            InitializeComponent();
        }
        private void InitMenu()
        {
            _navigationViewItems = new List<NavigationViewItem>();
            #region 自动获取Page，但顺序不可控
            //var classes = Assembly.GetExecutingAssembly()
            //         .GetTypes()
            //         .Where(t => t.Namespace == "TheGuideToTheNewEden.WPF.Views.NavigationViews" &&
            //                    t.IsClass &&
            //                    !t.IsAbstract)
            //         .ToList();

            //foreach (var t in classes)
            //{
            //    _navigationViewItems.Add(new NavigationViewItem()
            //    {
            //        Type = t,
            //        Title = Helpers.ResourcesHelper.GetString($"Navigation.{t.Name}"),
            //    });
            //}
            #endregion

            _navigationViewItems.Add(new NavigationViewItem(typeof(HomePage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(GamePreviewMgrPage)));
        }

        private void ShellPage_Initialized(object sender, EventArgs e)
        {
            ClientServiceHelper.GetRequiredService<Services.NavigationService>().Init(ContentFrame);
            MenuList.ItemsSource = _navigationViewItems;
        }

        public void Dispose()
        {
            ClientServiceHelper.GetRequiredService<Services.NavigationService>()?.Dispose();
        }

        private void MenuList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = MenuList.SelectedItem as NavigationViewItem;
            if (item != null)
            {
                ClientServiceHelper.GetRequiredService<Services.NavigationService>().NavigateTo(item.Type);
            }
        }
    }
}
