// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.WinUI.Interfaces;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Views.IntelOverlapPages
{
    public sealed partial class IntelBasePage : Page
    {
        public IntelBasePage(IIntelOverlapPage intelOverlapPage)
        {
            this.InitializeComponent();
            MainFrame.Content = intelOverlapPage;
        }
        public event EventHandler OnIntelInfoButtonClicked;
        public event EventHandler OnStopSoundButtonClicked;
        public event EventHandler OnClearButtonClicked;

        public void SetIntelInfo(string tip, string content, List<IntelShipContent> intelShips)
        {
            //Button_IntelInfo.Content = info;
            IntelInfoRichTextBlock.Blocks.Clear();
            Paragraph paragraph = new Paragraph();

            Run tipRun = new Run()
            {
                FontWeight = FontWeights.Normal,
                Text = tip
            };
            paragraph.Inlines.Add(tipRun);

            if (intelShips.NotNullOrEmpty())
            {
                List<Run> contentRuns = new List<Run>();
                int startIndex = 0;
                int normalContentRunLength = 0;
                foreach (var ship in intelShips)
                {
                    normalContentRunLength = ship.StartIndex - startIndex;
                    if (normalContentRunLength > 0)
                    {
                        contentRuns.Add(new Run()
                        {
                            FontWeight = FontWeights.Normal,
                            Text = content.Substring(startIndex, normalContentRunLength),
                        });
                    }
                    contentRuns.Add(new Run()
                    {
                        FontWeight = FontWeights.Black,
                        Text = content.Substring(ship.StartIndex, ship.Length),
                    });
                    startIndex = ship.StartIndex + ship.Length;
                }
                if (startIndex < content.Length)
                {
                    contentRuns.Add(new Run()
                    {
                        FontWeight = FontWeights.Normal,
                        Text = content.Substring(startIndex),
                    });
                }
                foreach (var r in contentRuns)
                {
                    paragraph.Inlines.Add(r);
                }
            }
            else
            {
                paragraph.Inlines.Add(new Run()
                {
                    FontWeight = FontWeights.Normal,
                    Text = content,
                });
            }
            IntelInfoRichTextBlock.Blocks.Add(paragraph);
        }
        private void Button_IntelInfo_Click(object sender, RoutedEventArgs e)
        {
            OnIntelInfoButtonClicked?.Invoke(sender, EventArgs.Empty);
        }

        private void Button_StopSound_Click(object sender, RoutedEventArgs e)
        {
            OnStopSoundButtonClicked?.Invoke(sender, EventArgs.Empty);
        }

        private void Button_Clear_Click(object sender, RoutedEventArgs e)
        {
            OnClearButtonClicked?.Invoke(sender, EventArgs.Empty);
        }
    }
}
