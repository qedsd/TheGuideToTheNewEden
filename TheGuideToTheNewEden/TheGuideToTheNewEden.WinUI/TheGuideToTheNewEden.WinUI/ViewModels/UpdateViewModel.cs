using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Services;
using static TheGuideToTheNewEden.WinUI.Services.AppUpdateService;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class UpdateViewModel : BaseViewModel
    {
        private CancellationTokenSource _cancellationTokenSource;

        private bool _checked;
        public bool Checked { get => _checked; set => SetProperty(ref _checked, value); }

        private bool _isLast;
        public bool IsLast{ get => _isLast; set => SetProperty(ref _isLast, value); }

        private bool _downloading;
        public bool Downloading { get => _downloading; set => SetProperty(ref _downloading, value); }

        private bool _downloaded;
        public bool Downloaded { get => _downloaded; set => SetProperty(ref _downloaded, value); }

        private double _downloadedSize;
        public double DownloadedSize { get => _downloadedSize; set => SetProperty(ref _downloadedSize, value); }

        private double _downloadSize;
        public double DownloadSize { get => _downloadSize; set => SetProperty(ref _downloadSize, value); }

        private AppRelease _lastRelease;
        public AppRelease LastRelease { get => _lastRelease; set => SetProperty(ref _lastRelease, value); }

        private List<AppRelease> _releases;
        public List<AppRelease> Releases { get => _releases; set => SetProperty(ref _releases, value); }

        public UpdateViewModel()
        {
            ClientServiceHelper.GetRequiredService<AppUpdateService>().GetReleasesStatus(out var releases, out var lastRelease, out var isLatest);
            Releases = releases;
            LastRelease = lastRelease;
            IsLast = isLatest;
            if(Releases == null)
            {
                UpdateStatus();
            }
            else
            {
                Checked = true;
            }
        }
        private async void UpdateStatus()
        {
            ShowWaiting();
            var failedMsg = await ClientServiceHelper.GetRequiredService<AppUpdateService>().UpdateReleasesStatusAsync();
            HideWaiting();
            if (!string.IsNullOrEmpty(failedMsg))
            {
                ShowError(failedMsg, false);
                Checked = false;
            }
            else
            {
                ClientServiceHelper.GetRequiredService<AppUpdateService>().GetReleasesStatus(out var releases, out var lastRelease, out var isLatest);
                Releases = releases;
                LastRelease = lastRelease;
                IsLast = isLatest;
                Checked = true;
            }
        }
        public ICommand UpdateStatusCommand => new RelayCommand(() =>
        {
            UpdateStatus();
        });

        public ICommand StartDownloadCommand => new RelayCommand(async() =>
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Downloading = true;
            string failedMsg = await ClientServiceHelper.GetRequiredService<AppUpdateService>().StartDownload(DownloadCallback, _cancellationTokenSource.Token);
            Downloading = false;
            if (string.IsNullOrEmpty(failedMsg))
            {
                Downloaded = true;
            }
            else
            {
                ShowError($"Failed to download: {failedMsg}");
            }
        });
        public ICommand StopDownloadCommand => new RelayCommand(() =>
        {
            _cancellationTokenSource.Cancel();
            Downloading = false;
        });
        public ICommand StartInstallCommand => new RelayCommand(async() =>
        {
            DevWinUI.WindowedContentDialog dialog = new()
            {
                Title = Helpers.ResourcesHelper.GetString("Update_InstallUpdate"),
                Content = Helpers.ResourcesHelper.GetString("Update_InstallUpdateTip"),
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_OK"),
                CloseButtonText = Helpers.ResourcesHelper.GetString("General_Cancel"),
                IsSecondaryButtonEnabled = false,
            };
            if(await dialog.ShowAsync(true) == ContentDialogResult.Primary)
            {
                ClientServiceHelper.GetRequiredService<AppUpdateService>().StartInstall();
                App.Close();
            }
        });

        public void DownloadCallback(double downloaded, double total)
        {
            Window?.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                DownloadedSize = downloaded / 1024f / 1024f;
                DownloadSize = total / 1024f / 1024f;
            });
        }
    }
}
