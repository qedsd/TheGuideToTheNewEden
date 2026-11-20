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

namespace EVESimulation
{
    public partial class MainWindow : Window
    {
        private Config _config;
        private CharacterConfig _selected;
        private readonly List<MapRegion> _mapRegions = new();
        private readonly List<MapSystem> _mapSystems = new();
        private readonly List<MapSystem> _simuSystems = new();
        private readonly Random _random = new();
        private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "Configs.json");
        private System.Timers.Timer _chatTimer;
        private System.Timers.Timer _combatTimer;
        private bool _running;
        private bool _loading;

        /// <summary>游戏真实日志目录（WPF 设置里的 EVELogsPath，缺省为文档下的 EVE\logs）。</summary>
        private string _defaultRootPath;

        public MainWindow()
        {
            InitializeComponent();
            InitConfig();
            InitMap();
            InitUI();

            _chatTimer = new System.Timers.Timer { AutoReset = true };
            _chatTimer.Elapsed += ChatTimer_Elapsed;
            _combatTimer = new System.Timers.Timer { AutoReset = true };
            _combatTimer.Elapsed += CombatTimer_Elapsed;

            Closing += (_, _) =>
            {
                StopSimulation();
                SaveConfig();
            };
        }

        // ---------- 初始化 ----------

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
                MessageBox.Show(this, $"读取配置失败，将使用默认配置：\n{ex.Message}", "EVE Simulation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _config = null;
            }

