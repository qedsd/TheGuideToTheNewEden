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
using TheGuideToTheNewEden.WinUI.Services;
using static Vanara.PInvoke.Kernel32.REASON_CONTEXT;

namespace TheGuideToTheNewEden.WinUI.ViewModels.Map
{
    public class IntelToolViewModel : BaseViewModel
    {
        private bool _running;
        public bool Running { get => _running; set => SetProperty(ref _running, value); }

        private bool _msgSelected = false;
        public bool MsgSelected
        {
            get => _msgSelected;
            set
            {
                SetProperty(ref _msgSelected, value);
            }
        }

        public MapIntelConfig Config { get; set; }
        public ObservableCollection<MapIntelMsg> IntelMsgs { get; set; }
        public List<MapIntelMsg> AllIntelMsgs { get; set; }

        private MapCanvas _mapCanvas;
        private Dictionary<int, MapData> _systemDatas;
        private Dictionary<int, SovData> _sovDatas;
        private DispatcherTimer _timer;


        #region 过滤器
        private MapIntelFilter _exclusionsFilter = new MapIntelFilter() { EmptyAlwayContains = false };
        private MapIntelFilter _inclusionsFilter = new MapIntelFilter() { EmptyAlwayContains = true };
        #endregion
        public IntelToolViewModel()
        {
            Config = Services.Settings.MapSettingService.Current.GetIntel();
            IntelMsgs = new ObservableCollection<MapIntelMsg>();
            AllIntelMsgs = new List<MapIntelMsg>();
            RefreshChannel();
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
            foreach(var channel in Channels)
            {
                if (channel.IsChecked == true)
                {
                    Config.Channels.Add(channel.Data);
                }
                else
                {
                    Config.Channels.Remove(channel.Data);
                }
            }
            Services.Settings.MapSettingService.Current.SaveIndel(Config);
            ShowWaiting();
            bool started = await Start();
            HideWaiting();
            if (started)
            {
                Running = true;
                MsgSelected = true;
            }
        });
        public ICommand StopCommand => new RelayCommand(() =>
        {
            Stop();
            Running = false;
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
                _exclusionsFilter.Clear();
                _inclusionsFilter.Clear();
                _exclusionsFilter.Add(Config.Exclusions);
                _inclusionsFilter.Add(Config.Inclusions);

                if (Config.ZKB)
                {
                    await Core.Services.ZKBStreamService.Current.Sub();
                    Core.Services.ZKBStreamService.Current.OnError += KillStream_OnError;
                    Core.Services.ZKBStreamService.Current.OnMessage += KillStream_OnMessage;
                }
                
                _targetChannelListeners.Clear();
                foreach(var channel in Channels.Where(p => p.IsChecked == true))
                {
                    _targetChannelListeners.Add(channel.Data);
                }
                if (_targetChannelListeners.Count > 0)
                {
                    ChannelIntelManager.Instance.OnIgnoreJumpsIntelUpdate += Channel_OnIgnoreJumpsIntelUpdate;
                    ChannelIntelManager.Instance.ListenChannelIntel(_targetChannelListeners);
                }
                StartTimer();
                _mapCanvas.IntelDrawer.SetEnable(true);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                StopTimer();
                ShowError(ex);
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
            var expiredMsgs = AllIntelMsgs.Where(p => p.Elapsed > (p.Type == MapIntelType.KB ? Config.ZKBDuration : Config.ChannelDuration) / 60).ToList();
            if (expiredMsgs.Any())
            {
                foreach(var expiredMsg in expiredMsgs)
                {
                    if(expiredMsg.Type == MapIntelType.KB)
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
                    }
                    else
                    {
                        _mapCanvas.IntelDrawer.Remove(expiredMsg.System.SolarSystemID, expiredMsg as MapIntelChannelMsg);
                    }
                        AllIntelMsgs.Remove(expiredMsg);
                    IntelMsgs.Remove(expiredMsg);
                    _systemDatas[expiredMsg.System.SolarSystemID].RemoveDataExt(expiredMsg.GUID);
                }
            }
            _mapCanvas.Draw();
        }

