using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZKB.NET.Models.KillStream;
using static ZKB.NET.Models.KillStream.KillStream;

namespace TheGuideToTheNewEden.WinUI.Services
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
        private KillStream _killStream;
        public ZKBStreamService()
        {

        }
        public async Task Sub()
        {
            System.Threading.Interlocked.Increment(ref _subCount);
            if(_killStream == null )
            {
                _killStream = await ZKB.NET.ZKB.SubKillStreamAsync();
                _killStream.OnMessage += KillStream_OnMessage;
            }
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
        private void KillStream_OnMessage(object sender, SKBDetail detail, string sourceData)
        {
            OnMessage?.Invoke(sender, detail, sourceData);
        }
    }
}
