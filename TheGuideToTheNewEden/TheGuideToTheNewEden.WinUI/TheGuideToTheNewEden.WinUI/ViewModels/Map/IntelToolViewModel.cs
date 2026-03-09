using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.WinUI.Models.Map;
using TheGuideToTheNewEden.WinUI.Views.Map;
using TheGuideToTheNewEden.WinUI.Views.Map.Tools;
using ZKB.NET.Models.KillStream;
using static TheGuideToTheNewEden.WinUI.Controls.MapDataTypeControl;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.WinUI.Extensions;
using Microsoft.UI.Xaml;
using ESI.NET.Models.Location;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

namespace TheGuideToTheNewEden.WinUI.ViewModels.Map
{
    public class IntelToolViewModel : BaseViewModel
    {
        private bool _running;
        public bool Running { get => _running; set => SetProperty(ref _running, value); }

        private Visibility _settingVisible = Visibility.Visible;
        public Visibility SettingVisible 
        { 
            get => _settingVisible;
            set
            {
                SetProperty(ref _settingVisible, value);
            }
        }

        public MapIntelConfig Config { get; set; }
        public ObservableCollection<MapIntelMsg> IntelMsgs { get; set; }
        public List<MapIntelMsg> AllIntelMsgs { get; set; }

        private MapCanvas _mapCanvas;
        private Dictionary<int, MapData> _systemDatas;
        private Dictionary<int, SovData> _sovDatas;
        private DispatcherTimer _timer;
        public IntelToolViewModel()
        {
            Config = Services.Settings.MapSettingService.Current.GetIntel();
            IntelMsgs = new ObservableCollection<MapIntelMsg>();
            AllIntelMsgs = new List<MapIntelMsg>();
        }
        public void Init(MapCanvas mapCanvas, Dictionary<int, MapData> systemDatas, Dictionary<int, SovData> sovDatas)
        {
            _mapCanvas = mapCanvas;
            _systemDatas = systemDatas;
            _sovDatas = sovDatas;
        }
        public void Dispose()
        {
            Stop();
            _mapCanvas = null;
            _systemDatas = null;
            _sovDatas = null;
            IntelMsgs.Clear();
            IntelMsgs = null;
        }

