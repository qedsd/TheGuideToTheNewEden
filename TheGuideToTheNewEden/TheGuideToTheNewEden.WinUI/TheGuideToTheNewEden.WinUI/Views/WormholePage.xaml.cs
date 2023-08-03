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
using TheGuideToTheNewEden.Core.DBModels;
using System.Text;
using TheGuideToTheNewEden.Core.Services.DB;
using Microsoft.UI.Xaml.Documents;
using Windows.UI;
using ESI.NET.Models.Universe;
using TheGuideToTheNewEden.WinUI.Services;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class WormholePage : Page
    {
        internal class SerachItem
        {
            public string Name {get;set;}
            public int Id { get; set; }
            public bool IsPortal
            {
                get => Obj.GetType() == typeof(WormholePortal);
            }
            public object Obj { get; set; }
        }
        public WormholePage()
        {
            this.InitializeComponent();
        }
        private SerachItem _selectedItem;
        private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if(string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = null;
                return;
            }
            if(_selectedItem != null && _selectedItem.Name == sender.Text)
            {
                return;
            }
            List<SerachItem> serachItems = new List<SerachItem>();
            var holes = await Core.Services.DB.WormholeService.QueryWormholeAsync(sender.Text);
            var portals = await Core.Services.DB.WormholeService.QueryPortalAsync(sender.Text);
            if(holes.NotNullOrEmpty())
            {
                foreach( var hole in holes)
                {
                    serachItems.Add(new SerachItem()
                    {
                        Id = hole.Id,
                        Name = hole.Name,
                        Obj = hole
                    });
                }
            }
            if(portals.NotNullOrEmpty())
            {
                foreach (var portal in portals)
                {
                    serachItems.Add(new SerachItem()
                    {
                        Id = portal.Id,
                        Name = portal.Name,
                        Obj = portal
                    });
                }
            }
            sender.ItemsSource = serachItems;
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            _selectedItem = args.SelectedItem as SerachItem;
            if(_selectedItem != null)
            {
                if(_selectedItem.IsPortal)
                {
                    LoadWormholePortalDetail(_selectedItem.Obj as WormholePortal);
                }
                else
                {
                    LoadWormholeDetail(_selectedItem.Obj as Wormhole);
                }
            }
            sender.Text = _selectedItem.Name;
        }

        readonly int defaultFontSize = 16;
        readonly int defaultLineHeight = 30;
        public void LoadWormholeDetail(Wormhole wormhole)
        {
            RichTextBlock_Main.Blocks.Clear();
            RichTextBlock_Other.Blocks.Clear();
            RichTextBlock_Main.HorizontalTextAlignment = TextAlignment.Center;
            RichTextBlock_Other.HorizontalTextAlignment = TextAlignment.Left;
            Paragraph nameParagraph = new Paragraph() { LineHeight = defaultLineHeight * 2 };
            RichTextBlock_Main.Blocks.Add(nameParagraph);
            nameParagraph.Inlines.Add(new Run()
            {
                FontSize = 32,
                Text = wormhole.Name,
                Foreground = new SolidColorBrush((Color)Helpers.ResourcesHelper.Get("SystemAccentColor")),
            }) ;

            Paragraph classParagraph = new Paragraph() { LineHeight = defaultLineHeight };
            RichTextBlock_Main.Blocks.Add(classParagraph);
            classParagraph.Inlines.Add(new Run()
            {
                FontSize = defaultFontSize,
                Text = Helpers.ResourcesHelper.GetString($"WormholePage_Class_{wormhole.Class}"),
            });
            
            if (wormhole.Phenomena >= 0)
            {
                classParagraph.Inlines.Add(new Run()
                {
                    FontSize = defaultFontSize,
                    Text = " ",
                });
                classParagraph.Inlines.Add(new Run()
                {
                    FontSize = defaultFontSize,
                    Text = Helpers.ResourcesHelper.GetString($"WormholePage_Phenomena_{wormhole.Phenomena}")
                });
            }

            if(!string.IsNullOrEmpty(wormhole.Statics))
            {
                Paragraph staticsParagraph = new Paragraph() { LineHeight = defaultLineHeight };
                RichTextBlock_Main.Blocks.Add(staticsParagraph);
                staticsParagraph.Inlines.Add(new Run()
                {
                    FontSize = defaultFontSize,
                    Text = $"{Helpers.ResourcesHelper.GetString("WormholePage_Portal_Respawn_Static")}: ",
                    FontWeight = new Windows.UI.Text.FontWeight(1)
                });
                foreach (var s in wormhole.Statics.Split(','))
                {
                    staticsParagraph.Inlines.Add(CreatePortalLink(int.Parse(s)));
                }
            }
            if (!string.IsNullOrEmpty(wormhole.Wanderings))
            {
                Paragraph wanderingsParagraph = new Paragraph() { LineHeight = defaultLineHeight };
                RichTextBlock_Main.Blocks.Add(wanderingsParagraph);
                wanderingsParagraph.Inlines.Add(new Run()
                {
                    FontSize = defaultFontSize,
                    Text = $"{Helpers.ResourcesHelper.GetString("WormholePage_Portal_Respawn_Wandering")}: ",
                    FontWeight = new Windows.UI.Text.FontWeight(1)
                });
                foreach (var s in wormhole.Wanderings.Split(','))
                {
                    wanderingsParagraph.Inlines.Add(CreatePortalLink(int.Parse(s)));
                }
            }

            #region ÐÐÐÇ
            var stellars = WormholeService.QueryWormholeStellar(wormhole.Id);
            if(stellars.NotNullOrEmpty())
            {
                var types = InvTypeService.QueryTypes(stellars.Select(p => p.TypeId).ToList());
                foreach(var stellar in stellars)
                {
                    Paragraph stellarParagraph = new Paragraph() { LineHeight = defaultLineHeight };
                    RichTextBlock_Other.Blocks.Add(stellarParagraph);
                    stellarParagraph.Inlines.Add(new Run()
                    {
                        FontSize = defaultFontSize,
                        Text = stellar.Name
                    });
                    var targetType = types.FirstOrDefault(p => p.TypeID == stellar.TypeId);
                    if(targetType != null)
                    {
                        stellarParagraph.Inlines.Add(new Run()
                        {
                            FontSize = defaultFontSize,
                            Text = $"   {targetType.TypeName}"
                        });
                    }
                }
            }
            #endregion

            #region link
            RichTextBlock_Other.Blocks.Add(new Paragraph());
            RichTextBlock_Other.Blocks.Add(CreateLinkParagraph($"http://anoik.is/systems/{wormhole.Name}", "Anoik"));
            RichTextBlock_Other.Blocks.Add(CreateLinkParagraph($"https://evemaps.dotlan.net/system/{wormhole.Name}", "Dotlan"));
            RichTextBlock_Other.Blocks.Add(CreateLinkParagraph($"https://www.ellatha.com/eve/WormholeSystemview.asp?key={wormhole.Name.TrimStart('J')}", "Ellatha"));
            RichTextBlock_Other.Blocks.Add(CreateLinkParagraph($"https://zkillboard.com/system/{wormhole.Id}", "Zkillboard"));
            RichTextBlock_Other.Blocks.Add(CreateLinkParagraph($"http://games.chruker.dk/eve_online/solarsystem.php?show=all&name={wormhole.Name}", "Chruker"));
            RichTextBlock_Other.Blocks.Add(CreateLinkParagraph($"http://venus.wormholes.club/", "°®Éñ³æ¶´"));
            #endregion
        }

        private Paragraph CreateLinkParagraph(string uri,string show)
        {
            Paragraph paragraph = new Paragraph() { LineHeight = defaultLineHeight };
            Hyperlink link = new Hyperlink()
            {
                NavigateUri = new Uri(uri)
            };
            link.Inlines.Add(new Run()
            {
                FontSize = defaultFontSize,
                Text = show,
            });
            paragraph.Inlines.Add(link);
            return paragraph;
        }
        private Hyperlink CreatePortalLink(int id)
        {
            var portal = WormholeService.QueryPortal(id);
            if (portal != null)
            {
                Hyperlink link = new Hyperlink()
                {
                    UnderlineStyle = UnderlineStyle.None,
                };
                if (string.IsNullOrEmpty(portal.Destination))
                {
                    link.Inlines.Add(new Run()
                    {
                        FontSize = defaultFontSize,
                        Text = $"{portal.Name}   ",
                    });
                }
                else
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.Append(portal.Name);
                    stringBuilder.Append("(->");
                    stringBuilder.Append(Helpers.ResourcesHelper.GetString($"WormholePage_Class_{portal.Destination}"));
                    stringBuilder.Append(")   ");
                    link.Inlines.Add(new Run()
                    {
                        FontSize = defaultFontSize,
                        Text = stringBuilder.ToString(),
                    });
                }
                link.Click += Link_Click;
                return link;
            }
            else
            {
                return null;
            }
        }

        private void Link_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            WormholePage wormholePage = new WormholePage();
            var text = (sender.Inlines.First() as Run).Text;
            string portalName = string.Empty;
            int index = text.IndexOf('(');
            if(index == -1)
            {
                portalName = text.Trim();
            }
            else
            {
                portalName = text.Substring(0, index).Trim();
            }
            wormholePage.LoadWormholePortalDetail(WormholeService.QueryPortalByName(portalName));
            NavigationService.NavigateTo(wormholePage, Helpers.ResourcesHelper.GetString("ShellPage_Wormhole"));
        }

        public void LoadWormholePortalDetail(WormholePortal wormholePortal)
        {
            RichTextBlock_Main.Blocks.Clear();
            RichTextBlock_Other.Blocks.Clear();
            RichTextBlock_Main.HorizontalTextAlignment = TextAlignment.Left;
            RichTextBlock_Other.HorizontalTextAlignment = TextAlignment.Center;
            Paragraph nameParagraph = new Paragraph() { LineHeight = defaultLineHeight * 2 };
            RichTextBlock_Main.Blocks.Add(nameParagraph);
            nameParagraph.Inlines.Add(new Run()
            {
                FontSize = 32,
                Text = wormholePortal.Name,
                Foreground = new SolidColorBrush((Color)Helpers.ResourcesHelper.Get("SystemAccentColor")),
            });

            if(!string.IsNullOrEmpty(wormholePortal.Destination))
            {
                Paragraph destParagraph = new Paragraph() { LineHeight = defaultLineHeight };
                RichTextBlock_Main.Blocks.Add(destParagraph);
                destParagraph.Inlines.Add(new Run()
                {
                    FontSize = defaultFontSize,
                    Text = $"{Helpers.ResourcesHelper.GetString("WormholePage_Portal_Destination")}",
                    FontWeight = new Windows.UI.Text.FontWeight(1)
                });
                destParagraph.Inlines.Add(new Run()
                {
                    FontSize = defaultFontSize,
                    Text = "    ",
                });
                foreach (var dest in wormholePortal.Destination.Split(','))
                {
                    destParagraph.Inlines.Add(new Run()
                    {
                        FontSize = defaultFontSize,
                        Text = $" {Helpers.ResourcesHelper.GetString($"WormholePage_Class_{dest}")} ",
                    });
                }
            }
            
            if(!string.IsNullOrEmpty(wormholePortal.AppearsIn))
            {
                Paragraph appearsInParagraph = new Paragraph() { LineHeight = defaultLineHeight };
                RichTextBlock_Main.Blocks.Add(appearsInParagraph);
                appearsInParagraph.Inlines.Add(new Run()
                {
                    FontSize = defaultFontSize,
                    Text = Helpers.ResourcesHelper.GetString("WormholePage_Portal_AppearsIn"),
                    FontWeight = new Windows.UI.Text.FontWeight(1)
                });
                appearsInParagraph.Inlines.Add(new Run()
                {
                    FontSize = defaultFontSize,
                    Text = "    ",
                });
                foreach (var ap in wormholePortal.AppearsIn.Split(','))
                {
                    appearsInParagraph.Inlines.Add(new Run()
                    {
                        FontSize = defaultFontSize,
                        Text = $" {Helpers.ResourcesHelper.GetString($"WormholePage_Class_{ap}")} ",
                    });
                }
            }
            

            Paragraph lifetimeParagraph = new Paragraph() { LineHeight = defaultLineHeight };
            RichTextBlock_Main.Blocks.Add(lifetimeParagraph);
            lifetimeParagraph.Inlines.Add(new Run()
            {
                FontSize = defaultFontSize,
                Text = Helpers.ResourcesHelper.GetString("WormholePage_Portal_Lifetime"),
                FontWeight = new Windows.UI.Text.FontWeight(1)
            });
            lifetimeParagraph.Inlines.Add(new Run()
            {
                FontSize = defaultFontSize,
                Text = $"    {wormholePortal.Lifetime} {Helpers.ResourcesHelper.GetString("General_Hour")}",
            });

            Paragraph maxMassperJumpParagraph = new Paragraph() { LineHeight = defaultLineHeight };
            RichTextBlock_Main.Blocks.Add(maxMassperJumpParagraph);
            maxMassperJumpParagraph.Inlines.Add(new Run()
            {
                FontSize = defaultFontSize,
                Text = Helpers.ResourcesHelper.GetString("WormholePage_Portal_MaxMassPerJump"),
                FontWeight = new Windows.UI.Text.FontWeight(1)
            });
            maxMassperJumpParagraph.Inlines.Add(new Run()
            {
                FontSize = defaultFontSize,
                Text = $"    {wormholePortal.MaxMassPerJump:N2} kg ({wormholePortal.MaxMassPerJumpNote})",
            });

            Paragraph totalJumpMassParagraph = new Paragraph() { LineHeight = defaultLineHeight };
            RichTextBlock_Main.Blocks.Add(totalJumpMassParagraph);
            totalJumpMassParagraph.Inlines.Add(new Run()
            {
                FontSize = defaultFontSize,
                Text = Helpers.ResourcesHelper.GetString("WormholePage_Portal_TotalJumpMass"),
                FontWeight = new Windows.UI.Text.FontWeight(1)
            });
            totalJumpMassParagraph.Inlines.Add(new Run()
            {
                FontSize = defaultFontSize,
                Text = $"    {wormholePortal.TotalJumpMass:N2} kg ({wormholePortal.TotalJumpMassNote})",
            });

            Paragraph respawnParagraph = new Paragraph() { LineHeight = defaultLineHeight };
            RichTextBlock_Main.Blocks.Add(respawnParagraph);
            var respawn = wormholePortal.Respawn == "Static" ? Helpers.ResourcesHelper.GetString("WormholePage_Portal_Respawn_Static") : Helpers.ResourcesHelper.GetString("WormholePage_Portal_Respawn_Wandering");
            respawnParagraph.Inlines.Add(new Run()
            {
                FontSize = defaultFontSize,
                Text = Helpers.ResourcesHelper.GetString("WormholePage_Portal_Respawn"),
                FontWeight = new Windows.UI.Text.FontWeight(1)
            });
            respawnParagraph.Inlines.Add(new Run()
            {
                FontSize = defaultFontSize,
                Text = $"    {respawn}",
            });

            Paragraph massRegenParagraph = new Paragraph() { LineHeight = defaultLineHeight };
            RichTextBlock_Main.Blocks.Add(massRegenParagraph);
            massRegenParagraph.Inlines.Add(new Run()
            {
                FontSize = defaultFontSize,
                Text = Helpers.ResourcesHelper.GetString("WormholePage_Portal_MassRegen"),
                FontWeight = new Windows.UI.Text.FontWeight(1)
            });
            massRegenParagraph.Inlines.Add(new Run()
            {
                FontSize = defaultFontSize,
                Text = $"    {wormholePortal.MassRegen:N2} kg",
            });
            RichTextBlock_Other.Blocks.Add(CreateLinkParagraph($"http://anoik.is/wormholes/{wormholePortal.Name}", "Anoik"));
            RichTextBlock_Other.Blocks.Add(CreateLinkParagraph($"https://www.ellatha.com/eve/wormholelistview.asp?key=Wormhole+{wormholePortal.Name}", "Ellatha"));
        }


    }
}
