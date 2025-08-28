using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Interfaces;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    internal static class AuthHelper
    {
        private const string AuthEventName = "TheGuideToTheNewEden.Auth";
        private static EventWaitHandle _eventWaitHandle;

        private static readonly Lock _locker = new Lock();
        private static string _cmd = null;
        public static bool RegistyProtocol()
        {
            try
            {
                var value = Registry.GetValue("HKEY_CLASSES_ROOT\\eveauth-qedsd-neweden2\\shell\\open\\command", null, null) as string;
                string newValue = $"\"{System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TheGuideToTheNewEden.exe")}\"%1\"";
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
        public static string ReadProtocol()
        {
            return Registry.GetValue("HKEY_CLASSES_ROOT\\eveauth-qedsd-neweden3\\shell\\open\\command", null, null) as string;
        }
        public static void WriteProtocol()
        {
            string newValue = $"\"{System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TheGuideToTheNewEden.exe")}\"%1\"";
            Registry.SetValue("HKEY_CLASSES_ROOT\\eveauth-qedsd-neweden3", "URL Protocol", "");
            Registry.SetValue("HKEY_CLASSES_ROOT\\eveauth-qedsd-neweden3\\shell\\open\\command", null, newValue);
        }
        public static void DeleteProtocol()
        {
            Registry.SetValue("HKEY_CLASSES_ROOT\\eveauth-qedsd-neweden3\\shell\\open\\command", null, null);
        }
        public static async Task<string> WaitingAuthAsync(CancellationToken token)
        {
            _eventWaitHandle ??= new EventWaitHandle(false, EventResetMode.AutoReset, AuthEventName);
            _eventWaitHandle.Reset();
            var task = Task.Run(() =>
            {
                _eventWaitHandle.WaitOne();
            });
            App.SingleInstanceHelper.Activated += SingleInstanceHelper_Activated;
            while (!token.IsCancellationRequested)
            {
                if (task.IsCompleted)
                {
                    App.SingleInstanceHelper.Activated -= SingleInstanceHelper_Activated;
                    lock (_locker)
                    {
                        return _cmd;
                    }
                }
                else
                {
                    await Task.Delay(1000);
                }
            }
            _eventWaitHandle?.Reset();
            App.SingleInstanceHelper.Activated -= SingleInstanceHelper_Activated;
            return null;
        }

        private static void SingleInstanceHelper_Activated(object sender, string[] e)
        {
            lock (_locker)
            {
                _cmd = e.FirstOrDefault(p => p.StartsWith("eveauth"));
            }
            _eventWaitHandle.Set();
        }
    }
}
