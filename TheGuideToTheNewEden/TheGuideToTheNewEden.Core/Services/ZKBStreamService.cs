using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZKB.NET.Models.KillStream;
using static ZKB.NET.Models.KillStream.KillStream;

namespace TheGuideToTheNewEden.Core.Services
{
    public class ZKBStreamService
    {
        private static ZKBStreamService _zkbStreamService;
        public static ZKBStreamService Current
        {
            get
            {
                _zkbStreamService ??= new ZKBStreamService();
                return _zkbStreamService;
            }
        }
        private int _subCount = 0;
        private KillStreamQ _killStream;
        public ZKBStreamService()
        {

        }
        public async Task Sub()
        {
            if(_killStream == null )
            {
                _killStream = new KillStreamQ(ZKB.NET.Config.Redisq);
                if(!await _killStream.ConnectAsync())
                {
                    _killStream = null;
                    throw new Exception("Connect Zkillredisq Failed");
                }
                _killStream.OnMessage += KillStream_OnMessage;
                _killStream.OnError += KillStream_OnError;
            }
            System.Threading.Interlocked.Increment(ref _subCount);
        }

        private void KillStream_OnError(object sender, Exception e)
        {
            OnError?.Invoke(this, e);
        }

        public void UnSub()
        {
            System.Threading.Interlocked.Decrement(ref _subCount);
            if(_subCount <= 0)
            {
                if(_killStream != null )
                {
                    _killStream.Close();
                    _killStream.OnMessage -= KillStream_OnMessage;
                }
                _killStream = null;
            }
        }
        public event MessageEventHandler OnMessage;
        public event EventHandler<Exception> OnError;
        private void KillStream_OnMessage(object sender, SKBDetail detail, string sourceData)
        {
            OnMessage?.Invoke(sender, detail, sourceData);
        }
    }
}
