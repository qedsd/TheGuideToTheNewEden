using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WinUI.ViewModels.Setting
{
    internal class ESIScopeSettingViewModel : BaseViewModel
    {
        private List<ESIScopeItem> _scopes;
        public List<ESIScopeItem> Scopes
        {
            get => _scopes;
            set => SetProperty(ref _scopes, value);
        }

        private bool _selectAll;
        public bool SelectAll
        {
            get => _selectAll;
            set 
            {
                if(SetProperty(ref _selectAll, value))
                {
                    SelectAllCommand.Execute(value);
                }
            } 
        }
        public ESIScopeSettingViewModel()
        {
            var all = Services.Settings.ESIScopeService.Current.GetAllScopes();
            var selected = Services.Settings.ESIScopeService.Current.GetSelectedScopes().ToHashSet();
            var scopeItems = new List<ESIScopeItem>();
            foreach (var scope in all)
            {
                ESIScopeItem item = new ESIScopeItem()
                {
                    Scope = scope,
                    Selected = selected.Contains(scope)
                };
                item.PropertyChanged += Scope_PropertyChanged;
                scopeItems.Add(item);
            }
            Scopes = scopeItems;
        }

        private bool _autoSave = true;
        private void Scope_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(_autoSave && e.PropertyName == nameof(ESIScopeItem.Selected))
            {
                var scope = sender as ESIScopeItem;
                if (scope.Selected)
                {
                    Services.Settings.ESIScopeService.Current.SelectScope(scope.Scope);
                }
                else
                {
                    Services.Settings.ESIScopeService.Current.CancelSelectScope(scope.Scope);
                }
            }
        }

        public ICommand SelectCommand => new RelayCommand<ESIScopeItem>((scope) =>
        {
            if(scope != null)
            {
                if (scope.Selected)
                {
                    Services.Settings.ESIScopeService.Current.SelectScope(scope.Scope);
                }
                else
                {
                    Services.Settings.ESIScopeService.Current.CancelSelectScope(scope.Scope);
                }
            }
        });

        public ICommand SelectAllCommand => new RelayCommand<bool>((isSelect) =>
        {
            _autoSave = false;
            if (isSelect)
            {
                Services.Settings.ESIScopeService.Current.SelectAllScope();
            }
            else
            {
                Services.Settings.ESIScopeService.Current.CancelSelectAllScope();
            }
            foreach (var scope in Scopes)
            {
                scope.Selected = isSelect;
            }
            _autoSave = true;
        });
    }
}
