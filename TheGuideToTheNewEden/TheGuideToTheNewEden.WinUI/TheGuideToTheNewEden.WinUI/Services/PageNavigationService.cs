using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TheGuideToTheNewEden.WinUI.Controls;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.ViewModels;
using TheGuideToTheNewEden.WinUI.Views;
using TheGuideToTheNewEden.WinUI.Views.KB;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class PageNavigationService : IService
    {
        private Dictionary<string, Views.IPage> _pages;
        private FrameworkElement _navPanel;
        private Frame _frame;
        private LoadingControl _LoadingControl;
        private Dictionary<string, string> _loadingPages;
        private string _currentPage;
        private InfoBarControl _infoBarControl;
        private Action<Type> _navigateToCallback;
        public void Init()
        {
            _pages = new Dictionary<string, Views.IPage>();
            _loadingPages = new Dictionary<string, string>();
        }
        public void Init(FrameworkElement navPanel, Frame frame, LoadingControl loadingControl, InfoBarControl infoBarControl, Action<Type> navigateToCallback)
        {
            _navPanel = navPanel;
            _frame = frame;
            _LoadingControl = loadingControl;
            _infoBarControl = infoBarControl;
            _navigateToCallback = navigateToCallback;
        }

        public void NavigateTo(Type content, params object[] values)
        {
            if (content == null) return;
            _frame.Navigate(content, values);
            _currentPage = content.Name;
            if(_frame.Content is IPage page)
            {
                _pages.TryAdd(content.Name, page);
            }
            if (_loadingPages.TryGetValue(content.Name, out string loadingContent))//要切换显示的页面还处于加载中，需要还原状态
            {
                _LoadingControl.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                _LoadingControl.IsLoading = true;
                _LoadingControl.LoadingContent = loadingContent;
                _frame.IsEnabled = false;
            }
            else
            {
                _LoadingControl.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                _LoadingControl.IsLoading = false;
                _frame.IsEnabled = true;
            }
        }
        public void ShowWaiting(string page, string tip = null)
        {
            GetWindow().DispatcherQueue.SafelyTryEnqueue(() =>
            {
                _loadingPages.Remove(page);
                _loadingPages.Add(page, tip);
                if (_currentPage == page)
                {
                    _LoadingControl.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                    _LoadingControl.IsLoading = true;
                    _LoadingControl.LoadingContent = tip;
                    _frame.IsEnabled = false;
                }
            });
        }
        public void HideWaiting(string page)
        {
            GetWindow().DispatcherQueue.SafelyTryEnqueue(() =>
            {
                _loadingPages.Remove(page);
                if (_currentPage == page)
                {
                    _LoadingControl.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    _LoadingControl.IsLoading = false;
                    _frame.IsEnabled = true;
                }
            });
        }
        public void ShowWaiting(Page page, string tip = null)
        {
            string name = page.GetType().Name;
            if(_pageNameRef.TryGetValue(name, out var refName))
            {
                name = refName;
            }
            ShowWaiting(name, tip);
        }
        public void HideWaiting(Page page)
        {
            string name = page.GetType().Name;
            if (_pageNameRef.TryGetValue(name, out var refName))
            {
                name = refName;
            }
            HideWaiting(name);
        }
        public void ShowWaiting(BaseViewModel vm, string tip = null)
        {
            string name = vm.GetType().Name.Replace("ViewModel", "Page");
            if (_pageNameRef.TryGetValue(name, out var refName))
            {
                name = refName;
            }
            ShowWaiting(name, tip);
        }
        public void HideWaiting(BaseViewModel vm)
        {
            string name = vm.GetType().Name.Replace("ViewModel", "Page");
            if (_pageNameRef.TryGetValue(name, out var refName))
            {
                name = refName;
            }
            HideWaiting(name);
        }
        public void ShowMsg(string sender, string msg, InfoBarControl.InfoType infoType, bool autoClose, string title = null)
        {
            GetWindow().DispatcherQueue.SafelyTryEnqueue(() =>
            {
                _infoBarControl.Show(sender, msg, infoType, autoClose, title);
            });
        }
        public void ShowMsg(Page page, string msg, InfoBarControl.InfoType infoType, bool autoClose, string title = null)
        {
            string name = page.GetType().Name;
            if (_pageNameRef.TryGetValue(name, out var refName))
            {
                name = refName;
            }
            ShowMsg(name, msg, infoType, autoClose, title);
        }
        public void ShowMsg(BaseViewModel vm, string msg, InfoBarControl.InfoType infoType, bool autoClose, string title = null)
        {
            string name = vm.GetType().Name.Replace("ViewModel", "Page");
            if (_pageNameRef.TryGetValue(name, out var refName))
            {
                name = refName;
            }
            ShowMsg(name, msg, infoType, autoClose, title);
        }
        public void HideMsg()
        {
            GetWindow().DispatcherQueue.SafelyTryEnqueue(() =>
            {
                _infoBarControl.Hide();
            });
        }
        public double GetNavPanelWidth()
        {
            return _navPanel.ActualWidth;
        }

        public void NavigateToMarket(params object[] values)
        {
            Helpers.WindowHelper.MainWindow.Activate();
            NavigateTo(typeof(MarketPage), values);
            _navigateToCallback?.Invoke(typeof(MarketPage));
        }

        public void NavigateToUpdate()
        {
            NavigateTo(typeof(SettingPage), "Update");
            _navigateToCallback?.Invoke(typeof(SettingPage));
        }

        public void NavigateToAbout()
        {
            NavigateTo(typeof(SettingPage), "About");
            _navigateToCallback?.Invoke(typeof(SettingPage));
        }

        public void NavigateToZKB()
        {
            NavigateTo(typeof(ZKBHomePage));
            _navigateToCallback?.Invoke(typeof(ZKBHomePage));
        }

        public void Dispose()
        {
            GetWindow().DispatcherQueue.SafelyTryEnqueue(() =>
            {
                foreach (var page in _pages.Values)
                {
                    page.Close();
                }
            });
        }

        private Window GetWindow()
        {
            return Helpers.WindowHelper.MainWindow;
        }

        /// <summary>
        /// 将功能内的细分page名称映射到功能page名称
        /// </summary>
        private static Dictionary<string, string> _pageNameRef = new Dictionary<string, string>
        {
            {"CharactersPage","CharactersShellPage" },
            {"OverviewPage","CharactersShellPage"},
            {"SkillPage","CharactersShellPage"},
            {"ClonePage","CharactersShellPage"},
            {"WalletPage","CharactersShellPage"},
            {"MailPage","CharactersShellPage"},
            {"ContractPage","CharactersShellPage"},
            {"IndustryPage","CharactersShellPage"},
            {"ScalperPage","ScalperShellPage" },
            {"ShoppingCartPage","ScalperShellPage" },
            {"ShoppingRecordPage","ScalperShellPage" },
            {"KillStreamPage","ZKBHomePage" },
        };
    }
}
