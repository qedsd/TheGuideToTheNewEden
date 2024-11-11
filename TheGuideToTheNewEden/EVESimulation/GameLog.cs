using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVESimulation
{
    public class GameLog : IDisposable
    {
        public string Listener { get; set; }
        public int ListenerID { get; set; }
        public DateTime SessionStarted { get; set; }

        private string _filePath;
        private StreamWriter _streamWriter;
        public void Init(string rootPath)
        {
            _filePath = Path.Combine(rootPath, "gamelogs", $"{SessionStarted.ToString("yyyyMMdd")}_{SessionStarted.ToString("HHmmss")}_{ListenerID}.txt");
            File.Create(_filePath).Close();
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
            //_streamWriter.Close();
        }
        public void Write(string time, string content)
        {
            using (var streamWriter = new StreamWriter(_filePath, true, Encoding.Default))
            {
                streamWriter.WriteLine($"[ {time} ] {content}");
            }
        }
    }
}
