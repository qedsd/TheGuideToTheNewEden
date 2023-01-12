using System;

using Microsoft.Toolkit.Mvvm.ComponentModel;
using TheGuideToTheNewEden.UWP.Helpers;
using Windows.ApplicationModel;

namespace TheGuideToTheNewEden.UWP.ViewModels
{
    public class HomeViewModel : ObservableObject
    {

        private string _versionDescription;

        public string VersionDescription
        {
            get { return _versionDescription; }

            set { SetProperty(ref _versionDescription, value); }
        }
        public HomeViewModel()
        {
            VersionDescription = GetVersionDescription();
        }
        private string GetVersionDescription()
        {
            var package = Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;

            return $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
