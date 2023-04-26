using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core;
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
                var value = Registry.GetValue("HKEY_CLASSES_ROOT\\eveauth-qedsd-neweden2\\shell\\open\\command", null, null) as string;
                string newValue = $"\"{System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TheGuideToTheNewEden.AuthListener.exe")}\"%1\"";
                if (value == newValue)
                {
                    return true;
                }
                else
                {
                    Registry.SetValue("HKEY_CLASSES_ROOT\\eveauth-qedsd-neweden2", "URL Protocol", "");
                    Registry.SetValue("HKEY_CLASSES_ROOT\\eveauth-qedsd-neweden2\\shell\\open\\command", null, newValue);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }
        public static async Task<string> WaitingAuthAsync()
        {
            string msgFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Auth","msg.txt");
            if(File.Exists(msgFile))
            {
                File.Delete(msgFile);
            }
            while(true)
            {
                if(File.Exists(msgFile))
                {
                    return File.ReadAllText(msgFile);
                }
                else
                {
                    await Task.Delay(1000);
                }
            }
        }
    }
}
