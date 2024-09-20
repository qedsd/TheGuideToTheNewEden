using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVESimulation
{
    public class Config
    {
        public string RootPath {  get; set; }
        public List<CharacterConfig> CharacterConfigs { get; set; }
    }
    public class CharacterConfig
    {
        public string Listener { get; set; }
        public int ListenerID { get; set; }
        public bool SimuChatlog { get; set; } = true;
        public bool SimuGamelog { get; set; } = true;
        public List<ChatChanel> ChatChanels { get; set; }
        public GameLog GameLog { get; set; }
        public List<string> Speakers { get; set; }
    }
}
