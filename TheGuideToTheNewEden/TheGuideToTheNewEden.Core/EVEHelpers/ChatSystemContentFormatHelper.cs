using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    public class ChatSystemContentFormatHelper
    {
        private ChatSystemContentFormat Formats;
        private List<string> localChangedFormats;
        private List<string> LocalChangedFormats
        {
            get
            {
                if (localChangedFormats == null)
                {
                    localChangedFormats = Formats.LocalChanged;
                }
                return localChangedFormats;
            }
        }
        private ChatSystemContentFormatHelper()
        {
            var json = System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "ChatSystemContentFormat.json"));
            if (json != null)
            {
                Formats = JsonConvert.DeserializeObject<ChatSystemContentFormat>(json);
            }
        }


        private static ChatSystemContentFormatHelper current;
        public static ChatSystemContentFormatHelper Current
        {
            get
            {
                if (current == null)
                {
                    current = new ChatSystemContentFormatHelper();
                }
                return current;
            }
        }

        public static bool IsLocalChanged(string content)
        {
            if (Current.LocalChangedFormats != null)
            {
                foreach(var item in Current.LocalChangedFormats)
                {
                    if(content.TrimStart().StartsWith(item))
                    {
                        return true;
                    }
                }
                
            }
            return false;
        }

        class ChatSystemContentFormat
        {
            public List<string> LocalChanged { get; set;}
        }
    }
}
