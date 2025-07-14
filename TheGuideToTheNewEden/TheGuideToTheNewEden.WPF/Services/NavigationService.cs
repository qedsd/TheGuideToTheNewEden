using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Interfaces;
using TheGuideToTheNewEden.WPF.Views;

namespace TheGuideToTheNewEden.WPF.Services
{
    public class NavigationService : IService
    {
        private Dictionary<string, Views.IPage> _pages;
        private Frame _frame;
        public void Init()
        {
            _pages = new Dictionary<string, Views.IPage>();
        }
        public void Init(Frame frame)
        {
            _frame = frame;
        }

        public void NavigateTo(Type content, params object[] values)
        {
            if (!_pages.TryGetValue(content.FullName, out IPage contentPage))
            {
                contentPage = Activator.CreateInstance(content) as IPage;
                contentPage.Init();
                _pages.Add(content.FullName, contentPage);
            }
            _frame.Navigate(contentPage, values);
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
