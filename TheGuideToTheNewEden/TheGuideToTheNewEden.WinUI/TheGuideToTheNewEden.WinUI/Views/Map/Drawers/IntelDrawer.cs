using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Octokit;
using TheGuideToTheNewEden.WinUI.Models.Map;
using TheGuideToTheNewEden.WinUI.Views.Map.Tools;
using Vanara.PInvoke;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;
using static TheGuideToTheNewEden.WinUI.Views.Map.Drawers.IntelDrawer;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Drawers
{
    public class IntelDrawer : MapDrawerBase
    {
        private object _locker = new object();
        private Dictionary<int, byte[]> _shipsImgBytes = new Dictionary<int, byte[]>();
        private Dictionary<int, CanvasBitmap> _shipsImgs = new Dictionary<int, CanvasBitmap>();
        private Dictionary<int, MapDataIntelInfo> _mapDataIntelInfos = new Dictionary<int, MapDataIntelInfo>();
        private bool _enable = false;

        public override void Draw(CanvasControl sender, CanvasDrawEventArgs args, Dictionary<int, MapData> allDatas, IEnumerable<MapData> visibleDatas, float zoom, bool drawBorder, Color mainTextColor)
        {
            List<MapCanvas.MapGraphBase> newMapGraphs = new List<MapCanvas.MapGraphBase>();
            foreach (var mapDataIntelInfo in _mapDataIntelInfos)
            {
                int intelWeight = mapDataIntelInfo.Value.GetWeigh();
                if (intelWeight > 0)
                {
                    var targetData = allDatas[mapDataIntelInfo.Key];
                    targetData.Active = true;
                    if (mapDataIntelInfo.Value.MapGraph == null)
                    {
                        mapDataIntelInfo.Value.MapGraph = new MapCanvas.CircleMapGraph()
                        {
                            CenterDataId = mapDataIntelInfo.Key,
                            Color = Windows.UI.Color.FromArgb(100, Colors.Red.R, Colors.Red.G, Colors.Red.B)
                        };
                        newMapGraphs.Add(mapDataIntelInfo.Value.MapGraph);
                    }
                    mapDataIntelInfo.Value.MapGraph.Margin = targetData.W * 4 * ((intelWeight > 50 ? 50 : intelWeight) / 50f);
                    var ships = mapDataIntelInfo.Value.GetShips();
                    int channels = mapDataIntelInfo.Value.GetChannelCount();
                    if (ships.Any() || channels > 0)
                    {
                        if (zoom > 12)
                        {
                            double w = targetData.W;
                            double h = targetData.H;

                            double xStart = targetData.X + targetData.W * (drawBorder ? 1.2 : 1);
                            double xStep = w;
                            double y = targetData.Y + (targetData.H - h) / 2;
                            if(channels > 0)
                            {
                                CanvasTextFormat channelCountTextFormat = new CanvasTextFormat()
                                {
                                    FontSize = (int)(_mapCanvas.GetMainFootSize()) - 2,
                                    HorizontalAlignment = CanvasHorizontalAlignment.Center,
                                    VerticalAlignment = CanvasVerticalAlignment.Top,
                                };
                                double xPos = targetData.X + w / 2;
                                args.DrawingSession.DrawText($"📢 {channels}  🕒 {mapDataIntelInfo.Value.GetChannelLatestTime()}", new System.Numerics.Vector2((float)xPos, (float)(targetData.Y + h * 1.2 + _mapCanvas.GetMainFootSize() + 2)), _mapCanvas.GetActiveColor(Microsoft.UI.Colors.OrangeRed, targetData.Active), channelCountTextFormat);
                            }
                            for (int i = 0; i < ships.Count; i++)
                            {
                                double targetX = xStart + i * xStep;
                                if (_mapCanvas.FindMapData(targetX, y) != null || (i != ships.Count - 1 && _mapCanvas.FindMapData(targetX + 2 * xStep, y) != null))
                                {
                                    CanvasTextFormat moreTextFormat = new CanvasTextFormat()
                                    {
                                        FontSize = (int)(_mapCanvas.GetMainFootSize() + 4),
                                        HorizontalAlignment = CanvasHorizontalAlignment.Left,
                                        VerticalAlignment = CanvasVerticalAlignment.Center,
                                    };
                                    args.DrawingSession.DrawText($"+{ships.Skip(i).Sum(p => p.Count)}", new System.Numerics.Vector2((float)(targetX + w * 0.2), (float)(y + h * 0.5)), _mapCanvas.GetActiveColor(Microsoft.UI.Colors.OrangeRed, targetData.Active), moreTextFormat);
                                    break;
                                }
                                if (ships[i].Img != null)
                                {
                                    args.DrawingSession.DrawImage(ships[i].Img, new Windows.Foundation.Rect() { X = targetX, Y = y, Width = w, Height = h });
                                }

                                if (ships[i].Count > 1)
                                {
                                    CanvasTextFormat textFormat1 = new CanvasTextFormat()
                                    {
                                        FontSize = _mapCanvas.GetMainFootSize() - 2,
                                        HorizontalAlignment = CanvasHorizontalAlignment.Right,
                                        VerticalAlignment = CanvasVerticalAlignment.Top,
                                    };
                                    args.DrawingSession.DrawText($"×{ships[i].Count}", new System.Numerics.Vector2((float)(targetX + w * 0.9), (float)(y)), _mapCanvas.GetActiveColor(Microsoft.UI.Colors.White, targetData.Active), textFormat1);
                                }
                                if (zoom > 16)
                                {
                                    CanvasTextFormat textFormat2 = new CanvasTextFormat()
                                    {
                                        FontSize = _mapCanvas.GetMainFootSize() - 2,
                                        HorizontalAlignment = CanvasHorizontalAlignment.Right,
                                        VerticalAlignment = CanvasVerticalAlignment.Bottom,
                                    };
                                    args.DrawingSession.DrawText($"🕒 {ships[i].Msg.Elapsed}", new System.Numerics.Vector2((float)(targetX + w * 0.9), (float)(y + h)), _mapCanvas.GetActiveColor(Microsoft.UI.Colors.White, targetData.Active), textFormat2);
                                }
                            }
                        }
                        else
                        {
                            CanvasTextFormat moreTextFormat = new CanvasTextFormat()
                            {
                                FontSize = (int)(_mapCanvas.GetMainFootSize() + 4),
                                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                                VerticalAlignment = CanvasVerticalAlignment.Center,
                            };
                            args.DrawingSession.DrawText($"+{ships.Sum(p => p.Count) + channels}", new System.Numerics.Vector2((float)(targetData.X + targetData.W * 1.2), (float)(targetData.Y + targetData.H * 0.5)), _mapCanvas.GetActiveColor(Microsoft.UI.Colors.OrangeRed, targetData.Active), moreTextFormat);
                        }
                    }
                }
            }
            if (newMapGraphs.Any())
            {
                _mapCanvas.AddMapGraph(newMapGraphs, false);
            }
            _mapCanvas.UpdateMapGraph();
        }
        public override bool GetEnable()
        {
            return _enable;
        }

        public override void SetEnable(bool enable)
        {
            _enable = enable;
        }

        public async void Add(int mapDataId, int shipId, int attackerId, MapIntelMsg msg)
        {
            CanvasBitmap shipImg = null;
            byte[] imgBytes = null;
            if (shipId > 0)
            {
                lock (_locker)
                {
                    _shipsImgs.TryGetValue(shipId, out shipImg);
                }
                if (shipImg == null)
                {
                    imgBytes = await Core.Helpers.HttpHelper.GetByteArrayAsync(Converters.GameImageConverter.GetImageUri(shipId, Converters.GameImageConverter.ImgType.Type, 64));
                }
            }
            if(shipImg == null && imgBytes != null)
            {
                using (var stream = new InMemoryRandomAccessStream())
                {
                    using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                    {
                        writer.WriteBytes(imgBytes);
                        await writer.StoreAsync();
                    }
                    stream.Seek(0);
                    shipImg = await CanvasBitmap.LoadAsync(_mapCanvas._canvasControl.Device, stream);
                }
            }
           
            lock (_locker)
            {
                if (!_mapDataIntelInfos.TryGetValue(mapDataId, out var mapDataIntelInfo))
                {
                    mapDataIntelInfo = new MapDataIntelInfo()
                    {
                        MapDataId = mapDataId,
                    };
                    _mapDataIntelInfos.Add(mapDataId, mapDataIntelInfo);
                }
                mapDataIntelInfo.AddShip(shipId, attackerId, msg, shipImg);
            }
            RequstDraw();
        }
        public void Add(int mapDataId, MapIntelChannelMsg msg)
        {
            lock (_locker)
            {
                if (!_mapDataIntelInfos.TryGetValue(mapDataId, out var mapDataIntelInfo))
                {
                    mapDataIntelInfo = new MapDataIntelInfo()
                    {
                        MapDataId = mapDataId,
                    };
                    _mapDataIntelInfos.Add(mapDataId, mapDataIntelInfo);
                }
                mapDataIntelInfo.AddChannelContent(msg);
            }
            RequstDraw();
        }
        public void Remove(int mapDataId, int shipId, int attackerId, bool draw = false)
        {
            if (_mapDataIntelInfos.TryGetValue(mapDataId, out var mapDataIntelInfo))
            {
                mapDataIntelInfo.RemoveShip(shipId, attackerId);//如果ship为0也不移除对象，复用对象
                if (mapDataIntelInfo.GetWeigh() > 0)
                {
                    _mapCanvas.RemoveMapGraph(mapDataIntelInfo.MapGraph, false);
                    mapDataIntelInfo.MapGraph = null;
                }
            }
            if (draw)
            {
                RequstDraw();
            }
        }
        public void Remove(int mapDataId, MapIntelChannelMsg msg, bool draw = false)
        {
            if (_mapDataIntelInfos.TryGetValue(mapDataId, out var mapDataIntelInfo))
            {
                mapDataIntelInfo.RemoveChannelContent(msg);
                if (mapDataIntelInfo.GetWeigh() > 0)
                {
                    _mapCanvas.RemoveMapGraph(mapDataIntelInfo.MapGraph, false);
                    mapDataIntelInfo.MapGraph = null;
                }
            }
            if (draw)
            {
                RequstDraw();
            }
        }
        public void Clear()
        {
            foreach(var info in _mapDataIntelInfos)
            {
                info.Value.ClearShip();
                info.Value.ClearChannelContent();
                _mapCanvas.RemoveMapGraph(info.Value.MapGraph, false);
                info.Value.MapGraph = null;
            }
            RequstDraw();
        }
        public void Clear(int mapDataId, bool onlyClearChannle)
        {
            if (_mapDataIntelInfos.TryGetValue(mapDataId, out var mapDataIntelInfo))
            {
                if (!onlyClearChannle)
                {
                    mapDataIntelInfo.ClearShip();
                }
                mapDataIntelInfo.ClearChannelContent();
                if(mapDataIntelInfo.GetWeigh() == 0)
                {
                    _mapCanvas.RemoveMapGraph(mapDataIntelInfo.MapGraph, false);
                    mapDataIntelInfo.MapGraph = null;
                }
            }
            RequstDraw();
        }

        public override void Start()
        {
            _enable = true;
        }

        public override void Stop()
        {
            _enable = false;
        }
        public override void Close()
        {
            _shipsImgBytes.Clear();
            foreach(var info in _mapDataIntelInfos)
            {
                info.Value.Dispose();
            }
            foreach(var ship in _shipsImgs)
            {
                ship.Value.Dispose();
            }
            _shipsImgs.Clear();
            _mapDataIntelInfos.Clear();
        }
        internal class MapDataIntelInfo
        {
            public int MapDataId { get; set; }
            #region ZKB
            private Dictionary<int, MapDataShip> _ships = new Dictionary<int, MapDataShip>();
            public void AddShip(int id, int attackerId, MapIntelMsg msg, CanvasBitmap img = null)
            {
                if(!_ships.TryGetValue(id, out var mapDataShip))
                {
                    mapDataShip = new MapDataShip()
                    {
                        ShipId = id,
                        Img = img,
                        Msg = msg
                    };
                    _ships.Add(id, mapDataShip);
                }
                mapDataShip.Add(attackerId);
            }
            public void RemoveShip(int id, int attackerId)
            {
                if (_ships.TryGetValue(id, out var mapDataShip))
                {
                    mapDataShip.Remove(attackerId);
                    if (mapDataShip.Count <= 0)
                    {
                        _ships.Remove(id);
                    }
                }
            }
            public void ClearShip()
            {
                foreach (var ship in _ships.Values)
                {
                    ship.Clear();
                }
                _ships.Clear();
            }
            public List<MapDataShip> GetShips()
            {
                return _ships.Values.ToList();
            }
            #endregion
            #region Channel
            private List<MapIntelChannelMsg> ChannelMsgs = new List<MapIntelChannelMsg>();
            public void AddChannelContent(MapIntelChannelMsg msg)
            {
                ChannelMsgs.Add(msg);
            }
            public void RemoveChannelContent(MapIntelChannelMsg msg)
            {
                ChannelMsgs.Remove(msg);
            }
            public double GetChannelLatestTime()
            {
                if (ChannelMsgs.Any())
                {
                    return ChannelMsgs.Min(p => p.Elapsed);
                }
                return -1;
            }
            public int GetChannelCount()
            {
                return ChannelMsgs.Count;
            }
            public void ClearChannelContent()
            {
                ChannelMsgs.Clear();
            }
            #endregion
            public int GetWeigh()
            {
                int weight = 0;
                foreach (var ship in _ships.Values)
                {
                    weight += ship.Count;
                }
                weight += ChannelMsgs.Count * 5;
                return weight;
            }
            public void Dispose()
            {
                foreach(var ship in _ships.Values)
                {
                    ship.Img = null;
                }
                _ships.Clear();
            }

            public MapCanvas.CircleMapGraph MapGraph { get; set; }
        }
        internal class MapDataShip
        {
            private HashSet<int> _attackerIds = new HashSet<int>();
            public int ShipId { get; set; }
            public int Count { get;private set; }
            public CanvasBitmap Img { get; set; }
            public MapIntelMsg Msg { get; set; }
            public void Add(int attackerId)
            {
                if (!_attackerIds.Contains(attackerId))
                {
                    _attackerIds.Add(attackerId);
                    Count++;
                }
            }
            public void Remove(int attackerId)
            {
                if (_attackerIds.Remove(attackerId))
                {
                    Count--;
                }
            }
            public void Clear()
            {
                _attackerIds.Clear();
                Count = 0;
            }
        } 

        internal class MapDataChannel
        {
            public MapIntelMsg Msg { get; set; }
            public List<Core.Models.EarlyWarningContent> Contents { get; set; }
        }
    }
}
