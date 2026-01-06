using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using TheGuideToTheNewEden.SDEBuilder.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;


namespace TheGuideToTheNewEden.SDEBuilder.WinUI
{
    public sealed partial class MainWindow : Window
    {
        private DateTime _releaseDate;
        public MainWindow()
        {
            this.InitializeComponent();
            AppWindow.Resize(new Windows.Graphics.SizeInt32(840, 800));
            SettingService.Init();
            string sdeFolder = SettingService.GetValue(SettingService.SDEFolderKey);
            SDEFolder.Text = sdeFolder;
            if (!string.IsNullOrEmpty(sdeFolder))
            {
                SaveDB.Text = $"SDE_{DateTime.Now.ToString("yyyyMMdd")}.db";
                SettingGrid.Loaded += SettingGrid_Loaded;
            }
        }

        private void SettingGrid_Loaded(object sender, RoutedEventArgs e)
        {
            SettingGrid.Loaded -= SettingGrid_Loaded;
            LoadSDEFiles(SDEFolder.Text);
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            LanguageEnum language = (LanguageEnum)LangComboBox.SelectedIndex;
            var items = FilesListView.ItemsSource as List<SDEFile>;
            if (items != null && items.Count > 0) 
            {
                try
                {
                    if (File.Exists(SaveDB.Text))
                    {
                        ContentDialog contentDialog = new ContentDialog()
                        {
                            XamlRoot = this.Content.XamlRoot,
                            Title = "Existed File",
                            Content = $"File {SaveDB.Text} already exists. Do you want to replace it?",
                            PrimaryButtonText = "Yes",
                            SecondaryButtonText = "Cancel"
                        };
                        if (await contentDialog.ShowAsync() != ContentDialogResult.Primary)
                        {
                            return;
                        }
                        else
                        {
                            File.Delete(SaveDB.Text);
                        }
                    }
                    WaitingGrid.Visibility = Visibility.Visible;
                    WaitingRing.IsActive = true;
                    await SDEBuilder.Builder.StartBuilder(items.Where(p => p.Checked == true).Select(p => p.File).ToArray(), language, SaveDB.Text);
                }
                catch (Exception ex)
                {
                    ContentDialog contentDialog = new ContentDialog()
                    {
                        XamlRoot = this.Content.XamlRoot,
                        Title = "Error",
                        Content = ex.ToString(),
                        PrimaryButtonText = "OK",
                        IsSecondaryButtonEnabled = false,
                    };
                    await contentDialog.ShowAsync();
                }
                finally
                {
                    WaitingGrid.Visibility = Visibility.Collapsed;
                    WaitingRing.IsActive = false;
                }
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSDEFiles(SDEFolder.Text);
        }
        private async void LoadSDEFiles(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                ContentDialog contentDialog = new ContentDialog()
                {
                    XamlRoot = this.Content.XamlRoot,
                    Title = "Error",
                    Content = "SDE Folder is empty",
                    PrimaryButtonText = "OK",
                    IsSecondaryButtonEnabled = false,
                };
                await contentDialog.ShowAsync();
            }
            else if (!Directory.Exists(folder))
            {
                ContentDialog contentDialog = new ContentDialog()
                {
                    XamlRoot = this.Content.XamlRoot,
                    Title = "Error",
                    Content = "SDE Folder does not exist",
                    PrimaryButtonText = "OK",
                    IsSecondaryButtonEnabled = false,
                };
                await contentDialog.ShowAsync();
            }
            else
            {
                var files = Directory.GetFiles(folder).Select(p => new SDEFile(p)).ToList();
                var sdeInfoFile = files.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p.File) == "_sde");
                if (sdeInfoFile != null)
                {
                    files.Remove(sdeInfoFile);
                    var model = JsonConvert.DeserializeObject<SDEInfo>(System.IO.File.ReadAllText(sdeInfoFile.File));
                    string lang = LangComboBox.SelectionBoxItem == null ? "En" : LangComboBox.SelectionBoxItem.ToString();
                    _releaseDate = model.ReleaseDate;
                    SaveDB.Text = $"SDE_{lang}_{model.ReleaseDate.ToString("yyyyMMdd")}.db";
                }
                FilesListView.ItemsSource = files;
            }
        }

        private async void PickSDEFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = await PickFolderAsync(this);
            if (folder != null)
            {
                SDEFolder.Text = folder.Path;
                SettingService.SetValue(SettingService.SDEFolderKey, folder.Path);
                LoadSDEFiles(SDEFolder.Text);
            }
        }
        public static async Task<StorageFolder> PickFolderAsync(Window window)
        {
            FolderPicker openPicker = new FolderPicker();
            openPicker.FileTypeFilter.Add("*");
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
            return await openPicker.PickSingleFolderAsync();
        }
        public static async Task<StorageFile> PickSaveFileAsync(string suggestedFileName, Window window)
        {
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            savePicker.FileTypeChoices.Add("Save file", new List<string>() { System.IO.Path.GetExtension(suggestedFileName) });
            savePicker.SuggestedFileName = suggestedFileName;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
            return await savePicker.PickSaveFileAsync();
        }

        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            var files = FilesListView?.ItemsSource as List<SDEFile>;
            if (files != null)
            {
                foreach (var file in files)
                {
                    file.Checked = true;
                }
            }
        }

        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            var files = FilesListView?.ItemsSource as List<SDEFile>;
            if (files != null)
            {
                foreach (var file in files)
                {
                    file.Checked = false;
                }
            }
        }

        private async void PickSaveDB_Click(object sender, RoutedEventArgs e)
        {
            var file = await PickSaveFileAsync($"SDE_{LangComboBox.SelectionBoxItem.ToString()}_{DateTime.Now.ToString("yyyyMMdd")}.db",this);
            if (file != null)
            {
                SaveDB.Text = file.Path;
            }
        }

        private async void StartNewEdenButton_Click(object sender, RoutedEventArgs e)
        {
            var items = FilesListView.ItemsSource as List<SDEFile>;
            string folder = AppContext.BaseDirectory;
            if (items != null && items.Count > 0)
            {
                try
                {
                    WaitingGrid.Visibility = Visibility.Visible;
                    WaitingRing.IsActive = true;
                    await SDEBuilder.NewEdenBuilder.StartBuilder(items.Select(p => p.File).ToArray(), folder, new LanguageEnum[] { LanguageEnum.Zh},_releaseDate);
                }
                catch (Exception ex)
                {
                    ContentDialog contentDialog = new ContentDialog()
                    {
                        XamlRoot = this.Content.XamlRoot,
                        Title = "Error",
                        Content = ex.ToString(),
                        PrimaryButtonText = "OK",
                        IsSecondaryButtonEnabled = false,
                    };
                    await contentDialog.ShowAsync();
                }
                finally
                {
                    WaitingGrid.Visibility = Visibility.Collapsed;
                    WaitingRing.IsActive = false;
                }
            }
        }
    }
}