            _defaultRootPath = WpfCharacterImporter.DetectGameLogsPath();
            NormalizeConfig();
        }

        private void NormalizeConfig()
        {
            _config ??= new Config();
            if (string.IsNullOrWhiteSpace(_config.RootPath) || IsTemporaryPath(_config.RootPath))
            {
                // 默认放到游戏真实日志目录；调试用的临时目录（%TEMP%、D:\temp）也纠正过来
                _config.RootPath = _defaultRootPath;
            }

            if (string.IsNullOrWhiteSpace(_config.RootPath))
            {
                _config.RootPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "logs");
            }
            _config.CharacterConfigs ??= new ObservableCollection<CharacterConfig>();

            if (_config.CharacterConfigs.Count == 0)
            {
                _config.CharacterConfigs.Add(new CharacterConfig
                {
                    Listener = "QEDSD",
                    ListenerID = 2113594914,
                    Corporation = "I K N",
                    ShipName = "伊毕斯号",
                    ShipHint = "Ibis",
                });
                _config.CharacterConfigs.Add(new CharacterConfig
                {
                    Listener = "Arya D Stark",
                    ListenerID = 2112494169,
                    Corporation = "I K N",
                    ShipName = "黑鸟级",
                    ShipHint = "Blackbird",
                });
            }

            foreach (var character in _config.CharacterConfigs)
            {
                if (string.IsNullOrWhiteSpace(character.Listener))
                {
                    character.Listener = "角色";
                }
                if (character.ListenerID <= 0)
                {
                    character.ListenerID = NextListenerId();
                }
                if (string.IsNullOrWhiteSpace(character.Corporation))
                {
                    character.Corporation = "I K N";
                }
                if (string.IsNullOrWhiteSpace(character.ShipName))
                {
                    character.ShipName = "小鹰级";
                }
                if (string.IsNullOrWhiteSpace(character.ShipHint))
                {
                    character.ShipHint = ShipCatalog.HintOfShip(character.ShipName);
                }

                character.ChatChanels ??= new ObservableCollection<ChatChanel>();
                if (character.ChatChanels.Count == 0)
                {
                    character.ChatChanels.Add(new ChatChanel
                    {
                        ChannelID = "local",
                        ChannelName = "本地",
                        Listener = character.Listener,
                        ListenerID = character.ListenerID,
                    });
                }

                character.GameLog ??= new GameLog
                {
                    Listener = character.Listener,
                    ListenerID = character.ListenerID,
                };

                character.SpeakerItems ??= new ObservableCollection<SpeakerItem>();
                if (character.SpeakerItems.Count == 0 && character.Speakers is { Count: > 0 })
                {
                    foreach (var name in character.Speakers.Where(p => !string.IsNullOrWhiteSpace(p)))
                    {
                        character.SpeakerItems.Add(CreateSpeaker(name));
                    }
                }
                if (character.SpeakerItems.Count == 0)
                {
                    character.SpeakerItems.Add(CreateSpeaker(character.Listener));
                    character.SpeakerItems.Add(CreateSpeaker("Arya D Stark"));
                }
                foreach (var speaker in character.SpeakerItems)
                {
                    if (string.IsNullOrWhiteSpace(speaker.Name))
                    {
                        speaker.Name = "发言人";
                    }
                    if (string.IsNullOrWhiteSpace(speaker.ShipName))
                    {
                        speaker.ShipName = "小鹰级";
                    }
                    if (string.IsNullOrWhiteSpace(speaker.ShipHint))
                    {
                        speaker.ShipHint = ShipCatalog.HintOfShip(speaker.ShipName);
                    }
                }

                character.Combat ??= new CombatSimulationSettings();
            }

            // 默认对手：列表里的另一个角色
            foreach (var character in _config.CharacterConfigs)
            {
                if (string.IsNullOrWhiteSpace(character.Combat.Actor)
                    || !_config.CharacterConfigs.Any(p => !ReferenceEquals(p, character) && p.Listener == character.Combat.Actor))
                {
                    character.Combat.Actor = _config.CharacterConfigs
                        .FirstOrDefault(p => !ReferenceEquals(p, character))?.Listener;
                }
            }

            EnsureUniqueListenerIds();
        }

        /// <summary>
        /// 旧配置里常见两个角色共用一个角色 ID（都是测试角色）：保留第一个，其余重新分配，
        /// 避免模拟时写同一个日志文件名。
        /// </summary>
        private void EnsureUniqueListenerIds()
        {
            var used = new HashSet<int>();
            foreach (var character in _config.CharacterConfigs)
            {
                if (character.ListenerID > 0 && used.Add(character.ListenerID))
                {
                    continue;
                }

                character.ListenerID = NextListenerId();
                used.Add(character.ListenerID);
            }
        }

        private static SpeakerItem CreateSpeaker(string name)
        {
            var ship = ShipCatalog.Ships[Math.Abs(name.GetHashCode()) % ShipCatalog.Ships.Count];
            return new SpeakerItem { Name = name, ShipName = ship.Name, ShipHint = ship.Hint };
        }

        private int NextListenerId()
        {
            int id;
            do
            {
                id = _random.Next(1_000_000_000, 2_000_000_000);
            }
            while (_config.CharacterConfigs.Any(p => p.ListenerID == id));
            return id;
        }

        private void InitUI()
        {
            LogRootPath.Text = _config.RootPath;
            CharacterList.ItemsSource = _config.CharacterConfigs;
            RegionListBox.ItemsSource = _mapRegions;
            ShipCombo.ItemsSource = ShipCatalog.Ships;

            if (_config.CharacterConfigs.Count > 0)
            {
                CharacterList.SelectedIndex = 0;
            }
        }

        private void InitMap()
        {
            try
            {
                foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "regions.csv")))
                {
                    var array = line.Split(',');
                    if (array.Length >= 2 && int.TryParse(array[0], out var id))
                    {
                        _mapRegions.Add(new MapRegion { Id = id, Name = array[1] });
                    }
                }

                foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "systems.csv")))
                {
                    var array = line.Split(',');
                    if (array.Length >= 3 && int.TryParse(array[0], out var id) && int.TryParse(array[2], out var regionId))
                    {
                        _mapSystems.Add(new MapSystem { Id = id, Name = array[1], RegionId = regionId });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"读取星系数据失败：\n{ex.Message}", "EVE Simulation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ---------- 角色切换与编辑 ----------

        private void CharacterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading)
            {
                return;
            }

            if (CharacterList.SelectedItem is CharacterConfig character)
            {
                CommitCurrentCharacter();
                SaveConfig();
                LoadCharacter(character);
            }
        }

        private void LoadCharacter(CharacterConfig character)
        {
            if (character == null)
            {
                return;
            }

            _loading = true;
            try
            {
                _selected = character;
                CharacterNameBox.Text = character.Listener;
                CharacterIdBox.Text = character.ListenerID.ToString();
                CorporationBox.Text = character.Corporation;
                ShipNameBox.Text = character.ShipName;
                ShipHintBox.Text = character.ShipHint;
                var ship = ShipCatalog.FindShip(character.ShipName);
                ShipCombo.SelectedItem = ship;
                if (ship == null)
                {
                    ShipCombo.Text = character.ShipName;
                }

                SimuChatlogCheck.IsChecked = character.SimuChatlog;
                SimuGamelogCheck.IsChecked = character.SimuGamelog;

                ChannelGrid.ItemsSource = character.ChatChanels;
                SpeakerGrid.ItemsSource = character.SpeakerItems;
                ChatChanels.ItemsSource = character.ChatChanels;
                Speakers.ItemsSource = character.SpeakerItems;
                ChatChanels.SelectedIndex = character.ChatChanels.Count > 0 ? 0 : -1;
                Speakers.SelectedIndex = character.SpeakerItems.Count > 0 ? 0 : -1;

                ActorCombo.ItemsSource = _config.CharacterConfigs
                    .Where(p => !ReferenceEquals(p, character))
                    .ToList();
                ActorCombo.SelectedItem = _config.CharacterConfigs
                    .FirstOrDefault(p => !ReferenceEquals(p, character) && p.Listener == character.Combat.Actor)
                    ?? ActorCombo.Items.Cast<CharacterConfig>().FirstOrDefault();

                var combat = character.Combat;
                DamageCheck.IsChecked = combat.Damage;
                MissCheck.IsChecked = combat.Miss;
                NeutCheck.IsChecked = combat.Neut;
                NosCheck.IsChecked = combat.Nos;
                RepairCheck.IsChecked = combat.Repair;
                ScramCheck.IsChecked = combat.Scram;
                EcmCheck.IsChecked = combat.Ecm;
                OutgoingCheck.IsChecked = combat.IncludeOutgoing;
                DamageMinBox.Text = combat.DamageMin.ToString();
                DamageMaxBox.Text = combat.DamageMax.ToString();
                NeutGjBox.Text = combat.NeutGj.ToString();
                NosGjBox.Text = combat.NosGj.ToString();
                RepairHpBox.Text = combat.RepairHp.ToString();
                AutoCombatCheck.IsChecked = combat.AutoCombat;
                AutoCombatIntervalBox.Text = combat.IntervalSeconds.ToString("0.##");
                AutoSimuChatlog.IsChecked = false;

                StatusText.Text = _running ? "运行中" : "未启动";
            }
            finally
            {
                _loading = false;
            }
        }

        private void CommitCurrentCharacter()
        {
            if (_selected == null)
            {
                return;
            }

            var name = (CharacterNameBox.Text ?? string.Empty).Trim();
            if (name.Length > 0)
            {
                _selected.Listener = name;
            }

            if (int.TryParse((CharacterIdBox.Text ?? string.Empty).Trim(), out var id) && id > 0)
            {
                _selected.ListenerID = id;
            }

            _selected.Corporation = (CorporationBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(_selected.Corporation))
            {
                _selected.Corporation = "I K N";
            }

            var shipName = (ShipNameBox.Text ?? string.Empty).Trim();
            if (shipName.Length > 0)
            {
                _selected.ShipName = shipName;
            }

            var shipHint = (ShipHintBox.Text ?? string.Empty).Trim();
            _selected.ShipHint = shipHint.Length > 0
                ? shipHint
                : ShipCatalog.HintOfShip(_selected.ShipName);
            _selected.SimuChatlog = SimuChatlogCheck.IsChecked == true;
            _selected.SimuGamelog = SimuGamelogCheck.IsChecked == true;

            foreach (var channel in _selected.ChatChanels)
            {
                channel.Listener = _selected.Listener;
                channel.ListenerID = _selected.ListenerID;
            }

            foreach (var speaker in _selected.SpeakerItems)
            {
                if (string.IsNullOrWhiteSpace(speaker.Name))
                {
                    speaker.Name = "发言人";
                }
                if (string.IsNullOrWhiteSpace(speaker.ShipName))
                {
                    speaker.ShipName = "小鹰级";
                }
                if (string.IsNullOrWhiteSpace(speaker.ShipHint))
                {
                    speaker.ShipHint = ShipCatalog.HintOfShip(speaker.ShipName);
                }
            }

            CommitCombatSettings();
        }

        private void CommitCombatSettings()
        {
            if (_selected == null)
            {
                return;
            }

            var combat = _selected.Combat;
            combat.Actor = (ActorCombo.SelectedItem as CharacterConfig)?.Listener;
            combat.Damage = DamageCheck.IsChecked == true;
            combat.Miss = MissCheck.IsChecked == true;
            combat.Neut = NeutCheck.IsChecked == true;
            combat.Nos = NosCheck.IsChecked == true;
            combat.Repair = RepairCheck.IsChecked == true;
            combat.Scram = ScramCheck.IsChecked == true;
            combat.Ecm = EcmCheck.IsChecked == true;
            combat.IncludeOutgoing = OutgoingCheck.IsChecked == true;
            combat.DamageMin = ParseInt(DamageMinBox.Text, 40, 1, 1000000);
            combat.DamageMax = ParseInt(DamageMaxBox.Text, 320, combat.DamageMin, 1000000);
            combat.NeutGj = ParseInt(NeutGjBox.Text, 45, 1, 1000000);
            combat.NosGj = ParseInt(NosGjBox.Text, 15, 1, 1000000);
            combat.RepairHp = ParseInt(RepairHpBox.Text, 64, 1, 1000000);
            combat.AutoCombat = AutoCombatCheck.IsChecked == true;
            combat.IntervalSeconds = ParseDouble(AutoCombatIntervalBox.Text, 1.5, 0.2, 60);
        }

        private static int ParseInt(string text, int fallback, int min, int max)
        {
            if (!int.TryParse((text ?? string.Empty).Trim(), out var value))
            {
                return fallback;
            }

            return Math.Clamp(value, min, max);
        }

        private static double ParseDouble(string text, double fallback, double min, double max)
        {
            if (!double.TryParse((text ?? string.Empty).Trim(), out var value))
            {
                return fallback;
            }

            return Math.Clamp(value, min, max);
        }

        private void ShipCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || ShipCombo.SelectedItem is not ShipCatalog.Ship ship)
            {
                return;
            }

            ShipNameBox.Text = ship.Name;
            ShipHintBox.Text = ship.Hint;
        }

        // ---------- 角色列表维护 ----------

        private void AddCharacter_Click(object sender, RoutedEventArgs e)
        {
            if (_running)
            {
                MessageBox.Show(this, "请先停止模拟。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CommitCurrentCharacter();
            var index = 1;
            while (_config.CharacterConfigs.Any(p => p.Listener == $"角色{index}"))
            {
                index++;
            }

            var character = new CharacterConfig
            {
                Listener = $"角色{index}",
                ListenerID = NextListenerId(),
                Corporation = "I K N",
                ShipName = "小鹰级",
                ShipHint = "Merlin",
            };
            character.ChatChanels.Add(new ChatChanel
            {
                ChannelID = "local",
                ChannelName = "本地",
                Listener = character.Listener,
                ListenerID = character.ListenerID,
            });
            character.GameLog = new GameLog
            {
                Listener = character.Listener,
                ListenerID = character.ListenerID,
            };
            character.SpeakerItems.Add(CreateSpeaker(character.Listener));
            character.Combat.Actor = _config.CharacterConfigs.FirstOrDefault()?.Listener;
            _config.CharacterConfigs.Add(character);
            CharacterList.SelectedItem = character;
            SaveConfig();
        }

        private void DuplicateCharacter_Click(object sender, RoutedEventArgs e)
        {
            if (_running || _selected == null)
            {
                MessageBox.Show(this, "请先停止模拟。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CommitCurrentCharacter();
            var copy = JsonConvert.DeserializeObject<CharacterConfig>(
                JsonConvert.SerializeObject(_selected));
            copy.Listener = $"{_selected.Listener} 副本";
            copy.ListenerID = NextListenerId();
            copy.ChatChanels = new ObservableCollection<ChatChanel>(
                copy.ChatChanels.Select(p => new ChatChanel
                {
                    ChannelID = p.ChannelID,
                    ChannelName = p.ChannelName,
                    Listener = copy.Listener,
                    ListenerID = copy.ListenerID,
                }));
            copy.GameLog = new GameLog { Listener = copy.Listener, ListenerID = copy.ListenerID };
            _config.CharacterConfigs.Add(copy);
            CharacterList.SelectedItem = copy;
            SaveConfig();
        }

        private void RemoveCharacter_Click(object sender, RoutedEventArgs e)
        {            if (_running)
            {
                MessageBox.Show(this, "请先停止模拟。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selected == null || _config.CharacterConfigs.Count <= 1)
            {
                MessageBox.Show(this, "至少保留一个角色。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(this, $"删除角色「{_selected.Listener}」？", "EVE Simulation",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            var index = _config.CharacterConfigs.IndexOf(_selected);
            _config.CharacterConfigs.Remove(_selected);
            CharacterList.SelectedIndex = Math.Clamp(index, 0, _config.CharacterConfigs.Count - 1);
            SaveConfig();
        }

        /// <summary>
        /// 从 TheGuideToTheNewEden.WPF 的数据目录导入真实角色（名称 + ID）。
        /// 默认目录取 <c>%LocalAppData%\TheGuideToTheNewEden</c> 的 datapath.json；Configs 里的
        /// Auth.json / 游戏日志头负责补全名字与 ID，频道/多开配置里的名字也会被合并。
        /// </summary>
        private void ImportFromWpf_Click(object sender, RoutedEventArgs e)
        {
            if (_running)
            {
                MessageBox.Show(this, "请先停止模拟。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CommitCurrentCharacter();
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "选择 TheGuideToTheNewEden 数据目录（含 Configs）",
                InitialDirectory = WpfCharacterImporter.DefaultDataPath(),
            };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            // 顺便按选中的 WPF 数据目录更新"游戏真实日志目录"默认值
            var detectedLogsPath = WpfCharacterImporter.DetectGameLogsPath(dialog.FolderName);
            if (!string.IsNullOrWhiteSpace(detectedLogsPath))
            {
                _defaultRootPath = detectedLogsPath;
                if (string.IsNullOrWhiteSpace(LogRootPath.Text) || IsTemporaryPath(LogRootPath.Text))
                {
                    LogRootPath.Text = detectedLogsPath;
                }
            }

            var characters = WpfCharacterImporter.Import(
                dialog.FolderName,
                WpfCharacterImporter.DefaultLogsPath(),
                out var summary);
            if (characters.Count == 0)
            {
                MessageBox.Show(this, $"没有找到可导入的角色。\n\n{summary}", "EVE Simulation",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var added = 0;
            var updated = 0;
            foreach (var imported in characters)
            {
                var byId = _config.CharacterConfigs.FirstOrDefault(p => p.ListenerID == imported.Id);
                if (byId != null)
                {
                    if (!string.Equals(byId.Listener, imported.Name, StringComparison.Ordinal))
                    {
                        byId.Listener = imported.Name;
                        updated++;
                    }

                    continue;
                }

                var byName = _config.CharacterConfigs.FirstOrDefault(p =>
                    string.Equals(p.Listener, imported.Name, StringComparison.OrdinalIgnoreCase));
                if (byName != null)
                {
                    byName.ListenerID = imported.Id;
                    updated++;
                    continue;
                }

                var character = new CharacterConfig
                {
                    Listener = imported.Name,
                    ListenerID = imported.Id,
                    Corporation = "I K N",
                    ShipName = "伊毕斯号",
                    ShipHint = "Ibis",
                };
                character.ChatChanels.Add(new ChatChanel
                {
                    ChannelID = "local",
                    ChannelName = "本地",
                    Listener = character.Listener,
                    ListenerID = character.ListenerID,
                });
                character.GameLog = new GameLog
                {
                    Listener = character.Listener,
                    ListenerID = character.ListenerID,
                };
                character.SpeakerItems.Add(CreateSpeaker(character.Listener));
                _config.CharacterConfigs.Add(character);
                added++;
            }

            // 给没有对手的角色补一个默认对手
            foreach (var character in _config.CharacterConfigs)
            {
                if (!_config.CharacterConfigs.Any(p => !ReferenceEquals(p, character)
                                                        && string.Equals(p.Listener, character.Combat.Actor, StringComparison.OrdinalIgnoreCase)))
                {
                    character.Combat.Actor = _config.CharacterConfigs
                        .FirstOrDefault(p => !ReferenceEquals(p, character))?.Listener;
                }
            }

            EnsureUniqueListenerIds();
            SaveConfig();
            CharacterList.Items.Refresh();
            if (added > 0)
            {
                CharacterList.SelectedItem = _config.CharacterConfigs.LastOrDefault();
            }

            var detail = string.Join("\n", characters.Select(p => $"{p.Name}（{p.Id}，{p.Source}）"));
            MessageBox.Show(this, $"{summary}\n\n新增 {added} 个，更新 {updated} 个。\n\n{detail}",
                "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ---------- 频道/发言人维护 ----------

        private void AddChannel_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                return;
            }

            var channel = new ChatChanel
            {
                ChannelID = "new",
                ChannelName = "新频道",
                Listener = _selected.Listener,
                ListenerID = _selected.ListenerID,
            };
            _selected.ChatChanels.Add(channel);
            ChatChanels.SelectedItem = channel;
        }

        private void RemoveChannel_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null || ChannelGrid.SelectedItem is not ChatChanel channel)
            {
                return;
            }

            _selected.ChatChanels.Remove(channel);
        }

        private void AddSpeaker_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                return;
            }

            var speaker = new SpeakerItem { Name = "新发言人", ShipName = "小鹰级", ShipHint = "Merlin" };
            _selected.SpeakerItems.Add(speaker);
            Speakers.SelectedItem = speaker;
        }

        private void RemoveSpeaker_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null || SpeakerGrid.SelectedItem is not SpeakerItem speaker)
            {
                return;
            }

            _selected.SpeakerItems.Remove(speaker);
        }

        // ---------- 开始/停止 ----------

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartSimulation();
            }
            catch (Exception ex)
            {
                StopSimulation();
                MessageBox.Show(this, $"启动模拟失败：\n{ex.Message}", "EVE Simulation",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartSimulation()
        {
            if (_selected == null)
            {
                throw new InvalidOperationException("请先选择一个角色。");
            }

            CommitCurrentCharacter();
            var character = _selected;
            var simulateChat = character.SimuChatlog;
            var simulateGame = character.SimuGamelog;
            if (!simulateChat && !simulateGame)
            {
                throw new InvalidOperationException("至少勾选「模拟聊天频道」或「模拟游戏日志」之一。");
            }

            if (!int.TryParse((CharacterIdBox.Text ?? string.Empty).Trim(), out var listenerId) || listenerId <= 0)
            {
                throw new FormatException("角色 ID 必须是正整数。");
            }

            var rootPath = (LogRootPath.Text ?? string.Empty).Trim();
            if (rootPath.Length == 0)
            {
                throw new InvalidOperationException("日志根目录不能为空。");
            }

            var channels = character.ChatChanels
                .Where(p => !string.IsNullOrWhiteSpace(p.ChannelID) && !string.IsNullOrWhiteSpace(p.ChannelName))
                .ToList();
            if (simulateChat && channels.Count == 0)
            {
                throw new InvalidOperationException("请至少添加一个有效频道（ID + 名称）。");
            }

            if (simulateChat)
            {
                if (character.SpeakerItems.Count == 0)
                {
                    throw new InvalidOperationException("请至少添加一个发言人。");
                }

                foreach (var channel in channels)
                {
                    channel.Listener = character.Listener;
                    channel.ListenerID = listenerId;
                    channel.SessionStarted = DateTime.Now;
                    channel.Init(rootPath);
                }
            }

            character.GameLog = new GameLog
            {
                Listener = character.Listener,
                ListenerID = listenerId,
                SessionStarted = DateTime.Now,
            };
            if (simulateGame)
            {
                character.GameLog.Init(rootPath);
            }

            _config.RootPath = rootPath;
            _running = true;
            // 多开预览靠游戏窗口标题取角色名（第一个 '-' 之后）；未开始只显示 EVE
            Title = $"EVE - {character.Listener}";
            CharacterPanel.IsEnabled = false;
            CharacterTab.IsEnabled = false;
            LogRootPath.IsEnabled = false;
            StartButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Visible;
            ChatSpeakButton.IsEnabled = simulateChat;
            ChatOpenLogButton.IsEnabled = simulateChat;
            StatusText.Text = $"运行中：{character.Listener}（{(simulateChat ? "聊天" : "")}{(simulateChat && simulateGame ? " + " : "")}{(simulateGame ? "日志" : "")}）";

            var autoChat = AutoSimuChatlog.IsChecked == true;
            var autoCombat = AutoCombatCheck.IsChecked == true;
            _loading = true;
            AutoSimuChatlog.IsChecked = false;
            AutoCombatCheck.IsChecked = false;
            _loading = false;

            SaveConfig();

            if (autoChat)
            {
                AutoSimuChatlog.IsChecked = true;
            }
            if (autoCombat)
            {
                AutoCombatCheck.IsChecked = true;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopSimulation();
        }

        private void StopSimulation()
        {
            _running = false;
            _chatTimer?.Stop();
            _combatTimer?.Stop();
            _loading = true;
            AutoSimuChatlog.IsChecked = false;
            AutoCombatCheck.IsChecked = false;
            _loading = false;

            if (_selected != null)
            {
                foreach (var channel in _selected.ChatChanels)
                {
                    channel.Dispose();
                }
                _selected.GameLog?.Dispose();
            }

            CharacterPanel.IsEnabled = true;
            CharacterTab.IsEnabled = true;
            LogRootPath.IsEnabled = true;
            StartButton.Visibility = Visibility.Visible;
            StopButton.Visibility = Visibility.Collapsed;
            ChatSpeakButton.IsEnabled = true;
            ChatOpenLogButton.IsEnabled = true;
            Title = "EVE";
            StatusText.Text = "未启动";
        }

        // ---------- 自动模拟 ----------

        private void AutoSimuChatlog_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading || !_running)
            {
                return;
            }

            if (!TryStartChatTimer())
            {
                _loading = true;
                AutoSimuChatlog.IsChecked = false;
                _loading = false;
            }
        }

        private void AutoSimuChatlog_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!_loading)
            {
                _chatTimer?.Stop();
            }
        }

        private bool TryStartChatTimer()
        {
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
                MessageBox.Show(this, "请至少选择一个星域。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!double.TryParse((AutoSimuChatlogSpan.Text ?? string.Empty).Trim(), out var span) || span <= 0)
            {
                MessageBox.Show(this, "自动模拟间隔必须大于 0。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            _chatTimer.Interval = span * 1000;
            _chatTimer.Start();
            return true;
        }

        private void AutoCombatCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading || !_running)
            {
                return;
            }

            CommitCombatSettings();
            if (_selected?.GameLog?.IsInitialized != true)
            {
                MessageBox.Show(this, "游戏日志未初始化：开始模拟时需勾选「模拟游戏日志」。", "EVE Simulation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _loading = true;
                AutoCombatCheck.IsChecked = false;
                _loading = false;
                return;
            }

            _combatTimer.Interval = _selected.Combat.IntervalSeconds * 1000;
            _combatTimer.Start();
        }

        private void AutoCombatCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!_loading)
            {
                _combatTimer?.Stop();
            }
        }

        private void ChatTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (!_running || _selected == null || _simuSystems.Count == 0)
                {
                    return;
                }

                var character = _selected;
                if (character.ChatChanels.Count == 0 || character.SpeakerItems.Count == 0)
                {
                    return;
                }

                var channel = character.ChatChanels[_random.Next(character.ChatChanels.Count)];
                var speaker = character.SpeakerItems[_random.Next(character.SpeakerItems.Count)];
                var system = _simuSystems[_random.Next(_simuSystems.Count)];
                var message = new StringBuilder();
                if (_random.Next(4) == 0)
                {
                    message.Append('*');
                }

                message.Append(system.Name);
                message.Append(' ');
                message.Append(_random.Next(0, 10));
                if (!string.IsNullOrWhiteSpace(speaker.ShipName))
                {
                    message.Append(' ');
                    message.Append(speaker.ShipName);
                }

                channel.Write(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), speaker.Name, message.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void CombatTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (!_running)
                {
                    return;
                }

                // 读到 UI 上的最新选项（勾选/数值可能随时改），再写日志
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (!_running)
                        {
                            return;
                        }

                        CommitCombatSettings();
                        var character = _selected;
                        if (character?.GameLog?.IsInitialized != true)
                        {
                            return;
                        }

                        var actor = FindActor(character);
                        var line = CombatLogSimulator.BuildAuto(character, actor, character.Combat, _random);
                        if (!string.IsNullOrEmpty(line))
                        {
                            character.GameLog.Write(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), line);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private CharacterConfig FindActor(CharacterConfig self)
        {
            var name = self?.Combat?.Actor;
            return _config.CharacterConfigs
                       .FirstOrDefault(p => !ReferenceEquals(p, self) && p.Listener == name)
                   ?? _config.CharacterConfigs.FirstOrDefault(p => !ReferenceEquals(p, self));
        }

        // ---------- 手动发送 ----------

        private void Button_ChatSpeak_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selected == null)
                {
                    return;
                }

                var channel = ChatChanels.SelectedItem as ChatChanel;
                if (channel == null)
                {
                    MessageBox.Show(this, "请先选择频道。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var speaker = Speakers.SelectedItem as SpeakerItem;
                if (speaker == null && !string.IsNullOrWhiteSpace(Speakers.Text))
                {
                    speaker = _selected.SpeakerItems.FirstOrDefault(p => p.Name == Speakers.Text)
                              ?? new SpeakerItem { Name = Speakers.Text, ShipName = "小鹰级", ShipHint = "Merlin" };
                }

                if (speaker == null || string.IsNullOrWhiteSpace(speaker.Name))
                {
                    MessageBox.Show(this, "请先选择或输入发言人。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                channel.Write(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), speaker.Name, SpeakContent.Text ?? string.Empty);
                StatusText.Text = $"已发言：{speaker.Name} → {channel.ChannelName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"发言失败：\n{ex.Message}", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendDamage_Click(object sender, RoutedEventArgs e) => SendCombat(CombatEventType.Damage);

        private void SendMiss_Click(object sender, RoutedEventArgs e) => SendCombat(CombatEventType.Miss);

        private void SendNeut_Click(object sender, RoutedEventArgs e) => SendCombat(CombatEventType.Neut);

        private void SendNos_Click(object sender, RoutedEventArgs e) => SendCombat(CombatEventType.Nos);

        private void SendRepair_Click(object sender, RoutedEventArgs e) => SendCombat(CombatEventType.Repair);

        private void SendScram_Click(object sender, RoutedEventArgs e) => SendCombat(CombatEventType.Scram);

        private void SendEcm_Click(object sender, RoutedEventArgs e) => SendCombat(CombatEventType.Ecm);

        private void SendCombat(CombatEventType type)
        {
            try
            {
                if (!_running)
                {
                    MessageBox.Show(this, "请先点「开始」。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_selected?.GameLog?.IsInitialized != true)
                {
                    MessageBox.Show(this, "游戏日志未初始化：开始模拟时需勾选「模拟游戏日志」。", "EVE Simulation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                CommitCombatSettings();
                var actor = FindActor(_selected);
                if (actor == null)
                {
                    MessageBox.Show(this, "没有对手角色：请先在左侧新增一个角色。", "EVE Simulation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var outgoing = OutgoingCheck.IsChecked == true;
                var line = CombatLogSimulator.Build(_selected, actor, type, _selected.Combat, _random, outgoing);
                _selected.GameLog.Write(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), line);
                StatusText.Text = $"已发送：{line}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"写入战斗日志失败：\n{ex.Message}", "EVE Simulation",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_Gamelog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selected?.GameLog?.IsInitialized != true)
                {
                    MessageBox.Show(this, "游戏日志未初始化：请先点「开始」并勾选「模拟游戏日志」。", "EVE Simulation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _selected.GameLog.Write(DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"), GamelogContent.Text ?? string.Empty);
                StatusText.Text = "已写入一行原始日志";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"写入游戏日志失败：\n{ex.Message}", "EVE Simulation",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenCombatLog_Click(object sender, RoutedEventArgs e)
            => OpenLogFile(_selected?.GameLog?.FilePath, "游戏日志");

        private void Button_ChatOpenLog_Click(object sender, RoutedEventArgs e)
            => OpenLogFile((ChatChanels.SelectedItem as ChatChanel)?.FilePath, "聊天频道日志");

        private void Button_GamelogOpenLog_Click(object sender, RoutedEventArgs e)
            => OpenLogFile(_selected?.GameLog?.FilePath, "游戏日志");

        private void OpenLogFile(string filePath, string name)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    MessageBox.Show(this, $"{name}文件不存在，请先点「开始」。", "EVE Simulation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"打开{name}失败：\n{ex.Message}", "EVE Simulation",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- 配置 ----------

        /// <summary>临时/调试目录（测试脚本用的 D:\temp、系统 %TEMP%）不应作为默认日志根目录。</summary>
        private static bool IsTemporaryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }

            var full = path.TrimEnd('\\', '/');
            var temp = Path.GetTempPath().TrimEnd('\\', '/');
            return full.StartsWith(temp + "\\", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(full, temp, StringComparison.OrdinalIgnoreCase)
                   || full.Contains(@"\temp\", StringComparison.OrdinalIgnoreCase)
                   || full.EndsWith(@"\temp", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>恢复到游戏真实日志目录（WPF 设置里的 EVELogsPath，缺省为文档下的 EVE\logs）。</summary>
        private void RestoreDefaultPath_Click(object sender, RoutedEventArgs e)
        {
            if (_running)
            {
                MessageBox.Show(this, "请先停止模拟。", "EVE Simulation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogRootPath.Text = _defaultRootPath;
        }

        private void SaveConfig()
        {
            try
            {
                File.WriteAllText(_configPath, JsonConvert.SerializeObject(_config, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}
