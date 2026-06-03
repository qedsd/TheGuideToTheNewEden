using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using TheGuideToTheNewEden.WinUI.ViewModels;
using TheGuideToTheNewEden.WinUI.Views.Character;
using TheGuideToTheNewEden.WinUI.Views.Map;
using TheGuideToTheNewEden.WinUI.Views.Tools;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class MainPage : UserControl
    {
        private string _version;
        private readonly string _nameSpace;
        public MainPage()
        {
            _nameSpace = this.GetType().Namespace;
            Loaded += MainPage_Loaded;
            this.InitializeComponent();
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainPage_Loaded;
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().Init(NavPanel,ContentFrame, Loading, InfoBar, SelecteFromNavigateTo);
            HomeNavigationViewItem.IsSelected = true;
            _version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            if (AutoUpdateService.Value)
            {
                CheckUpdate();
            }
            var settingItem = MenuList.SettingsItem as NavigationViewItem;//使用本地化语言显示Setting项
            if (settingItem != null)
            {
                settingItem.Content = Helpers.ResourcesHelper.GetString("General_Setting");
            }
        }

        public void Dispose()
        {
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>()?.Dispose();
        }

        #region 更新
        private async void CheckUpdate()
        {
            
        }
        #endregion

        #region 系统托盘
        private ICommand ShowCommand => new RelayCommand(() =>
        {
            Helpers.WindowHelper.MainWindow.Activate();
        });
        private ICommand ExitCommand => new RelayCommand(() =>
        {
            try
            {
                TaskbarIcon.Dispose();
            }
            catch { }
            App.Close();
        });
        #endregion

        private async void ShowTestInfo()
        {
            ContentDialog contentDialog = new ContentDialog();
            contentDialog.Title = Helpers.ResourcesHelper.GetString("TestVersionInfo_Title");
            contentDialog.Content = new TextBlock()
            {
                Text = Helpers.ResourcesHelper.GetString("TestVersionInfo_Desc"),
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            };
            contentDialog.XamlRoot = WindowHelper.GetWindowForElement(this).Content.XamlRoot;
            contentDialog.CloseButtonText = Helpers.ResourcesHelper.GetString("General_OK");
            contentDialog.IsPrimaryButtonEnabled = false;
            await contentDialog.ShowAsync();
        }

        public void SelecteFromNavigateTo(Type type)
        {
            if(type == typeof(SettingPage))
            {
                MenuList.SelectedItem = MenuList.SettingsItem;
            }
            string targetFullName = type.FullName;
            Microsoft.UI.Xaml.Controls.NavigationViewItem foundTargetItem(IList<object> items)
            {
                foreach (var menu in items)
                {
                    var item = menu as Microsoft.UI.Xaml.Controls.NavigationViewItem;
                    if (item.MenuItems.Count > 0)
                    {
                        var target = foundTargetItem(item.MenuItems);
                        if (target != null)
                        {
                            return target;
                        }
                    }
                    else if(item.Tag != null)
                    {
                        if(GetFullNameOfTag(item.Tag.ToString()) == targetFullName)
                        {
                            return item;
                        }
                    }
                }
                return null;
            }
            var targetItem = foundTargetItem(MenuList.MenuItems);
            if (targetItem != null)
            {
                targetItem.IsSelected = true;
                MenuList.SelectedItem = targetItem;
            }
        }
        private string GetFullNameOfTag(string tag)
        {
            return $"{_nameSpace}.{tag}";
        }
        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().NavigateTo(typeof(SettingPage));
            }
            else
            {
                var item = args.SelectedItem as Microsoft.UI.Xaml.Controls.NavigationViewItem;
                if(item?.Tag != null)
                {
                    string tag = item.Tag.ToString();
                    if (!string.IsNullOrEmpty(tag))
                    {
                        string fullName = $"{this.GetType().Namespace}.{tag}";
                        var type = Type.GetType(fullName);
                        if (type != null)
                        {
                            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().NavigateTo(type);
                        }
                        else
                        {
                            fullName = $"{this.GetType().Namespace}.Tools.{tag}";
                            type = Type.GetType(fullName);
                            if (type != null)
                            {
                                var instance = Activator.CreateInstance(type);
                                int windowW = 500;
                                int windowH = 200;
                                if(instance is ITool tool)
                                {
                                    tool.GetWindowSize(out windowW, out windowH);
                                }
                                string content = item.Content.ToString();
                                ToolWindow toolWindow = new ToolWindow(content, content, instance as UIElement, WindowTitleStyle.MiniAndClose, true, true, true, true, true, windowW, windowH);
                                toolWindow.LogPositionAndSize(content);
                                toolWindow.Activate();
                                sender.SelectedItem = null;
                            }
                            else
                            {
                                ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowMsg("MainPage", $"Unknown type: {fullName}", Controls.InfoBarControl.InfoType.Error, true);
                                return;
                            }
                        }
                        if(item.ContextFlyout == null)
                        {
                            MenuFlyout menuFlyout = new MenuFlyout();
                            MenuFlyoutItem resetMenu = new MenuFlyoutItem();
                            var resourceValue = Application.Current.Resources["General_Reset"] as string;
                            resetMenu.Text = resourceValue;
                            resetMenu.Tag = type;
                            resetMenu.Click += ResetMenu_Click;
                            menuFlyout.Items.Add(resetMenu);
                            item.ContextFlyout = menuFlyout;
                        }
                    }
                }
            }
        }

        private void ResetMenu_Click(object sender, RoutedEventArgs e)
        {
            var pageType = (sender as MenuFlyoutItem).Tag as Type;
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ResetPage(pageType);
        }

        private void LogoButton_Click(object sender, RoutedEventArgs e)
        {
            MenuList.IsPaneOpen = !MenuList.IsPaneOpen;
            NavPanel.Width = MenuList.IsPaneOpen ? MenuList.OpenPaneLength : MenuList.CompactPaneLength;
        }
    }
}
