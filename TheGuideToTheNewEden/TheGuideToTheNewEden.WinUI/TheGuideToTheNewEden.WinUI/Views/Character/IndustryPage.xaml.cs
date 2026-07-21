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
using TheGuideToTheNewEden.WinUI.Extensions;
using EVEStandard.Models.API;
using EVEStandard;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class IndustryPage : Page, ICharacterPage
    {
        private EVEStandardAPI _esiClient;
        private AuthDTO _auth;
        private Core.Models.Character.AuthorizedCharacterData _characterData;
        private bool _isLoaded = false;
        public IndustryPage()
        {
            this.InitializeComponent();
            Loaded += IndustryPage_Loaded;
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
            var paras = e.Parameter as object[];
            if (paras != null && paras.Length == 2)
            {
                _esiClient = paras[0] as EVEStandardAPI;
                _characterData = paras[1] as Core.Models.Character.AuthorizedCharacterData;
                _auth = _characterData.ToAuthDTO();
            }
        }
        public void Clear()
        {
            _isLoaded = false;
        }

        public async void Refresh()
        {
            this.ShowWaiting();
            var result = await _esiClient.Industry.ListCharacterIndustryJobsAsync(_auth, true);
            if (result?.Model != null)
            {
                if (result.Model.NotNullOrEmpty())
                {
                    List<IndustryJob> jobs = new List<IndustryJob>();
                    foreach (var item in result.Model)
                    {
                        var job = await CreateIndustryJob(item);
                        if (job != null)
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
            else
            {
                this.ShowError("ListCharacterIndustryJobsAsync Failed", true);
            }
            this.HideWaiting();
        }

        private async Task<IndustryJob> CreateIndustryJob(EVEStandard.Models.IndustryJob job)
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
                var sta = await Services.StructureService.QueryStructureAsync(job.StationId, _characterData.CharacterID);
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
            industryJob.Product = Core.Services.DB.InvTypeService.QueryType(job.ProductTypeId.Value);
            return industryJob;
        }
    }
}