        public ICommand StartCommand => new RelayCommand(async() =>
        {
            Services.Settings.MapSettingService.Current.SaveIndel(Config);
            ShowWaiting();
            await Start();
            HideWaiting();
            Running = true;
            SettingVisible = Visibility.Collapsed;
        });
        public ICommand StopCommand => new RelayCommand(() =>
        {
            Stop();
            Running = false;
            SettingVisible = Visibility.Visible;
        });
        public ICommand SettingCommand => new RelayCommand(() =>
        {
            SettingVisible = SettingVisible == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        });
        public ICommand ClearCommand => new RelayCommand(() =>
        {
            Window.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                IntelMsgs.Clear();
                AllIntelMsgs.Clear();
                _mapCanvas.IntelDrawer.Clear();
            });
        });
        private async Task<bool> Start()
        {
            try
            {
                await Core.Services.ZKBStreamService.Current.Sub();
                Core.Services.ZKBStreamService.Current.OnError += KillStream_OnError;
                Core.Services.ZKBStreamService.Current.OnMessage += KillStream_OnMessage;
                StartTimer();
                _mapCanvas.IntelDrawer.SetEnable(true);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                StopTimer();
                return false;
            }
        }
        private void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer()
                {
                    Interval = TimeSpan.FromSeconds(30),
                };
                _timer.Tick += Timer_Tick; ;
            }
            _timer.Start();
        }
        private void StopTimer()
        {
            _timer?.Stop();
        }
        private void Timer_Tick(object sender, object e)
        {
            foreach(var msg in AllIntelMsgs)
            {
                msg.Elapsed = Math.Round((DateTime.Now - msg.Time).TotalMinutes,0);
            }
            var expiredMsgs = AllIntelMsgs.Where(p => p.Elapsed > Config.ZKBDuration / 60).ToList();
            if (expiredMsgs.Any())
            {
                foreach(var expiredMsg in expiredMsgs)
                {
                    foreach (var attacker in expiredMsg.Attackers)
                    {
                        if (attacker.Type == 0 && attacker.Main != null && attacker.Attacker != null)
                        {
                            _mapCanvas.IntelDrawer.Remove(expiredMsg.System.SolarSystemID, attacker.Main.Id, attacker.Attacker.Id);
                        }
                        else if (attacker.Type == 1 && attacker.Main != null && attacker.Attackers != null)
                        {
                            foreach (var a in attacker.Attackers)
                            {
                                _mapCanvas.IntelDrawer.Remove(expiredMsg.System.SolarSystemID, a.Main.Id, a.Attacker.Id);
                            }
                        }
                    }
                    AllIntelMsgs.Remove(expiredMsg);
                    IntelMsgs.Remove(expiredMsg);
                }
            }
            _mapCanvas.Draw();
        }

        private void Stop()
        {
            Core.Services.ZKBStreamService.Current.UnSub();
            Core.Services.ZKBStreamService.Current.OnError -= KillStream_OnError;
            Core.Services.ZKBStreamService.Current.OnMessage -= KillStream_OnMessage;
            _mapCanvas.IntelDrawer.SetEnable(false);
            StopTimer();
        }
        private void KillStream_OnError(object sender, Exception e)
        {
            ShowError(e.Message, false);
        }
        private async void KillStream_OnMessage(object sender, SKBDetail detail, string sourceData)
        {
            try
            {
                var span = DateTime.UtcNow - detail.KillmailTime;
                if ((Config.ZKBDuration <= 0 || span.TotalSeconds < Config.ZKBDuration) && !detail.Zkb.Npc)
                {
                    if (_systemDatas.TryGetValue(detail.SolarSystemId, out var data) && data.Enable)
                    {
                        var info = Core.Helpers.KBHelpers.CreateKBItemInfo(detail);
                        if (info != null)
                        {
                            MapIntelMsg msg = new MapIntelMsg()
                            {
                                Type = MapIntelType.KB,
                                System = info.SolarSystem,
                                Region = info.Region,
                                KB = info,
                                Time = info.SKBDetail.KillmailTime.ToLocalTime(),
                            };
                            msg.Attackers = await GetAttackers(msg.KB);
                            if (msg.Attackers?.Count > Config.ZKBMaxAttackerCount)
                            {
                                var shipGroup = msg.Attackers.Where(p => p.Attacker != null).GroupBy(p => p.Main.Id).ToList();
                                var factionGroup = msg.Attackers.Where(p => p.Faction != null).GroupBy(p => p.Faction.Id).ToList();
                                msg.Attackers.Clear();
                                foreach (var ship in shipGroup)
                                {
                                    msg.Attackers.Add(new MapIntelAttacker()
                                    {
                                        Type = 1,
                                        StatisticCount = ship.Count(),
                                        Main = ship.First().Main,
                                        MainContent = $"+{ship.Count()}",
                                        MainIconURL = Converters.GameImageConverter.GetImageUri(ship.Key, Converters.GameImageConverter.ImgType.Type, 64),
                                        Attackers = ship.ToList()
                                    });
                                }
                                foreach (var faction in factionGroup)
                                {
                                    msg.Attackers.Add(new MapIntelAttacker()
                                    {
                                        Type = 2,
                                        StatisticCount = faction.Count(),
                                        Main = faction.First().Faction,
                                        MainContent = $"+{faction.Count()}",
                                        MainIconURL = Converters.GameImageConverter.GetImageUri(faction.Key, faction.First().Faction.GetCategory() == IdName.CategoryEnum.Corporation ? Converters.GameImageConverter.ImgType.Corporation : Converters.GameImageConverter.ImgType.Alliance, 64),
                                        Attackers = faction.ToList()
                                    });
                                }
                            }
                            InserMsg(msg);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                ShowError(ex.Message);
            }
        }
        private void InserMsg(MapIntelMsg msg)
        {
            if (_sovDatas != null && _sovDatas.TryGetValue(msg.System.SolarSystemID, out var sovData))
            {
                msg.Sov = sovData;
            }
            msg.Elapsed = Math.Round((DateTime.Now - msg.Time).TotalMinutes,0);
            Window.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                if(IntelMsgs.Count > Config.MaxMsgCount)
                {
                    IntelMsgs.RemoveAt(IntelMsgs.Count - 1);
                }
                IntelMsgs.Insert(0, msg);
                AllIntelMsgs.Add(msg);
                if (msg.Type == MapIntelType.KB)
                {
                    foreach(var attacker in msg.Attackers)
                    {
                        if (attacker.Type == 0 && attacker.Main != null && attacker.Attacker != null)
                        {
                            _mapCanvas.IntelDrawer.Add(msg.System.SolarSystemID, attacker.Main.Id, attacker.Attacker.Id, msg);
                        }
                        else if(attacker.Type == 1 && attacker.Main != null && attacker.Attackers != null)
                        {
                            foreach (var a in attacker.Attackers)
                            {
                                _mapCanvas.IntelDrawer.Add(msg.System.SolarSystemID, a.Main.Id, a.Attacker.Id, msg);
                            }
                        }
                    }
                }
            });
        }

        private async Task<List<MapIntelAttacker>> GetAttackers(Core.Models.KB.KBItemInfo kB)
        {
            if (kB.SKBDetail.Attackers.NotNullOrEmpty())
            {
                List<MapIntelAttacker> attackers = new List<MapIntelAttacker>();
                foreach (var attacker in kB.SKBDetail.Attackers)
                {
                    MapIntelAttacker mapIntelAttacker = new MapIntelAttacker();
                    attackers.Add(mapIntelAttacker);
                    if (attacker.CharacterId > 0)
                    {
                        mapIntelAttacker.Attacker = new IdName(attacker.CharacterId, attacker.CharacterId.ToString(), IdName.CategoryEnum.Character);
                    }
                    else if (attacker.CorporationId > 0)
                    {
                        mapIntelAttacker.Attacker = new IdName(attacker.CorporationId, attacker.CorporationId.ToString(), IdName.CategoryEnum.Corporation);
                    }
                    else if (attacker.AllianceId > 0)
                    {
                        mapIntelAttacker.Attacker = new IdName(attacker.AllianceId, attacker.AllianceId.ToString(), IdName.CategoryEnum.Alliance);
                    }
                    if (mapIntelAttacker.Attacker != null)
                    {
                        mapIntelAttacker.AttackerURL = Converters.GameImageConverter.GetImageUri(mapIntelAttacker.Attacker.Id, Converters.GameImageConverter.ImgType.Character, 64);
                    }

                    IdName faction = null;
                    if (attacker.AllianceId > 0)
                    {
                        faction = new IdName(attacker.AllianceId, attacker.AllianceId.ToString(), IdName.CategoryEnum.Alliance);
                    }
                    else if (attacker.CorporationId > 0)
                    {
                        faction = new IdName(attacker.CorporationId, attacker.CorporationId.ToString(), IdName.CategoryEnum.Corporation);
                    }
                    if (faction != null)
                    {
                        mapIntelAttacker.FactionIconURL = faction == null ? null : Converters.GameImageConverter.GetImageUri(faction.Id, faction.GetCategory() == IdName.CategoryEnum.Corporation ? Converters.GameImageConverter.ImgType.Corporation : Converters.GameImageConverter.ImgType.Alliance, 64);
                    }
                    mapIntelAttacker.Faction = faction;
                    var shipType = Core.Services.DB.InvTypeService.QueryType(attacker.ShipTypeId);
                    if (shipType != null)
                    {
                        mapIntelAttacker.Main = new IdName(shipType.TypeID, shipType.TypeName, IdName.CategoryEnum.InventoryType);
                    }
                    mapIntelAttacker.MainIconURL = Converters.GameImageConverter.GetImageUri(attacker.ShipTypeId, Converters.GameImageConverter.ImgType.Type, 64);
                }
                if (attackers.Any())
                {
                    var ids = new HashSet<int>();
                    foreach (var attacker in attackers)
                    {
                        if (attacker.Attacker != null)
                        {
                            ids.Add(attacker.Attacker.Id);
                        }
                        if (attacker.Faction != null)
                        {
                            ids.Add(attacker.Faction.Id);
                        }
                    }
                    var idNames = await Core.Services.IDNameService.GetByIdsAsync(ids.ToList());
                    if (idNames.NotNullOrEmpty())
                    {
                        var idNameDict = idNames.ToDictionary(p => p.Id);
                        foreach (var attacker in attackers)
                        {
                            if (attacker.Attacker != null && idNameDict.TryGetValue(attacker.Attacker.Id, out var name))
                            {
                                attacker.Attacker.Name = name.Name;
                            }
                            if (attacker.Faction != null && idNameDict.TryGetValue(attacker.Faction.Id, out var factionName))
                            {
                                attacker.Faction.Name = factionName.Name;
                            }
                        }
                    }
                }
                return attackers;
            }
            else
            {
                return null;
            }
        }

    }
}
