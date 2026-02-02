using ESI.NET.Models.Corporation;
using ESI.NET.Models.SSO;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Models.Map;
using Windows.Storage.Streams;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Drawers
{
    public class CharacterDrawer : MapDrawerBase
    {
        private ObservableCollection<AuthorizedCharacterData> _characters;
        /// <summary>
        /// key = 角色id
        /// value=星系id
        /// </summary>
        private Dictionary<int, int> _charactersLocation = new Dictionary<int, int>();
        private Dictionary<int, int> _charactersLocationTemp = new Dictionary<int, int>();

        /// <summary>
        /// key = 角色id
        /// value = img
        /// </summary>
        private Dictionary<int, CanvasBitmap> _characterImgs = new Dictionary<int, CanvasBitmap>();

        /// <summary>
        /// key = 角色id
        /// value = img
        /// </summary>
        private Dictionary<int, byte[]> _characterImgBytes = new Dictionary<int, byte[]>();
        private Dictionary<int, byte[]> _characterImgBytesTemp = new Dictionary<int, byte[]>();

        /// <summary>
        /// EVEStandard的授权
        /// </summary>
        private Dictionary<int, EVEStandard.Models.API.AuthDTO> _charactersAuth = new Dictionary<int, EVEStandard.Models.API.AuthDTO>();
        private object _locker1 = new object();
        private object _locker2 = new object();
        private Timer _timer;
        private EVEStandard.EVEStandardAPI _esi;
        public CharacterDrawer()
        {
            _esi = ESIService.GetDefaultESI2();
            _characters = Services.CharacterService.GetCharacters();
            Services.CharacterService.OnCharacterChanged += CharacterService_OnCharacterChanged;
            _timer = new Timer()
            {
                Interval = 1000,
                AutoReset = false
            };
            _timer.Elapsed += Timer_Elapsed;
            _timer.Start();
        }

        private void CharacterService_OnCharacterChanged(object sender, EventArgs e)
        {
            Helpers.WindowHelper.MainWindow.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                lock (_locker1)
                {
                    _characters = Services.CharacterService.GetCharacters();
                }
                RequstDraw();
            });
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _charactersLocationTemp.Clear();
            _characterImgBytesTemp.Clear();
            HashSet<int> withImgs = new HashSet<int>();
            try
            {
                lock (_locker2)
                {
                    withImgs = _characterImgBytes.Keys.ToHashSet2();
                }
                lock (_locker1)
                {
                    foreach (var character in _characters)
                    {
                        if (!character.IsTokenValid())
                        {
                            _charactersAuth.Remove(character.CharacterID);
                            if (!character.RefreshTokenAsync().Result)
                            {
                                Core.Log.Error($"Refresh character token failed: {character.CharacterID}");
                                continue;
                            }
                        }
                        if (!_charactersAuth.TryGetValue(character.CharacterID, out var auth))
                        {
                            auth = ESIService.ToEVEStandardSSO(character);
                            _charactersAuth.Add(character.CharacterID, auth);
                        }
                        var location = _esi.Location.GetCharacterLocationV1Async(auth).Result;
                        if(_charactersLocationTemp.TryAdd(character.CharacterID, location.Model.SolarSystemId))
                        {
                            if (!withImgs.Contains(character.CharacterID))
                            {
                                var imgBytes = Core.Helpers.HttpHelper.GetByteArrayAsync(Converters.GameImageConverter.GetImageUri(character.CharacterID, Converters.GameImageConverter.ImgType.Character, 64)).Result;
                                if (imgBytes != null)
                                {
                                    _characterImgBytesTemp.Add(character.CharacterID, imgBytes);
                                }
                            }
                        }
                    }
                }
                lock (_locker2)
                {
                    foreach (var character in _charactersLocationTemp)
                    {
                        _charactersLocation.Remove(character.Key);
                        _charactersLocation.Add(character.Key, character.Value);
                    }
                    foreach(var character in _characterImgBytesTemp)
                    {
                        _characterImgBytes.Add(character.Key, character.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                Error($"Update Character Location Error: {ex.Message}");
            }
            finally
            {
                _timer.Start();
            }
        }

        /// <summary>
        /// 记录每个星系需要绘制的角色
        /// key = 星系
        /// value = ids
        /// </summary>
        private Dictionary<int, List<int>> _characterOfDatas = new Dictionary<int, List<int>>();
        public override void Draw(CanvasDrawEventArgs args, Dictionary<int, MapData> allDatas, IEnumerable<MapData> visibleDatas, float zoom, bool drawBorder, Windows.UI.Color mainTextColor)
        {
            float foontSize = zoom > 12 ? 12 : zoom;
            CanvasTextFormat mainTextFormat = new CanvasTextFormat()
            {
                FontSize = foontSize,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center,
            };
            _characterOfDatas.Clear();

            lock (_locker2)
            {
                foreach (var character in _charactersLocation)
                {
                    if (!_characterImgs.TryGetValue(character.Key, out var img))
                    {
                        if (_characterImgBytes.TryGetValue(character.Key, out var bytes))
                        {
                            using (var stream = new InMemoryRandomAccessStream())
                            {
                                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                                {
                                    writer.WriteBytes(bytes);
                                    writer.StoreAsync().Wait();
                                }
                                stream.Seek(0);
                                img = CanvasBitmap.LoadAsync(args.DrawingSession, stream).AsTask().Result;
                                if (img != null)
                                {
                                    _characterImgs.Add(character.Key, img);
                                }
                            }
                        }
                    }
                    if (img != null)
                    {
                        if (!_characterOfDatas.TryGetValue(character.Value, out var datas))
                        {
                            datas = new List<int>();
                            _characterOfDatas.Add(character.Value, datas);
                        }
                        datas.Add(character.Key);
                    }
                }
                foreach(var item in _characterOfDatas)
                {
                    var targetData = allDatas[item.Key];
                    double w = targetData.W;
                    double h = targetData.H;

                    double xStart = targetData.X + w / 2 - (item.Value.Count > 3 ? 3 : item.Value.Count) * w / 2;
                    double xStep = w;
                    double x = targetData.X;
                    double y = targetData.Y - (drawBorder ? targetData.H * 1.2 : targetData.H * 1);
                    for(int i = 0; i< item.Value.Count && i < 3; i++)
                    {
                        args.DrawingSession.DrawImage(_characterImgs[item.Value[i]], new Windows.Foundation.Rect() { X = xStart + i * xStep, Y = y, Width = w, Height = h });
                    }
                    if(item.Value.Count > 3)
                    {
                        args.DrawingSession.DrawText($"+{item.Value.Count - 3}", new System.Numerics.Vector2((float)(xStart + 3 * xStep + w/2), (float)(y + h / 2)), mainTextColor, mainTextFormat);
                    }
                }
            }
        }
        public override void Close()
        {
            Services.CharacterService.OnCharacterChanged -= CharacterService_OnCharacterChanged;
            _timer?.Stop();
            _timer.Dispose();
        }

        public override void Stop()
        {
            _timer?.Stop();
        }
        public override void Start()
        {
            _timer?.Start();
        }
        public override void SetEnable(bool enable)
        {
            base.SetEnable(enable);
            if (enable)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }
    }
}
