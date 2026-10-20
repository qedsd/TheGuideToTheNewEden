using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EVESimulation
{
    public class ChatChanel : IDisposable
    {
        public string ChannelID { get; set; }
        public string ChannelName { get; set; }
        public string Listener { get; set; }
        public int ListenerID { get; set; }
        public DateTime SessionStarted { get; set; }

        private string _filePath;

        [JsonIgnore]
        public bool IsInitialized => !string.IsNullOrEmpty(_filePath);

        [JsonIgnore]
        public string FilePath => _filePath;

        public void Init(string rootPath)
        {
            var folder = Path.Combine(rootPath, "Chatlogs");
            Directory.CreateDirectory(folder);

            var sessionStarted = SessionStarted == DateTime.MinValue ? DateTime.Now : SessionStarted;
            string filePath;
            while (true)
            {
                filePath = Path.Combine(folder, $"{GetSafeFileName(ChannelName)}_{sessionStarted:yyyyMMdd}_{sessionStarted:HHmmss}_{ListenerID}.txt");
                if (!File.Exists(filePath))
                {
                    break;
                }
                sessionStarted = sessionStarted.AddSeconds(1);
            }
            SessionStarted = sessionStarted;
            _filePath = filePath;

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
            if (!IsInitialized)
            {
                throw new InvalidOperationException($"聊天频道[{ChannelName}]未初始化，请先点击“开始”。");
            }
            using (var fileStream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var streamWriter = new StreamWriter(fileStream, Encoding.Unicode))
            {
                streamWriter.WriteLine($"[ {time} ] {speaker} > {content}");
            }
        }

        private static string GetSafeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = (name ?? string.Empty).Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
            var result = new string(chars);
            return string.IsNullOrWhiteSpace(result) ? "Channel" : result;
        }
    }
}