        private void Stop()
        {
            Core.Services.ZKBStreamService.Current.UnSub();
            Core.Services.ZKBStreamService.Current.OnError -= KillStream_OnError;
            Core.Services.ZKBStreamService.Current.OnMessage -= KillStream_OnMessage;
            ChannelIntelManager.Instance.OnIgnoreJumpsIntelUpdate -= Channel_OnIgnoreJumpsIntelUpdate;
            ChannelIntelManager.Instance.UnListenChannelIntel();
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
                        if(_exclusionsFilter.Contains(IdName.CategoryEnum.SolarSystem ,detail.SolarSystemId) || !_inclusionsFilter.Contains(IdName.CategoryEnum.SolarSystem, detail.SolarSystemId))
                        {
                            return;
                        }
                        var attackerIds = detail.Attackers.Select(p => p.CharacterId).ToArray();
                        if (_exclusionsFilter.Contains(IdName.CategoryEnum.Character, attackerIds) || !_inclusionsFilter.Contains(IdName.CategoryEnum.Character, attackerIds))
                        {
                            return;
                        }
                        var attackerCorpIds = detail.Attackers.Select(p => p.CorporationId).ToArray();
                        if (_exclusionsFilter.Contains(IdName.CategoryEnum.Corporation, attackerCorpIds) || !_inclusionsFilter.Contains(IdName.CategoryEnum.Corporation, attackerCorpIds))
                        {
                            return;
                        }
                        var attackerAllianceIds = detail.Attackers.Select(p => p.AllianceId).ToArray();
                        if (_exclusionsFilter.Contains(IdName.CategoryEnum.Alliance, attackerAllianceIds) || !_inclusionsFilter.Contains(IdName.CategoryEnum.Alliance, attackerAllianceIds))
                        {
                            return;
                        }
                        var attackerShipIds = detail.Attackers.Select(p => p.ShipTypeId).ToArray();
                        if (_exclusionsFilter.Contains(IdName.CategoryEnum.InventoryType, attackerShipIds) || !_inclusionsFilter.Contains(IdName.CategoryEnum.InventoryType, attackerShipIds))
                        {
                            return;
                        }
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
                IntelMsgs.Insert(0, msg);
                AllIntelMsgs.Add(msg);
                if (msg.Type == MapIntelType.KB)
                {
                    _systemDatas[msg.System.SolarSystemID].AddShip(msg.KB.SKBDetail.Attackers.Where(p=>p.CharacterId > 0).Select(p => p.ShipTypeId));
                    foreach (var attacker in msg.Attackers)
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
                else
                {
                    if(msg is MapIntelChannelMsg channelMsg)
                    {
                        if (channelMsg.ChhannelContent.IntelType == Core.Enums.IntelChatType.Intel)
                        {
                            _systemDatas[msg.System.SolarSystemID].AddMsg($"[{channelMsg.ChhannelContent.Time.ToString("HH:mm:ss")}] {channelMsg.ChhannelContent.Content}");
                            _mapCanvas.IntelDrawer.Add(msg.System.SolarSystemID, channelMsg);
                        }
                        else
                        {
                            _mapCanvas.IntelDrawer.Clear(msg.System.SolarSystemID, Config.ClearChannelMode == 1);
                            _systemDatas[msg.System.SolarSystemID].RemoveMsg();
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

        #region ChannelIntel
        private HashSet<string> _targetChannelListeners = new HashSet<string>();
        private ObservableCollection<Core.Models.CheckableModel<string>> _channels = new ObservableCollection<Core.Models.CheckableModel<string>>();

        public ObservableCollection<Core.Models.CheckableModel<string>> Channels
        {
            get => _channels;
            set => SetProperty(ref _channels, value);
        }
        public ICommand RefreshChannelCommand => new RelayCommand(() =>
        {
            RefreshChannel();
        });
        private void RefreshChannel()
        {
            Channels.Clear();
            var active = ChannelIntelManager.Instance.GetActiveListeners();
            foreach (var listener in active)
            {
                Channels.Add(new Core.Models.CheckableModel<string>(listener, Config.Channels.Contains(listener)));
            }
        }
        private void Channel_OnIgnoreJumpsIntelUpdate(Core.Models.ChannelIntel.ChannelIntelObserver observer, IEnumerable<Core.Models.EarlyWarningContent> news)
        {
            if (_targetChannelListeners.Contains(observer.ChatChanelInfo.Listener))
            {
                foreach (var content in news)
                {
                    if (_systemDatas.TryGetValue(content.SolarSystemId, out var data) && data.Enable)
                    {
                        if (_exclusionsFilter.Contains(IdName.CategoryEnum.SolarSystem, content.SolarSystemId) || !_inclusionsFilter.Contains(IdName.CategoryEnum.SolarSystem, content.SolarSystemId))
                        {
                            return;
                        }
                        var system = Core.Services.DB.MapSolarSystemService.Query(content.SolarSystemId);
                        var region = Core.Services.DB.MapRegionService.Query(system.RegionID);
                        MapIntelChannelMsg msg = new MapIntelChannelMsg()
                        {
                            Type = MapIntelType.Channel,
                            System = system,
                            Region = region,
                            Time = DateTime.Now,
                            ChhannelContent = content,
                        };
                        InserMsg(msg);
                    }
                }
            }
        }
        #endregion
    }

    public class MapDataZKBExt: MapDataExt
    {
        public MapDataZKBExt(MapIntelMsg mapIntelMsg)
        {
            DataType = mapIntelMsg.Type == MapIntelType.KB ? MapDataType.ZKBIntel : MapDataType.ChannelIntel;
        }
        public MapIntelMsg Msg { get; set; }
    }

    public class MapDataShipExt : MapDataExt
    {
        public MapDataShipExt()
        {
            DataType = MapDataType.Ship;
        }
        public Dictionary<int, ShipItem> Ships { get; set; } = new Dictionary<int, ShipItem>();
    }

    public class MapIntelFilter
    {
        public HashSet<int> Characters { get; set; } = new HashSet<int>();
        public HashSet<int> Corporations { get; set; } = new HashSet<int>();
        public HashSet<int> Alliances { get; set; } = new HashSet<int>();
        public HashSet<int> SolarSystems { get; set; } = new HashSet<int>();
        public HashSet<int> Regions { get; set; } = new HashSet<int>();
        public HashSet<int> Types { get; set; } = new HashSet<int>();

        /// <summary>
        /// True:若数据为空则永远返回包含在内
        /// False:若数据为空则永远返回不包含在内
        /// </summary>
        public bool EmptyAlwayContains { get; set; }

        public void Clear()
        {
            Characters.Clear();
            Corporations.Clear();
            Alliances.Clear();
            SolarSystems.Clear();
            Regions.Clear();
            Types.Clear();
        }
        public void Add(IEnumerable<IdName> idNames)
        {
            foreach(var idName in idNames)
            {
                switch (idName.GetCategory())
                {
                    case IdName.CategoryEnum.Character:Characters.Add(idName.Id);break;
                    case IdName.CategoryEnum.Corporation:Corporations.Add(idName.Id); break;
                    case IdName.CategoryEnum.Alliance: Alliances.Add(idName.Id); break;
                    case IdName.CategoryEnum.SolarSystem: SolarSystems.Add(idName.Id); break;
                    case IdName.CategoryEnum.Region: Regions.Add(idName.Id); break;
                    case IdName.CategoryEnum.InventoryType: Types.Add(idName.Id); break;
                }
            }
        }

        public bool Contains(IdName idName)
        {
            return Contains(idName.GetCategory(), idName.Id);
        }
        public bool Contains(IdName.CategoryEnum category, int id)
        {
            switch (category)
            {
                case IdName.CategoryEnum.Character: return Characters.Count == 0 ? EmptyAlwayContains : Characters.Contains(id);
                case IdName.CategoryEnum.Corporation: return Corporations.Count == 0 ? EmptyAlwayContains : Corporations.Contains(id);
                case IdName.CategoryEnum.Alliance: return Alliances.Count == 0 ? EmptyAlwayContains : Alliances.Contains(id);
                case IdName.CategoryEnum.SolarSystem: return SolarSystems.Count == 0 ? EmptyAlwayContains : SolarSystems.Contains(id);
                case IdName.CategoryEnum.Region: return Regions.Count == 0 ? EmptyAlwayContains : Regions.Contains(id);
                case IdName.CategoryEnum.InventoryType: return Types.Count == 0 ? EmptyAlwayContains : Types.Contains(id);
                default: return false;
            }
        }
        public bool Contains(IdName.CategoryEnum category, IEnumerable<int> ids)
        {
            foreach(var id in ids)
            {
                if(Contains(category, id))
                {
                    return true;
                }
            }
            return false;
        }
        public bool Contains(IEnumerable<IdName> idNames)
        {
            foreach (var idName in idNames)
            {
                if (Contains(idName))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
