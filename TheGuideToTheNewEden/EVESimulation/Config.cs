using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EVESimulation
{
    /// <summary>模拟器配置：日志根目录 + 可维护的角色列表（左侧列表 / 右侧选项）。</summary>
    public class Config
    {
        public string RootPath { get; set; }

        public ObservableCollection<CharacterConfig> CharacterConfigs { get; set; } = new();
    }

    /// <summary>一个可模拟的角色：身份、舰船、聊天频道、发言人、战斗日志选项。</summary>
    public class CharacterConfig : INotifyPropertyChanged
    {
        private string _listener;

        public string Listener
        {
            get => _listener;
            set
            {
                if (_listener == value)
                {
                    return;
                }

                _listener = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Listener)));
            }
        }

        public int ListenerID { get; set; }

        /// <summary>军团 ticker（战斗日志里形如 [I K N]）。</summary>
        public string Corporation { get; set; } = "I K N";

        /// <summary>舰船中文名（日志里会写成 &lt;localized hint="英文"&gt;中文*&lt;/localized&gt;）。</summary>
        public string ShipName { get; set; } = "小鹰级";

        /// <summary>舰船英文名（localized hint）。</summary>
        public string ShipHint { get; set; } = "Merlin";

        public bool SimuChatlog { get; set; } = true;

        public bool SimuGamelog { get; set; } = true;

        public ObservableCollection<ChatChanel> ChatChanels { get; set; } = new();

        public GameLog GameLog { get; set; }

        /// <summary>旧版配置（逗号分隔的发言人名），仅用于迁移。</summary>
        public System.Collections.Generic.List<string> Speakers { get; set; }

        public ObservableCollection<SpeakerItem> SpeakerItems { get; set; } = new();

        public CombatSimulationSettings Combat { get; set; } = new();

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>频道发言人：名字 + 常用舰船（自动模拟情报时带上舰船名）。</summary>
    public class SpeakerItem
    {
        public string Name { get; set; }

        public string ShipName { get; set; } = "小鹰级";

        public string ShipHint { get; set; } = "Merlin";
    }

    /// <summary>战斗日志模拟选项（按真实中文日志行式生成）。</summary>
    public class CombatSimulationSettings
    {
        /// <summary>对手角色名（列表里的另一个角色）。</summary>
        public string Actor { get; set; }

        public bool AutoCombat { get; set; }

        public double IntervalSeconds { get; set; } = 1.5;

        public bool Damage { get; set; } = true;

        public bool Miss { get; set; } = true;

        public bool Neut { get; set; } = true;

        public bool Nos { get; set; } = true;

        public bool Repair { get; set; } = true;

        public bool Scram { get; set; } = true;

        public bool Ecm { get; set; } = true;

        /// <summary>是否也生成"我打/我修/我干扰别人"的出向事件（默认只模拟收到的）。</summary>
        public bool IncludeOutgoing { get; set; }

        public int DamageMin { get; set; } = 40;

        public int DamageMax { get; set; } = 320;

        public int NeutGj { get; set; } = 45;

        public int NosGj { get; set; } = 15;

        public int RepairHp { get; set; } = 64;
    }
}
