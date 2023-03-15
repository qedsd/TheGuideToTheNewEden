using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    public class ChatSystemContentFormatHelper
    {
        private dynamic Formats;
        private List<string> localChangedFormats;
        private List<string> LocalChangedFormats
        {
            get
            {
                if (localChangedFormats == null)
                {
                    localChangedFormats = (Current.Formats.LocalChanged as List<string>);
                }
                return localChangedFormats;
            }
        }
        private ChatSystemContentFormatHelper()
        {
            var json = System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Configs", "ChatSystemContentFormat.json"));
            if (json != null)
            {
                Formats = JsonConvert.DeserializeObject(json);
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

                }
                
            }
            else
            {
                return false;
            }
        }
    }
}
