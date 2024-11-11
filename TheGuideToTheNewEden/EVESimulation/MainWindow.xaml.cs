using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EVESimulation
{
    public partial class MainWindow : Window
    {
        private Config _config;
        private CharacterConfig _characterConfig;
        private System.Timers.Timer _simuChatlogTimer;
        private Dictionary<string, int> _speakerIds = new Dictionary<string, int>();
        private readonly string _configPath = "Configs.json";
        private List<MapRegion> _mapRegions;
        private List<MapSystem> _mapSystems;
        private List<MapSystem> _simuSystems = new List<MapSystem> ();
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
            if (File.Exists(_configPath))
            {
                string content = File.ReadAllText(_configPath);
                _config = JsonConvert.DeserializeObject<Config>(content);
            }
            else
            {
                _config = new Config();
                _config.RootPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "logs");
                _config.CharacterConfigs = new List<CharacterConfig>()
                {
                    new CharacterConfig()
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
                        Speakers = new List<string>(){ "QEDSD", "Arya", "Jon" }
                    }
                };
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
            CharacterID.Text = _characterConfig.ListenerID.ToString();
            SimuGamelog.IsChecked = _characterConfig.SimuChatlog;
            SimuChatlog.IsChecked = _characterConfig.SimuGamelog;
            StringBuilder stringBuilder = new StringBuilder();
            foreach(var p in _characterConfig.ChatChanels)
            {
                stringBuilder.AppendLine($"{p.ChannelID},{p.ChannelName},{p.SessionStarted.ToString("yyyy.MM.dd HH:mm:ss")}");
            }
            ChatChanelList.Text = stringBuilder.ToString();
            stringBuilder.Clear();
            foreach (var p in _characterConfig.Speakers)
            {
                stringBuilder.Append(p);
                stringBuilder.Append(',');
            }
            stringBuilder.Remove(stringBuilder.Length - 1, 1);
            SpeakerList.Text = stringBuilder.ToString();
        }
        private void InitMap()
        {
            _mapRegions = new List<MapRegion>();
            var regionLines = File.ReadLines("regions.csv");
            foreach (var line in regionLines)
            {
                var array = line.Split(',');
                _mapRegions.Add(new MapRegion()
                {
                    Id = int.Parse(array[0]),
                    Name = array[1]
                });
            }
            _mapSystems = new List<MapSystem>();
            var systemLines = File.ReadLines("systems.csv");
            foreach (var line in systemLines)
            {
                var array = line.Split(",");
                _mapSystems.Add(new MapSystem()
                {
                    Id = int.Parse(array[0]),
                    Name = array[1],
                    RegionId = int.Parse(array[2])
                });
            }
        }
        private void Characters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(Characters.SelectedItem != null)
            {
                _characterConfig = _config.CharacterConfigs.FirstOrDefault(p=>p.Listener ==  Characters.SelectedItem.ToString());
                InitCharacterUI();
            }
        }

        private void AutoSimuChatlog_Click(object sender, RoutedEventArgs e)
        {
            _simuChatlogTimer.Stop();
            if ((sender as CheckBox).IsChecked == true)
            {
                _simuSystems.Clear();
                foreach (var item in RegionListBox.SelectedItems)
                {
                    var region = item as MapRegion;
                    _simuSystems.AddRange(_mapSystems.Where(p => p.RegionId == region.Id));
                }
                _simuChatlogTimer.Interval = double.Parse(AutoSimuChatlogSpan.Text) * 1000;
                _simuChatlogTimer.Start();
            }
        }

        /// <summary>
        /// 自动聊天频道输出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SimuChatlogTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Random random = new Random();
            int speaker = random.Next(0, _characterConfig.Speakers.Count);
            int chanel = random.Next(0, _characterConfig.ChatChanels.Count);
            int system = random.Next(0, _simuSystems.Count);
            int mark = random.Next(0, 4);
            StringBuilder stringBuilder = new StringBuilder();
            if (mark == 0)
            {
                stringBuilder.Append('*');
            }
            stringBuilder.Append(_simuSystems[system].Name);
            stringBuilder.Append(' '); 
            stringBuilder.Append(random.Next(0, 100).ToString());
            
            _characterConfig.ChatChanels[chanel].Write(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), _characterConfig.Speakers[speaker], stringBuilder.ToString());
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Visible;
            SettingPanel.IsEnabled = false;
            AutoSimuChatlog.IsChecked = false;
            RunningGrid.IsEnabled = true;
            if(_config.CharacterConfigs.FirstOrDefault(p=>p.Listener == Characters.Text) == null)
            {
                _characterConfig = _characterConfig.DepthClone<CharacterConfig>();
                _config.CharacterConfigs.Add(_characterConfig);
                (Characters.ItemsSource as ObservableCollection<string>).Add(Characters.Text);
            }

            _characterConfig.Listener = Characters.Text;
            _characterConfig.ListenerID = int.Parse(CharacterID.Text);
            var lines = ChatChanelList.Text.Split("\r\n");
            _characterConfig.ChatChanels.Clear();
            foreach (var line in lines)
            {
                var array = line.Trim().Split(',');
                if(array.Length == 3)
                {
                    ChatChanel chatChanel = new ChatChanel()
                    {
                        ChannelID = array[0],
                        ChannelName = array[1],
                        SessionStarted = DateTime.Parse(array[2]),
                        Listener = _characterConfig.Listener,
                        ListenerID = _characterConfig.ListenerID,
                    };
                    _characterConfig.ChatChanels.Add(chatChanel);
                }
            }
            _characterConfig.GameLog = new GameLog()
            {
                Listener = _characterConfig.Listener,
                ListenerID = _characterConfig.ListenerID,
                SessionStarted = DateTime.Now,
            };
            _characterConfig.Speakers.Clear();
            foreach(var speaker in SpeakerList.Text.Split(','))
            {
                if(!string.IsNullOrEmpty(speaker))
                {
                    _characterConfig.Speakers.Add(speaker);
                }
            }
            _config.RootPath = LogRootPath.Text;

            foreach(var chanel in _characterConfig.ChatChanels)
            {
                chanel.Init(_config.RootPath);
            }
            _characterConfig.GameLog.Init(_config.RootPath);

            Title = $"EVE - {_characterConfig.Listener}";
            ChatChanels.ItemsSource = _characterConfig.ChatChanels;
            ChatChanels.SelectedIndex = 0;
            Speakers.ItemsSource = _characterConfig.Speakers;
            Speakers.SelectedIndex = 0;

            File.WriteAllText(_configPath, JsonConvert.SerializeObject(_config));
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StartButton.Visibility = Visibility.Visible;
            StopButton.Visibility = Visibility.Collapsed;
            SettingPanel.IsEnabled = true;
            _simuChatlogTimer.Stop();
            RunningGrid.IsEnabled = false;
            foreach (var chanel in _characterConfig.ChatChanels)
            {
                chanel.Dispose();
            }
            _characterConfig.GameLog.Dispose();
        }

        private void Button_ChatSpeak_Click(object sender, RoutedEventArgs e)
        {
            var chanel = ChatChanels.SelectedItem as ChatChanel;
            string speaker = Speakers.Text;
            if( chanel != null && !string.IsNullOrEmpty(speaker))
            {
                chanel.Write(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), speaker, SpeakContent.Text);
            }
        }

        private void Button_Gamelog_Click(object sender, RoutedEventArgs e)
        {
            _characterConfig.GameLog.Write(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), GamelogContent.Text);
        }
    }
}