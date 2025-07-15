using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using ESI.NET.Models.PlanetaryInteraction;
using TheGuideToTheNewEden.WPF.Controls;
using TheGuideToTheNewEden.WPF.Interfaces;
using TheGuideToTheNewEden.WPF.Views;

namespace TheGuideToTheNewEden.WPF.Services
{
    public class NavigationService : IService
    {
        private Dictionary<string, Views.IPage> _pages;
        private Frame _frame;
        private LoadingControl _LoadingControl;
        private Dictionary<string, string> _loadingPages;
        private string _currentPage;
        private InfoBarControl _infoBarControl;
        public void Init()
        {
            _pages = new Dictionary<string, Views.IPage>();
            _loadingPages = new Dictionary<string, string>();
        }
        public void Init(Frame frame, LoadingControl loadingControl, InfoBarControl infoBarControl)
        {
            _frame = frame;
            _LoadingControl = loadingControl;
            _infoBarControl = infoBarControl;
        }

        public void NavigateTo(Type content, params object[] values)
        {
            if (content == null) return;
            if (!_pages.TryGetValue(content.Name, out IPage contentPage))
            {
                contentPage = Activator.CreateInstance(content) as IPage;
                contentPage.Init();
                _pages.Add(content.Name, contentPage);
            }
            _frame.Navigate(contentPage, values);
            _currentPage = content.Name;

            if (_loadingPages.TryGetValue(content.Name, out string loadingContent))//要切换显示的页面还处于加载中，需要还原状态
            {
                _LoadingControl.Visibility = System.Windows.Visibility.Visible;
                _LoadingControl.IsLoading = true;
                _LoadingControl.LoadingContent = loadingContent;
                _frame.IsEnabled = false;
            }
            else
            {
                _LoadingControl.Visibility = System.Windows.Visibility.Collapsed;
                _LoadingControl.IsLoading = false;
                _frame.IsEnabled = true;
            }
        }
        public void ShowWaiting(string page, string tip = null)
        {
            _loadingPages.Remove(page);
            _loadingPages.Add(page, tip);
            if(_currentPage == page)
            {
                _LoadingControl.Visibility = System.Windows.Visibility.Visible;
                _LoadingControl.IsLoading = true;
                _LoadingControl.LoadingContent = tip;
                _frame.IsEnabled = false;
            }
        }
        public void HideWaiting(string page)
        {
            _loadingPages.Remove(page);
            if (_currentPage == page)
            {
                _LoadingControl.Visibility = System.Windows.Visibility.Collapsed;
                _LoadingControl.IsLoading = false;
                _frame.IsEnabled = true;
            }
        }
        public void ShowMsg(string sender, string msg, InfoBarControl.InfoType infoType,bool autoClose, string title = null)
        {
            _infoBarControl.Show(sender, msg, infoType, autoClose, title);
        }
        public void HideMsg()
        {
            _infoBarControl.Hide();
        }


        public void Dispose()
        {
            foreach (var page in _pages.Values)
            {
                page.Close();
            }
        }
    }
}
