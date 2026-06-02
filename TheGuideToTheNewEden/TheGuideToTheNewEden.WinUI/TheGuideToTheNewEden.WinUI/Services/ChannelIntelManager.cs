using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Models;

namespace TheGuideToTheNewEden.WinUI.Services
{
    /// <summary>
    /// 管理所有ChannelIntel实例，统一转发OnIgnoreJumpsIntelUpdate事件，并提供当前活跃Listener列表
    /// </summary>
    internal class ChannelIntelManager
    {
        private static readonly ChannelIntelManager _instance = new ChannelIntelManager();
        public static ChannelIntelManager Instance => _instance;

        private readonly List<ChannelIntel> _activeChannelIntels = new List<ChannelIntel>();

        /// <summary>
        /// 统一对外的OnIgnoreJumpsIntelUpdate事件
        /// </summary>
        public event Action<Core.Models.ChannelIntel.ChannelIntelObserver, IEnumerable<Core.Models.EarlyWarningContent>> OnIgnoreJumpsIntelUpdate;

        private ChannelIntelManager() { }

        /// <summary>
        /// 注册ChannelIntel实例并监听其_observers的OnIgnoreJumpsIntelUpdate事件
        /// </summary>
        public void Register(ChannelIntel channelIntel)
        {
            if (!_activeChannelIntels.Contains(channelIntel))
            {
                _activeChannelIntels.Add(channelIntel);
                AttachObservers(channelIntel);
            }
        }

        /// <summary>
        /// 注销ChannelIntel实例
        /// </summary>
        public void Unregister(ChannelIntel channelIntel)
        {
            if (_activeChannelIntels.Contains(channelIntel))
            {
                DetachObservers(channelIntel);
                _activeChannelIntels.Remove(channelIntel);
            }
        }

        /// <summary>
        /// 获取当前所有活跃的Listener
        /// </summary>
        public List<string> GetActiveListeners()
        {
            return _activeChannelIntels.Select(c => c.Listener).ToList();
        }

        private void AttachObservers(ChannelIntel channelIntel)
        {
            var observers = channelIntel.GetObservers();
            foreach (var observer in observers)
            {
                observer.OnIgnoreJumpsIntelUpdate += Observer_OnIgnoreJumpsIntelUpdate;
            }
        }

        private void Observer_OnIgnoreJumpsIntelUpdate(Core.Models.ChannelIntel.ChannelIntelObserver channelIntelObserver, IEnumerable<Core.Models.EarlyWarningContent> news)
        {
            OnIgnoreJumpsIntelUpdate?.Invoke(channelIntelObserver, news);
        }

        private void DetachObservers(ChannelIntel channelIntel)
        {
            var observers = channelIntel.GetObservers();
            foreach (var observer in observers)
            {
                observer.OnIgnoreJumpsIntelUpdate -= Observer_OnIgnoreJumpsIntelUpdate;
            }
        }

        public void ListenChannelIntel(string name)
        {
            _activeChannelIntels.FirstOrDefault(p => p.Listener == name)?.ListenChannelIntel();
        }
        public void ListenChannelIntel(IEnumerable<string> name)
        {
            var hs = name.ToHashSet();
            foreach (var channelIntel in _activeChannelIntels)
            {
                if (hs.Contains(channelIntel.Listener))
                {
                    channelIntel.ListenChannelIntel();
                }
            }
        }
        public void UnListenChannelIntel(string name)
        {
            _activeChannelIntels.FirstOrDefault(p => p.Listener == name)?.UnListenChannelIntel();
        }
        public void UnListenChannelIntel()
        {
            foreach(var channelIntel in _activeChannelIntels)
            {
                channelIntel.UnListenChannelIntel();
            }
        }
        /// <summary>
        /// 停止并注销所有活跃的 ChannelIntel 实例
        /// </summary>
        public void Clear()
        {
            foreach (var channelIntel in _activeChannelIntels.ToList())
            {
                channelIntel.Stop();
            }
        }
    }
}
