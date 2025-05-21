using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.PlanetResources;
using TheGuideToTheNewEden.Core.Services;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal class ESIScopeService
    {
        private static ESIScopeService current;
        public static ESIScopeService Current
        {
            get
            {
                if (current == null)
                    current = new ESIScopeService();
                return current;
            }
        }

        private static readonly string SourceFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "ESIScopes.txt");
        private static readonly string SelectedFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "ESIScopes.txt");

        private List<string> _allScopes;
        public List<string> GetAllScopes()
        {
            if (_allScopes == null)
            {
                _allScopes = new List<string>();
                var lines = System.IO.File.ReadAllLines(SourceFilePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    _allScopes.Add(lines[i]);
                }
            }
            return _allScopes;
        }

        public List<string> GetSelectedScopes()
        {
            if (File.Exists(SelectedFilePath))
            {
                return File.ReadAllLines(SelectedFilePath).ToList();
            }
            else
            {
                return GetAllScopes();
            }
        }

        public void SelectScope(string scope)
        {
            var selected = GetSelectedScopes();
            if (!selected.Contains(scope))
            {
                selected.Add(scope);
                File.WriteAllLines(SelectedFilePath, selected);
            }
        }

        public void SelectAllScope()
        {
            File.WriteAllLines(SelectedFilePath, GetAllScopes());
        }

        public void CancelSelectScope(string scope)
        {
            var selected = GetSelectedScopes();
            if (selected.Contains(scope))
            {
                selected.Remove(scope);
                File.WriteAllLines(SelectedFilePath, selected);
            }
        }

        public void CancelSelectAllScope()
        {
            File.WriteAllText(SelectedFilePath, string.Empty);
        }
    }
}
