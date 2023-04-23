using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Interfaces;
using static SixLabors.ImageSharp.Metadata.Profiles.Exif.EncodedString;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    internal static class AuthHelper
    {
        public static bool RegistyProtocol()
        {
            try
            {
                if (Registry.ClassesRoot.OpenSubKey("eveauth-qedsd-neweden2") == null)
                {
                    var yourProtocolName = Registry.ClassesRoot.CreateSubKey("eveauth-qedsd-neweden2");
                    var command = yourProtocolName.CreateSubKey("shell").CreateSubKey("open").CreateSubKey("command");
                    yourProtocolName.SetValue("URL Protocol", "");
                    command.SetValue(null, $"\"{System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TheGuideToTheNewEden.AuthListener.exe")}\"%1\"");
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }
        private static FileSystemWatcher _fileSystemWatcher;
        public static async Task<string> WaitingAuthAsync()
        {
            _msg = null;
            _waiting = true;
            if(_fileSystemWatcher == null)
            {
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Auth");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                FileSystemWatcher fileSystemWatcher = new FileSystemWatcher();
                fileSystemWatcher.Path = folder;
                fileSystemWatcher.EnableRaisingEvents = true;
                fileSystemWatcher.Changed += FileSystemWatcher_Changed;
            }
            while (_waiting)
            {
                await Task.Delay(1000);
            }
            return _msg;
        }
        private static bool _waiting = false;
        private static string _msg;
        private static void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if(File.Exists(e.FullPath))
            {
                _msg = File.ReadAllText(e.FullPath);
                _waiting = false;
            }
        }
    }
}
