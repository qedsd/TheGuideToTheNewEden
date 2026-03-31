using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TheGuideToTheNewEden.Core.Models
{
    public class CheckableModel<T>:ObservableObject
    {
        private bool? _isChecked;
        public bool? IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }
        private T _data;
        public T Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        public CheckableModel() { }
        public CheckableModel(T data)
        {
            _data = data;
        }
        public CheckableModel(T data, bool? isChecked)
        {
            _data = data;
            IsChecked = isChecked;
        }
    }
}
