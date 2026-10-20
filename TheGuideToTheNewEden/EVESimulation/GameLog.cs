using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace EVESimulation
{
    public class GameLog : IDisposable
    {
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
            var folder = Path.Combine(rootPath, "gamelogs");
            Directory.CreateDirectory(folder);

            var sessionStarted = SessionStarted == DateTime.MinValue ? DateTime.Now : SessionStarted;
            string filePath;
            while (true)
            {
                filePath = Path.Combine(folder, $"{sessionStarted:yyyyMMdd}_{sessionStarted:HHmmss}_{ListenerID}.txt");
                if (!File.Exists(filePath))
                {
                    break;
                }
                sessionStarted = sessionStarted.AddSeconds(1);
            }
            SessionStarted = sessionStarted;
            _filePath = filePath;

            using (var streamWriter = new StreamWriter(_filePath, true, Encoding.Default))
            {
                streamWriter.WriteLine("------------------------------------------------------------");
                streamWriter.WriteLine("  游戏记录");
                streamWriter.WriteLine($"  收听者: {Listener}");
                streamWriter.WriteLine($"  进程开始: {SessionStarted.ToString("yyyy.MM.dd HH:mm:ss")}");
                streamWriter.WriteLine("------------------------------------------------------------");
                streamWriter.Flush();
            }
        }

        public void Dispose()
        {
            _filePath = null;
        }

        public void Write(string time, string content)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("游戏日志未初始化，请先点击“开始”。");
            }
            using (var fileStream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var streamWriter = new StreamWriter(fileStream, Encoding.Default))
            {
                streamWriter.WriteLine($"[ {time} ] {content}");
            }
        }
    }
}
