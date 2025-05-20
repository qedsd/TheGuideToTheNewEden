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
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.ViewModels;
using TheGuideToTheNewEden.WinUI.Views.Character;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class CharactersPage : Page
    {
        public CharactersPage()
        {
            this.InitializeComponent();
            Loaded += CharacterPage_Loaded;
        }
        private void CharacterPage_Loaded(object sender, RoutedEventArgs e)
        {
            VM.Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }
        

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if((e.ClickedItem as CharacterViewModel).SelectedCharacter == null)
            {
                VM.AddCommand.Execute(null);
            }
            else
            {
                VM.ShowCommand.Execute(e.ClickedItem);
            }
        }

        private void AddTemplat_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as FrameworkElement).DataContext = VM;
        }
    }
    public class CharacterCardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ShowTemplate { get; set; }

        public DataTemplate AddTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if ((item as CharacterViewModel).SelectedCharacter == null)
            {
                return AddTemplate;
            }
            else
            {
                return ShowTemplate;
            }
        }
    }
}
