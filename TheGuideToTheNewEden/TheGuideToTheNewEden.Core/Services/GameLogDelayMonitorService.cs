using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;

namespace TheGuideToTheNewEden.Core.Services
{
    public class GameLogDelayMonitorService
    {
        public delegate void GameLogDelayExpireDelegate(int id);
        public event GameLogDelayExpireDelegate OnGameLogDelayExpire;
        private readonly object _locker = new object();
        private readonly Dictionary<int, DateTime> _expireTimes = new Dictionary<int, DateTime>();
        private Timer _timer = new Timer()
        {
            AutoReset = false,
            Interval = 1000,
        };

        public GameLogDelayMonitorService()
        {
            _timer.Elapsed += Timer_Elapsed;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (_locker)
            {
                var now = DateTime.Now;
                List<int> removeIds = new List<int>();
                foreach(var item in _expireTimes)
                {
                    if((item.Value - now).TotalMilliseconds < 0)
                    {
                        removeIds.Add(item.Key);
                        OnGameLogDelayExpire?.Invoke(item.Key);
                    }
                }
                foreach(var item in removeIds)
                {
                    _expireTimes.Remove(item);
                }
            }
            _timer.Start();
        }

        public void Update(int id, DateTime expire)
        {
            _timer.Stop();
            lock (_locker)
            {
                _expireTimes.Remove(id);
                _expireTimes.Add(id, expire);
            }
            _timer.Start();
        }
    }
}
