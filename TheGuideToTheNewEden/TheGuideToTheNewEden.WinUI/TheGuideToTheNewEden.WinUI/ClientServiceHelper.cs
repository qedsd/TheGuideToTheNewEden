using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Microsoft.Extensions.DependencyInjection;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.Core.Interfaces;
using TheGuideToTheNewEden.Core.Services;

namespace TheGuideToTheNewEden.WinUI
{
    public class ClientServiceHelper : IDisposable
    {
        private static ClientServiceHelper _current;

        public static ClientServiceHelper Current => _current;
        private readonly IServiceCollection _serviceCollection = new ServiceCollection();

        public Dictionary<string, ILog> Loggers { get; private set; }
        public ILog Logger { get; private set; }
        public IServiceProvider ServiceProvider { get; private set; }
        public IServiceScope ServiceScope { get; private set; }

        private ClientServiceHelper()
        {
            _serviceCollection = new ServiceCollection();
        }
        private void BuildService(ILog log)
        {
            UseILog(log);

            AddComponentsService();

            ServiceProvider = _serviceCollection.BuildServiceProvider(new ServiceProviderOptions()
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            ServiceScope = ServiceProvider.CreateScope();
        }
        /// <summary>
        /// 添加各种服务
        /// </summary>
        private void AddComponentsService()
        {
            _serviceCollection.AddSingleton<PageNavigationService>();
            _serviceCollection.AddSingleton<CharacterNavigationService>();
            _serviceCollection.AddSingleton<KBNavigationService>();
            _serviceCollection.AddSingleton<ITranslationService>(new YDTranslationService());
            _serviceCollection.AddSingleton<ChannelTranslationService>();
            _serviceCollection.AddSingleton<AppUpdateService>();
            _serviceCollection.AddSingleton<BusinessService>();
            _serviceCollection.AddSingleton<TranslationSettingService>();
        }
        private void UseILog(ILog log)
        {
            _serviceCollection.AddScoped(p => Logger);
        }
        public void Dispose()
        {
            foreach (var service in _current._serviceCollection)
            {
                if (service.ServiceType.FullName.StartsWith(typeof(IService).Namespace))
                {
                    var instance = GetRequiredService(service.ServiceType);
                    if (instance is IService sv)
                    {
                        sv.Dispose();
                    }
                }
            }
        }
        #region public static
        /// <summary>
        /// 初始化所有服务
        /// </summary>
        /// <param name="log">若传入，整个库均使用该log；若不传入，自动按库的配置文件log4.xaml构建</param>
        public static void Init(ILog log = null)
        {
            _current = new ClientServiceHelper();
            _current.BuildService(log);

            foreach (var service in _current._serviceCollection)
            {
                if (service.ServiceType.FullName.StartsWith(typeof(IService).Namespace))
                {
                    var instance = GetRequiredService(service.ServiceType);
                    if (instance is IService sv)
                    {
                        sv.Init();
                    }
                }
            }
        }
        public static T GetRequiredService<T>()
        {
            if (_current == null)
                return default(T);
            else
                return _current.ServiceScope.ServiceProvider.GetRequiredService<T>();
        }
        public static object GetRequiredService(Type type)
        {
            if (_current == null)
                return type;
            else
                return _current.ServiceScope.ServiceProvider.GetRequiredService(type);
        }
        #endregion
    }
}
