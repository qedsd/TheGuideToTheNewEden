using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    public static class PickHelper
    {
        /// <summary>
        /// 选择文件
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static async Task<StorageFile> PickImgAsync(Window window)
        {
            List<string> ps = new List<string>()
            {
                ".jpg",
                ".jpeg",
                ".png"
            };
            return await PickFileAsync(window, ps);
        }
        /// <summary>
        /// 选择文件
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static async Task<StorageFile> PickFileAsync(Window window, string filter)
        {
            return await PickFileAsync(window, new List<string>() { filter });
        }
        /// <summary>
        /// 选择文件
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static async Task<StorageFile> PickFileAsync(Window window,List<string> filter = null)
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
            if (filter != null && filter.Count != 0)
            {
                filter.Where(p=>!string.IsNullOrWhiteSpace(p)).ToList().ForEach(p => openPicker.FileTypeFilter.Add(p));
            }
            if(openPicker.FileTypeFilter.Count == 0)
            {
                openPicker.FileTypeFilter.Add("*");
            }
            return await openPicker.PickSingleFileAsync();
        }
        /// <summary>
        /// 选择文件夹
        /// </summary>
        /// <returns></returns>
        public static async Task<StorageFolder> PickFolderAsync(Window window)
        {
            FolderPicker openPicker = new FolderPicker();
            openPicker.FileTypeFilter.Add("*");
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);
            return await openPicker.PickSingleFolderAsync();
        }
        /// <summary>
        /// 选择保存文件
        /// </summary>
        /// <returns></returns>
        public static async Task<StorageFile> PickSaveFileAsync(string suggestedFileName, Window window)
        {
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            savePicker.FileTypeChoices.Add("保存文件", new List<string>() { System.IO.Path.GetExtension(suggestedFileName) });
            savePicker.SuggestedFileName = suggestedFileName;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
            return await savePicker.PickSaveFileAsync();
        }
    }
}
