using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using ESI.NET.Models.PlanetaryInteraction;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public class SingleInstanceHelper
    {
        private const string AppName = "TheGuideToTheNewEden";
        private const string TempFile = "SingleInstanceTemp";
        private EventWaitHandle _eventWaitHandle;


        /// <summary>
        /// 非第一个实例激活时
        /// </summary>
        public event EventHandler<string[]> Activated;

        /// <summary>
        /// 尝试将当前实例注册为单例
        /// </summary>
        /// <returns>true：成功注册为第一个实例 false：已存在实例</returns>
        public bool RegisterSingleInstance()
        {
            _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, AppName, out bool isFirstInstance);

            if (!isFirstInstance)
            {
                var cmds = Environment.GetCommandLineArgs();
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TempFile);
                File.WriteAllLines(path, cmds);
                using (var eventSignal = new EventWaitHandle(false,EventResetMode.AutoReset, AppName))
                {
                    eventSignal.Set();
                }
                return false;
            }
            else
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    while (true)
                    {
                        _eventWaitHandle.WaitOne();
                        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TempFile);
                        string[] cmds = null;
                        if(File.Exists(path))
                        {
                            cmds = File.ReadAllLines(path);
                        }
                        Activated?.Invoke(this, cmds);
                    }
                });
                return true;
            }
        }
    }
}
