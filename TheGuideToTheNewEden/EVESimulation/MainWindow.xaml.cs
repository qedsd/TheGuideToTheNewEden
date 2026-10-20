using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Timers;

namespace EVESimulation
{
    public partial class MainWindow : Window
    {
        private Config _config;
        private CharacterConfig _characterConfig;
        private System.Timers.Timer _simuChatlogTimer;
        private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "Configs.json");
        private List<MapRegion> _mapRegions = new List<MapRegion>();
        private List<MapSystem> _mapSystems = new List<MapSystem>();
        private readonly List<MapSystem> _simuSystems = new List<MapSystem>();
        private readonly Random _random = new Random();
        private bool _isRunning;

        public MainWindow()
        {
            InitializeComponent();
            InitConfig();
            InitMap();
            InitUI();
            _simuChatlogTimer = new System.Timers.Timer();
            _simuChatlogTimer.Elapsed += SimuChatlogTimer_Elapsed;
            _simuChatlogTimer.AutoReset = true;
        }

        private void InitConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(_configPath));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"读取配置失败，将使用默认配置：\n{ex.Message}", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                _config = null;
            }
            NormalizeConfig();
        }

        private void NormalizeConfig()
        {
            if (_config == null)
            {
                _config = new Config();
            }
            if (string.IsNullOrWhiteSpace(_config.RootPath))
            {
                _config.RootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "logs");
            }
            if (_config.CharacterConfigs == null)
            {
                _config.CharacterConfigs = new List<CharacterConfig>();
            }
            if (_config.CharacterConfigs.Count == 0)
            {
                _config.CharacterConfigs.Add(new CharacterConfig()
                {
                    Listener = "QEDSD",
                    ListenerID = 1234567890,
                    ChatChanels = new List<ChatChanel>
                    {
                        new ChatChanel()
                        {
                            ChannelID = "local",
                            ChannelName = "本地",
                            Listener = "QEDSD",
                            ListenerID = 1234567890,
                            SessionStarted = DateTime.Now,
                        }
                    },
                    GameLog = new GameLog()
                    {
                        Listener = "QEDSD",
                        ListenerID = 1234567890,
                        SessionStarted = DateTime.Now,
                    },
                    Speakers = new List<string>() { "QEDSD", "Arya", "Jon" }
                });
            }
            foreach (var character in _config.CharacterConfigs)
            {
                if (character.ChatChanels == null)
                {
                    character.ChatChanels = new List<ChatChanel>();
                }
                if (character.Speakers == null)
                {
                    character.Speakers = new List<string>();
                }
                if (character.GameLog == null)
                {
                    character.GameLog = new GameLog()
                    {
                        Listener = character.Listener,
                        ListenerID = character.ListenerID,
                        SessionStarted = DateTime.Now,
                    };
                }
            }
        }

        private void InitUI()
        {
            LogRootPath.Text = _config.RootPath;
            Characters.ItemsSource = _config.CharacterConfigs.Select(p => p.Listener).ToObservableCollection();
            _characterConfig = _config.CharacterConfigs.FirstOrDefault();
            Characters.SelectedItem = _characterConfig.Listener;
            RegionListBox.ItemsSource = _mapRegions;
            InitCharacterUI();
        }

        private void InitCharacterUI()
        {
            if (_characterConfig == null)
            {
                return;
            }
            CharacterID.Text = _characterConfig.ListenerID.ToString();
            SimuChatlog.IsChecked = _characterConfig.SimuChatlog;
            SimuGamelog.IsChecked = _characterConfig.SimuGamelog;

            var stringBuilder = new StringBuilder();
            foreach (var p in _characterConfig.ChatChanels)
            {
                stringBuilder.AppendLine($"{p.ChannelID},{p.ChannelName},{p.SessionStarted.ToString("yyyy.MM.dd HH:mm:ss")}");
            }
            ChatChanelList.Text = stringBuilder.ToString();
            SpeakerList.Text = string.Join(",", _characterConfig.Speakers);
        }

        private void InitMap()
        {
            try
            {
                _mapRegions = new List<MapRegion>();
                foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "regions.csv")))
                {
                    var array = line.Split(',');
                    if (array.Length < 2 || !int.TryParse(array[0], out var id))
                    {
                        continue;
                    }
                    _mapRegions.Add(new MapRegion()
                    {
                        Id = id,
                        Name = array[1]
                    });
                }
                _mapSystems = new List<MapSystem>();
                foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "systems.csv")))
                {
                    var array = line.Split(',');
                    if (array.Length < 3
                        || !int.TryParse(array[0], out var id)
                        || !int.TryParse(array[2], out var regionId))
                    {
                        continue;
                    }
                    _mapSystems.Add(new MapSystem()
                    {
                        Id = id,
                        Name = array[1],
                        RegionId = regionId
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"读取星系数据失败：\n{ex.Message}", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Characters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Characters.SelectedItem != null)
            {
                var character = _config.CharacterConfigs.FirstOrDefault(p => p.Listener == Characters.SelectedItem.ToString());
                if (character != null)
                {
                    _characterConfig = character;
                    InitCharacterUI();
                }
            }
        }

        private void AutoSimuChatlog_Checked(object sender, RoutedEventArgs e)
        {
            StartAutoSimulation();
        }

        private void AutoSimuChatlog_Unchecked(object sender, RoutedEventArgs e)
        {
            _simuChatlogTimer?.Stop();
        }

        private void StartAutoSimulation()
        {
            _simuChatlogTimer?.Stop();
            if (!_isRunning)
            {
                return;
            }

            _simuSystems.Clear();
            foreach (var item in RegionListBox.SelectedItems)
            {
                if (item is MapRegion region)
                {
                    _simuSystems.AddRange(_mapSystems.Where(p => p.RegionId == region.Id));
                }
            }
            if (_simuSystems.Count == 0)
            {
                AutoSimuChatlog.IsChecked = false;
                MessageBox.Show(this, "请至少选择一个星域。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(AutoSimuChatlogSpan.Text, out var span) || span <= 0)
            {
                AutoSimuChatlog.IsChecked = false;
                MessageBox.Show(this, "自动模拟间隔时间必须大于0。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _simuChatlogTimer.Interval = span * 1000;
            _simuChatlogTimer.Start();
        }

        /// <summary>
        /// 自动聊天频道输出
        /// </summary>
        private void SimuChatlogTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (!_isRunning)
                {
                    return;
                }
                var character = _characterConfig;
                if (character == null || character.ChatChanels == null || character.ChatChanels.Count == 0
                    || character.Speakers == null || character.Speakers.Count == 0 || _simuSystems.Count == 0)
                {
                    return;
                }

                var speaker = character.Speakers[_random.Next(0, character.Speakers.Count)];
                var chanel = character.ChatChanels[_random.Next(0, character.ChatChanels.Count)];
                var system = _simuSystems[_random.Next(0, _simuSystems.Count)];
                var stringBuilder = new StringBuilder();
                if (_random.Next(0, 4) == 0)
                {
                    stringBuilder.Append('*');
                }
                stringBuilder.Append(system.Name);
                stringBuilder.Append(' ');
                stringBuilder.Append(_random.Next(0, 100).ToString());

                chanel.Write(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), speaker, stringBuilder.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartSimulation();
            }
            catch (Exception ex)
            {
                StopSimulation();
                MessageBox.Show(this, $"启动模拟失败：\n{ex.Message}", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartSimulation()
        {
            var listener = (Characters.Text ?? string.Empty).Trim();
            if (listener.Length == 0)
            {
                throw new InvalidOperationException("角色名称不能为空。");
            }
            if (!int.TryParse((CharacterID.Text ?? string.Empty).Trim(), out var listenerId))
            {
                throw new FormatException("角色ID必须是整数。");
            }
            var rootPath = (LogRootPath.Text ?? string.Empty).Trim();
            if (rootPath.Length == 0)
            {
                throw new InvalidOperationException("日志路径不能为空。");
            }

            var simulateChatlog = SimuChatlog.IsChecked == true;
            var simulateGamelog = SimuGamelog.IsChecked == true;

            var chatChanels = new List<ChatChanel>();
            var sessionStarted = DateTime.Now;
            foreach (var rawLine in (ChatChanelList.Text ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var array = rawLine.Trim().Split(',');
                if (array.Length != 3)
                {
                    continue;
                }
                var channelID = array[0].Trim();
                var channelName = array[1].Trim();
                if (channelID.Length == 0 || channelName.Length == 0)
                {
                    continue;
                }
                chatChanels.Add(new ChatChanel()
                {
                    ChannelID = channelID,
                    ChannelName = channelName,
                    SessionStarted = sessionStarted,
                    Listener = listener,
                    ListenerID = listenerId,
                });
            }
            if (simulateChatlog && chatChanels.Count == 0)
            {
                throw new InvalidOperationException("没有有效的聊天频道，格式应为：频道ID,频道名称,会话开始时间");
            }

            var speakers = new List<string>();
            foreach (var speaker in (SpeakerList.Text ?? string.Empty).Split(','))
            {
                var value = speaker.Trim();
                if (value.Length > 0 && !speakers.Contains(value))
                {
                    speakers.Add(value);
                }
            }
            if (simulateChatlog && speakers.Count == 0)
            {
                throw new InvalidOperationException("请至少配置一个发言人。");
            }

            var character = _config.CharacterConfigs.FirstOrDefault(p => p.Listener == listener);
            if (character == null)
            {
                character = (_characterConfig == null ? new CharacterConfig() : _characterConfig.DepthClone<CharacterConfig>()) ?? new CharacterConfig();
                _config.CharacterConfigs.Add(character);
                if (Characters.ItemsSource is ObservableCollection<string> listeners)
                {
                    listeners.Add(listener);
                }
            }
            _characterConfig = character;
            character.Listener = listener;
            character.ListenerID = listenerId;
            character.SimuChatlog = simulateChatlog;
            character.SimuGamelog = simulateGamelog;
            character.ChatChanels = chatChanels;
            character.Speakers = speakers;
            character.GameLog = new GameLog()
            {
                Listener = listener,
                ListenerID = listenerId,
                SessionStarted = DateTime.Now,
            };
            _config.RootPath = rootPath;

            if (simulateChatlog)
            {
                foreach (var chanel in chatChanels)
                {
                    chanel.Init(rootPath);
                }
            }
            if (simulateGamelog)
            {
                character.GameLog.Init(rootPath);
            }

            ChatChanelList.Text = string.Join("\r\n", chatChanels.Select(p => $"{p.ChannelID},{p.ChannelName},{p.SessionStarted.ToString("yyyy.MM.dd HH:mm:ss")}"));
            SpeakerList.Text = string.Join(",", speakers);
            File.WriteAllText(_configPath, JsonConvert.SerializeObject(_config));

            ChatChanels.ItemsSource = null;
            ChatChanels.ItemsSource = character.ChatChanels;
            ChatChanels.SelectedIndex = character.ChatChanels.Count > 0 ? 0 : -1;
            Speakers.ItemsSource = null;
            Speakers.ItemsSource = character.Speakers;
            Speakers.SelectedIndex = character.Speakers.Count > 0 ? 0 : -1;

            _isRunning = true;
            Title = $"EVE - {listener}";
            ChatSpeakButton.IsEnabled = simulateChatlog;
            ChatOpenLogButton.IsEnabled = simulateChatlog;
            GamelogSpeakButton.IsEnabled = simulateGamelog;
            GamelogOpenLogButton.IsEnabled = simulateGamelog;
            StartButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Visible;
            SettingPanel.IsEnabled = false;
            AutoSimuChatlog.IsChecked = false;
            RunningGrid.IsEnabled = true;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopSimulation();
        }

        private void StopSimulation()
        {
            _isRunning = false;
            _simuChatlogTimer?.Stop();
            AutoSimuChatlog.IsChecked = false;
            ChatSpeakButton.IsEnabled = true;
            ChatOpenLogButton.IsEnabled = true;
            GamelogSpeakButton.IsEnabled = true;
            GamelogOpenLogButton.IsEnabled = true;

            if (_characterConfig != null)
            {
                if (_characterConfig.ChatChanels != null)
                {
                    foreach (var chanel in _characterConfig.ChatChanels)
                    {
                        chanel.Dispose();
                    }
                }
                _characterConfig.GameLog?.Dispose();
            }

            StartButton.Visibility = Visibility.Visible;
            StopButton.Visibility = Visibility.Collapsed;
            SettingPanel.IsEnabled = true;
            RunningGrid.IsEnabled = false;
            Title = "EVE";
        }

        private void Button_ChatSpeak_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var chanel = ChatChanels.SelectedItem as ChatChanel;
                if (chanel == null)
                {
                    MessageBox.Show(this, "请先选择要发言的聊天频道。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var speaker = (Speakers.SelectedItem as string) ?? (Speakers.Text ?? string.Empty).Trim();
                if (speaker.Length == 0)
                {
                    MessageBox.Show(this, "请先选择或输入发言人。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                chanel.Write(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), speaker, SpeakContent.Text ?? string.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"发言失败：\n{ex.Message}", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_Gamelog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_characterConfig?.GameLog == null)
                {
                    MessageBox.Show(this, "游戏日志未初始化，请先点击“开始”。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _characterConfig.GameLog.Write(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), GamelogContent.Text ?? string.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"写入游戏日志失败：\n{ex.Message}", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_ChatOpenLog_Click(object sender, RoutedEventArgs e)
        {
            OpenLogFile((ChatChanels.SelectedItem as ChatChanel)?.FilePath, "聊天频道日志");
        }

        private void Button_GamelogOpenLog_Click(object sender, RoutedEventArgs e)
        {
            OpenLogFile(_characterConfig?.GameLog?.FilePath, "游戏日志");
        }

        private void OpenLogFile(string filePath, string name)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    MessageBox.Show(this, $"{name}文件不存在，请先点击“开始”。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"打开{name}失败：\n{ex.Message}", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
