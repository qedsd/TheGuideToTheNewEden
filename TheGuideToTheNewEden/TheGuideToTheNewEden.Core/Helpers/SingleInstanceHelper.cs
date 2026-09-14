using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Helpers
{
    /// <summary>
    /// 单实例与命令行转发：第二个实例把命令行交给第一个实例后退出。
    /// 传递方式是"临时文件 + 命名事件"——事件只负责唤醒，参数走文件。
    /// </summary>
    public class SingleInstanceHelper
    {
        private const string DefaultInstanceName = "TheGuideToTheNewEden";
        private const string DefaultTempFile = "SingleInstanceTemp";

        private readonly string _instanceName;
        private readonly string _tempFileName;
        private EventWaitHandle _eventWaitHandle;

        /// <summary>
        /// 非第一个实例激活时触发，参数为该实例的命令行（读取失败时为 null）。
        /// 注意：事件在工作线程上触发。
        /// </summary>
        public event EventHandler<string[]> Activated;

        public SingleInstanceHelper()
            : this(DefaultInstanceName, DefaultTempFile)
        {
        }

        /// <summary>
        /// 指定实例标识与临时文件名。
        /// </summary>
        /// <remarks>
        /// 同一台机器上并存的多个版本（如 WinUI 版与 WPF 版）必须使用**不同**的标识与临时文件：
        /// 否则后启动的那一版会被先启动的那版吞掉——命令行转交给了对方，自己却直接退出，
        /// 表现为"程序点了没反应"。
        /// </remarks>
        public SingleInstanceHelper(string instanceName, string tempFileName = DefaultTempFile)
        {
            _instanceName = string.IsNullOrWhiteSpace(instanceName) ? DefaultInstanceName : instanceName;
            _tempFileName = string.IsNullOrWhiteSpace(tempFileName) ? DefaultTempFile : tempFileName;
        }

        /// <summary>
        /// 尝试将当前实例注册为单例
        /// </summary>
        /// <returns>true：成功注册为第一个实例 false：已存在实例（命令行已转交，本进程应退出）</returns>
        public bool RegisterSingleInstance(string appDataPath)
        {
            _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, _instanceName, out bool isFirstInstance);

            if (!isFirstInstance)
            {
                ForwardCommandLine(appDataPath);
                return false;
            }

            _ = Task.Run(() => Listen(appDataPath));
            return true;
        }

        private string TempPath(string appDataPath) => Path.Combine(appDataPath, _tempFileName);

        /// <summary>
        /// 非首实例：把命令行落盘并唤醒主实例。
        /// </summary>
        /// <remarks>
        /// 落盘失败就**不发信号**：否则主实例会读到上一次残留的内容，把旧命令行当成新的
        /// （对授权回调来说就是把一条过期的 code 又走一遍）。
        /// </remarks>
        private void ForwardCommandLine(string appDataPath)
        {
            var path = TempPath(appDataPath);
            try
            {
                Directory.CreateDirectory(appDataPath);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.WriteAllLines(path, Environment.GetCommandLineArgs());
            }
            catch
            {
                return;
            }

            try
            {
                using var eventSignal = new EventWaitHandle(false, EventResetMode.AutoReset, _instanceName);
                eventSignal.Set();
            }
            catch
            {
                // 唤醒失败不抛出：本实例的使命就是转交命令行，失败也只能静默退出。
            }
        }

        private void Listen(string appDataPath)
        {
            var path = TempPath(appDataPath);
            while (true)
            {
                try
                {
                    _eventWaitHandle.WaitOne();

                    string[] cmds = null;
                    if (File.Exists(path))
                    {
                        cmds = File.ReadAllLines(path);
                        // 读完即删：否则下一次唤醒（例如用户又双击了一次 exe）会拿到这次留下的旧命令行。
                        File.Delete(path);
                    }

                    Activated?.Invoke(this, cmds);
                }
                catch
                {
                    // 单次读取/删除失败不能让监听线程退出，否则此后再也收不到任何激活。
                }
            }
        }
    }
}
