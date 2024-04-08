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
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models;
using ESI.NET.Models.Industry;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class IndustryPage : Page, ICharacterPage
    {
        private BaseWindow _window;
        private EsiClient _esiClient;
        private int _characterId;
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
            var array = e.Parameter as dynamic[];
            _esiClient = array[0];
            _characterId = array[1];
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
                    List<IndustryJob> jobs = new List<IndustryJob>();
                    foreach(var item in result.Data)
                    {
                        var job = await CreateIndustryJob(item);
                        if(job != null)
                        {
                            job.StatusDesc = Helpers.ResourcesHelper.GetString($"IndustryPage_Status_{job.Status.ToLower()}");
                            if (job.Status == "active" && job.EndDate <= DateTime.UtcNow)
                            {
                                job.StatusDesc = Helpers.ResourcesHelper.GetString("IndustryPage_Status_Done");
                            }
                            jobs.Add(job);
                        }
                    }
                    DataGrid.ItemsSource = jobs;
                }
            }
            _window?.HideWaiting();
        }

        private async Task<IndustryJob> CreateIndustryJob(ESI.NET.Models.Industry.Job job)
        {
            IndustryJob industryJob = new IndustryJob(job);
            if (job.StationId < 70000000)//¿Õ¼äÕ¾
            {
                var sta = Core.Services.DB.StaStationService.Query((int)job.StationId);
                if (sta != null)
                {
                    industryJob.Location = new IdNameLong(sta.StationID, sta.StationName, IdName.CategoryEnum.Station);
                }
                else
                {
                    industryJob.Location = new IdNameLong(job.StationId, job.StationId.ToString(), IdName.CategoryEnum.Station);
                }
            }
            else
            {
                var sta = await Services.StructureService.QueryStructureAsync(job.StationId, _characterId);
                if (sta != null)
                {
                    industryJob.Location = new IdNameLong(sta.Id, sta.Name, IdName.CategoryEnum.Structure);
                }
                else
                {
                    industryJob.Location = new IdNameLong(job.StationId, job.StationId.ToString(), IdName.CategoryEnum.Structure);
                }
            }
            industryJob.Blueprint = Core.Services.DB.InvTypeService.QueryType(job.BlueprintTypeId);
            industryJob.Product = Core.Services.DB.InvTypeService.QueryType(job.ProductTypeId);
            return industryJob;
        }
    }
}
