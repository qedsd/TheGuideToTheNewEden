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
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Views.Character
{
    public sealed partial class CharactersPage : Page
    {
        public CharactersPage()
        {
            this.InitializeComponent();
            Loaded += CharactersPage_Loaded;
        }

        private void CharactersPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= CharactersPage_Loaded;
            VM.Init();
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

        private void RemoveCharacter_Click(object sender, RoutedEventArgs e)
        {
            VM.RemoveCommand.Execute((sender as FrameworkElement).DataContext);
        }

        private void GridView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            //var item = e.Items.First() as CharacterViewModel;
            //e.Data.SetText(item.SelectedCharacter.CharacterID.ToString());
            //e.Data.RequestedOperation = DataPackageOperation.Move;
        }

        private async void GridView_Drop(object sender, DragEventArgs e)
        {
            GridView target = (GridView)sender;
            if (e.DataView.Contains(StandardDataFormats.Text))
            {
                DragOperationDeferral def = e.GetDeferral();
                string id = await e.DataView.GetTextAsync();
                var character = VM.Characters.FirstOrDefault(p => p.SelectedCharacter.CharacterID.ToString() == id);
                if (character != null)
                {
                    
                }
            }
        }

        private void GridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            var characters = VM.Characters.Where(p => p.SelectedCharacter != null).Select(p=>p.SelectedCharacter).ToList();
            if(characters.Count > 0)
            {
                for (int i = 0; i < characters.Count; i++)
                {
                    var character = characters[i];
                    if (character != null)
                    {
                        Services.CharacterService.SetOrder(character, i);
                    }
                }
                Services.CharacterService.Save();
            }
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
