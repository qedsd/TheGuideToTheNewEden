using ESI.NET;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Indusrty;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class IndustryPage : Page, ICharacterPage
    {
        private BaseWindow _window;
        private EsiClient _esiClient;
        private bool _isLoaded = false;
        public IndustryPage()
        {
            this.InitializeComponent();
            Loaded += IndustryPage_Loaded;
            Loaded += IndustryPage_Loaded1;
        }

        private void IndustryPage_Loaded1(object sender, RoutedEventArgs e)
        {
            Loaded -= IndustryPage_Loaded1;
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }

        private void IndustryPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded)
            {
                Refresh();
                _isLoaded = true;
            }
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _esiClient = e.Parameter as EsiClient;
        }
        public void Clear()
        {
            _isLoaded = false;
        }

        public async void Refresh()
        {
            _window?.ShowWaiting();
            var result = await _esiClient.Industry.JobsForCharacter();
            if (result.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _window?.HideWaiting();
                _window?.ShowError(result.Message);
                Core.Log.Error(result.Message);
            }
            else
            {
                if(result.Data.NotNullOrEmpty())
                {
                    var jobs = await Task.Run(List<IndustryJob> () =>
                    {
                        List<IndustryJob> jobs = new List<IndustryJob>();
                        foreach (var job in result.Data)
                        {
                            jobs.Add(new IndustryJob(job));
                        }
                        return jobs;
                    });

                }
            }
            _window?.HideWaiting();
        }
    }
}
