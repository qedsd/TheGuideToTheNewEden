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
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class LinkInfoControl : UserControl
    {
        public LinkInfoControl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty LinkInfoProperty
            = DependencyProperty.Register(
                nameof(LinkInfo),
                typeof(LinkInfo),
                typeof(LinkInfoControl),
                new PropertyMetadata(null, new PropertyChangedCallback(LinkInfoPropertyChanged)));

        public LinkInfo LinkInfo
        {
            get => (LinkInfo)GetValue(LinkInfoProperty);
            set
            {
                SetValue(LinkInfoProperty, value);
                if(value != null)
                {
                    TextBlock_Name.Text = value.Name;
                    TextBlock_Desc.Text = value.Description;
                    StringBuilder stringBuilder = new StringBuilder();
                    foreach(var c in  value.Categories)
                    {
                        stringBuilder.Append(c);
                        stringBuilder.Append(' ');
                    }
                    if(stringBuilder.Length > 1)
                    {
                        stringBuilder.Remove(stringBuilder.Length - 1, 1);
                    }
                    TextBlock_Categories.Text = stringBuilder.ToString();

                    stringBuilder.Clear();
                    foreach (var c in value.Platforms)
                    {
                        stringBuilder.Append(c);
                        stringBuilder.Append(' ');
                    }
                    if (stringBuilder.Length > 1)
                    {
                        stringBuilder.Remove(stringBuilder.Length - 1, 1);
                    }
                    TextBlock_Platforms.Text = stringBuilder.ToString();

                    stringBuilder.Clear();
                    foreach (var c in value.Langs)
                    {
                        stringBuilder.Append(c);
                        stringBuilder.Append(' ');
                    }
                    if (stringBuilder.Length > 1)
                    {
                        stringBuilder.Remove(stringBuilder.Length - 1, 1);
                    }
                    TextBlock_Lang.Text = stringBuilder.ToString();
                }
            }
        }
        private static void LinkInfoPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as LinkInfoControl).LinkInfo = e.NewValue as LinkInfo;
        }
    }
}
