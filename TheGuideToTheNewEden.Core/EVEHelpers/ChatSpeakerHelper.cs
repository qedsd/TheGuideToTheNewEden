using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    public class ChatSpeakerHelper
    {
        private SpeakerNames Speakers;
        private HashSet<string> eveSystems;
        private HashSet<string> EVESystems
        {
            get
            {
                if (eveSystems == null)
                {
                    eveSystems = Speakers.EVESystem?.ToHashSet();
                }
                return eveSystems;
            }
        }
        private ChatSpeakerHelper()
        {
            var json = System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "SpeakerNames.json"));
            if(json != null )
            {
                Speakers = JsonConvert.DeserializeObject<SpeakerNames>(json);
            }
        }


        private static ChatSpeakerHelper current;
        public static ChatSpeakerHelper Current
        {
            get
            {
                if(current == null)
                {
                    current = new ChatSpeakerHelper();
                }
                return current;
            }
        }

        public static bool IsEVESystem(string name)
        {
            if(Current.EVESystems != null)
            {
                return Current.EVESystems.Contains(name);
            }
            else
            {
                return false;
            }
        }
        class SpeakerNames
        {
            public List<string> EVESystem { get; set; }
        }
    }
}
