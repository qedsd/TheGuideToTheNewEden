using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class CharacterNavigationService
    {
        private Type _homeType;
        private Type _contentType;
        private Frame _frame;
        private object _currentParameter;
        private Dictionary<object, object> _instances = new Dictionary<object, object>();
        public void Init(Type homeType,Type contentType, Frame frame)
        {
            _homeType = homeType;
            _contentType = contentType;
            _frame = frame;
        }
        public void NavigateTo(object parameter)
        {
            if (_currentParameter != parameter)
            {
                _currentParameter = parameter;
                object instance = null;
                if(!_instances.TryGetValue(parameter, out instance))
                {
                    instance = Activator.CreateInstance(_contentType, parameter);
                    _instances.Add(parameter, instance);
                }
                _frame.Content = instance;
                Navigated?.Invoke(this, parameter);
            }
        }
        public void NavigateToHome()
        {
            _currentParameter = null;
            _frame.Navigate(_homeType);
        }

        public void RemoveInstance(object parameter)
        {
            if (_instances.Remove(parameter))
            {
                Removed?.Invoke(this, parameter);
            }
        }

        public event EventHandler<object> Navigated;
        public event EventHandler<object> Removed;
    }
}
