using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVESimulation
{
    public class ChatChanel: IDisposable
    {
        public string ChannelID { get; set; }
        public string ChannelName { get; set; }
        public string Listener { get; set; }
        public int ListenerID { get; set; }
        public DateTime SessionStarted { get; set; }

        private string _filePath;
        public void Init(string rootPath)
        {
            _filePath = Path.Combine(rootPath, "Chatlogs", $"{ChannelName}_{SessionStarted.ToString("yyyyMMdd")}_{SessionStarted.ToString("HHmmss")}_{ListenerID}.txt");
            using (var streamWriter = new StreamWriter(_filePath, false, Encoding.Unicode))
            {
                streamWriter.WriteLine();
                streamWriter.WriteLine();
                streamWriter.WriteLine();
                streamWriter.WriteLine();
                streamWriter.WriteLine("        ---------------------------------------------------------------");
                streamWriter.WriteLine();
                streamWriter.WriteLine($"          Channel ID:      {ChannelID}");
                streamWriter.WriteLine($"          Channel Name:    {ChannelName}");
                streamWriter.WriteLine($"          Listener:        {Listener}");
                streamWriter.WriteLine($"          Session started: {SessionStarted.ToString("yyyy.MM.dd HH:mm:ss")}");
                streamWriter.WriteLine("        ---------------------------------------------------------------");
                streamWriter.WriteLine();
            }
        }
        public void Dispose()
        {
            _filePath = null;
        }
        public void Write(string time, string speaker, string content)
        {
            if(_filePath != null)
            {
                using (var streamWriter = new StreamWriter(_filePath, true, Encoding.Unicode))
                {
                    streamWriter.WriteLine($"[ {time} ] {speaker} > {content}");
                }
            }
        }
    }
}
