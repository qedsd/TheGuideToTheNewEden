using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Wins;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace TheGuideToTheNewEden.WinUI.Models
{
    public class GameLogMonitor
    {
        private GameLogInfo _gameLogInfo;
        private GameLogItemConfig _config;
        private bool _running;
        private Core.Models.GameLogItem _gameLogItem;
        private GaemLogMsgWindow _msgWindow;
        private Windows.Media.Playback.MediaPlayer _mediaPlayer;
        private MediaSource _mediaSource;
        private System.Timers.Timer _timer;

        /// <summary>
        /// 最近一次触发更新时间
        /// </summary>
        private DateTime _lastTriggeredTime = DateTime.MaxValue;
        private object _locker = new object();

        public event GameLogItem.ContentUpdate OnContentUpdate;

        public GameLogMonitor(GameLogInfo gameLogInfo, GameLogItemConfig config, string filePath)
        {
            _gameLogInfo = gameLogInfo;
            _config = config;
            _gameLogItem = new GameLogItem(gameLogInfo, config, filePath);
        }

        public void Start()
        {
            if (Core.Services.ObservableFileService.Add(_gameLogItem))
            {
                _running = true;
                _gameLogItem.OnContentUpdate += GameLogItem_OnContentUpdate;
                if (_config.WindowNotify)
                {
                    _msgWindow = GetMsgWindow();
                }
                else
                {
                    _msgWindow?.Close();
                    _msgWindow = null;
                }
                if (_config.SoundNotify)
                {
                    Uri uri = new Uri(string.IsNullOrEmpty(_config.SoundFile) ?
                                      System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "default.mp3") :
                                      _config.SoundFile);
                    _mediaSource = MediaSource.CreateFromUri(uri);
                    _mediaPlayer??= new Windows.Media.Playback.MediaPlayer()
                    {
                        IsLoopingEnabled = _config.RepeatSound
                    };
                    _mediaPlayer.Source = _mediaSource;
                }
                else
                {
                    _mediaSource?.Dispose();
                    _mediaPlayer?.Dispose();
                    _mediaSource = null;
                    _mediaPlayer = null;
                }
                if(_config.MonitorMode == 1)
                {
                    if(_timer == null)
                    {
                        _timer = new System.Timers.Timer()
                        {
                            AutoReset = false,
                            Interval = 1000,
                        };
                        _timer.Elapsed += DelayTimer_Elapsed;
                    }
                    _timer.Start();
                }
            }
            else
            {
                throw new Exception("Add To ObservableFileService Falied");
            }
        }

        public void Stop()
        {
            _running = false;
            _gameLogItem.OnContentUpdate -= GameLogItem_OnContentUpdate;
            Core.Services.ObservableFileService.Remove(_gameLogItem);
            _msgWindow?.Hide();
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Source = null;
            }
            _mediaSource?.Dispose();
            _mediaSource = null;
            _timer?.Stop();
        }
        private void GameLogItem_OnContentUpdate(GameLogItem item, IEnumerable<Core.Models.EVELogs.GameLogContent> news)
        {
            OnContentUpdate?.Invoke(item, news);
            foreach (var msg in news)
            {
                if (msg.Important)
                {
                    if (_config.MonitorMode == 0)
                    {
                        Notify(msg.SourceContent);
                    }
                    else
                    {
                        //刷新时间
                        lock (_locker)
                        {
                            _lastTriggeredTime = DateTime.Now;
                        }
                    }
                }
            }
            
        }
        private GaemLogMsgWindow GetMsgWindow()
        {
            if(_msgWindow == null)
            {
                _msgWindow = new GaemLogMsgWindow(_gameLogInfo.ListenerName, _gameLogInfo.ListenerID);
                _msgWindow.SetTitle($"{Helpers.ResourcesHelper.GetString("ShellPage_GameLogMonitor")}-{_gameLogInfo.ListenerName}-{_config.ConfigName}");
                _msgWindow.OnHided += MessageWindow_OnHided; ;
                _msgWindow.OnShowGameButtonClick += MessageWindow_OnShowGameButtonClick; ;
            }
            return _msgWindow;
        }

        private void MessageWindow_OnShowGameButtonClick(GaemLogMsgWindow gaemLogMsgWindow)
        {
            var hwnd = Helpers.WindowHelper.GetGameHwndByCharacterName(gaemLogMsgWindow.ListenerName);
            if (hwnd != IntPtr.Zero)
            {
                Helpers.WindowHelper.SetForegroundWindow_Click(hwnd);
            }
        }

        private void MessageWindow_OnHided(GaemLogMsgWindow messageWindow)
        {
            _mediaPlayer?.Pause();
        }
        private void DelayTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            lock (_locker)
            {
                try
                {
                    if ((DateTime.Now - _lastTriggeredTime).TotalSeconds > _config.DisappearDelay)
                    {
                        _lastTriggeredTime = DateTime.MaxValue;
                        Notify(Helpers.ResourcesHelper.GetString("GameLogMonitorPage_DelayExpireTip"));
                    }
                }
                catch(Exception ex)
                {
                    ClientServiceHelper.GetRequiredService<PageNavigationService>().ShowMsg("GameLogMonitor", $"Delay Timer Error: {ex.Message}", Controls.InfoBarControl.InfoType.Error, false);
                }
                finally
                {
                    _timer.Start();
                }
            }
        }
        private void Notify(string msg)
        {
            _msgWindow?.Show(msg);
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Position = TimeSpan.Zero;
                _mediaPlayer.Play();
            }
            if (_config.SystemNotify)
            {
                Notifications.GameLogMonitorToast.SendToast(_gameLogInfo.ListenerID, _gameLogInfo.ListenerName, msg);
            }
        }
        public string GetGUID()
        {
            return _config.GUID;
        }
        public void Dispose()
        {
            Stop();
            _msgWindow?.Close();
            _msgWindow = null;
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Source = null;
                _mediaPlayer.Dispose();
            }
            _mediaSource?.Dispose();
            _mediaSource = null;
            _mediaPlayer = null;
            if(_timer != null)
            {
                _timer.Elapsed -= DelayTimer_Elapsed;
                _timer.Dispose();
                _timer = null;
            }
        }
    }
}
