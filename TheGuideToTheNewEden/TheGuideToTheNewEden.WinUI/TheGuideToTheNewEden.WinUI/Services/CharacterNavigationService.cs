using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using TheGuideToTheNewEden.WinUI.ViewModels;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class CharacterNavigationService
    {
        private Type _homeType;
        private Type _contentType;
        private TabView _tabView;
        private TabViewItem _homeTabViewItem;

        /// <summary>
        /// key = CharacterViewModel
        /// value = TabViewItem
        /// </summary>
        private Dictionary<object, TabViewItem> _instances = new Dictionary<object, TabViewItem>();
        public void Init(Type homeType,Type contentType, TabView tabView)
        {
            _homeType = homeType;
            _contentType = contentType;
            _tabView = tabView;
        }
        public void NavigateTo(object parameter)
        {
            TabViewItem instance = null;
            if (!_instances.TryGetValue(parameter, out instance))
            {
                var contentPage = Activator.CreateInstance(_contentType, parameter);
                instance = new TabViewItem()
                {
                    Header = (parameter as CharacterViewModel)?.SelectedCharacter.CharacterName,
                    Content = contentPage,
                    IsSelected = true,
                    IsClosable = true,
                    Tag = parameter
                };
                _tabView.TabItems.Add(instance);
                _instances.Add(parameter, instance);
            }
            instance.IsSelected = true;
            Navigated?.Invoke(this, parameter);
        }
        public void NavigateToHome()
        {
            if(_homeTabViewItem == null)
            {
                _homeTabViewItem = new TabViewItem()
                {
                    Header = Helpers.ResourcesHelper.GetString("CharacterPage_All"),
                    Content = Activator.CreateInstance(_homeType),
                    IsSelected = true,
                    IsClosable = false,
                };
                _tabView.TabItems.Add(_homeTabViewItem);
            }
            _homeTabViewItem.IsSelected = true;
        }

        public void RemoveInstance(object parameter)
        {
            if(_instances.TryGetValue(parameter, out var instance))
            {
                _tabView.TabItems?.Remove(instance);
                _instances.Remove(parameter);
                Removed?.Invoke(this, parameter);
            }
        }

        public void Reset()
        {
            foreach(var instance in _instances)
            {
                _tabView.TabItems?.Remove(instance);
            }
            _instances.Clear();
            _homeTabViewItem = null;
            _homeType = null;
            _contentType = null;
            _tabView = null;
        }

        public event EventHandler<object> Navigated;
        public event EventHandler<object> Removed;
    }
}
