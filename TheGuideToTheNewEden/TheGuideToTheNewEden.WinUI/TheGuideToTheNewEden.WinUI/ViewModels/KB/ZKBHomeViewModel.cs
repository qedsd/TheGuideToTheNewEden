using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WinUI.ViewModels.KB
{
    public class ZKBHomeViewModel:BaseViewModel
    {
        private KillStream _killStream;
        public ZKBHomeViewModel()
        {
        }
        public async Task InitAsync()
        {
            try
            {
                _killStream = await ZKB.NET.ZKB.SubKillStreamAsync();
                _killStream.OnMessage += KillStream_OnMessage;
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                Window?.ShowError(ex.Message);
            }
        }

        private void KillStream_OnMessage(object sender, SKBDetail detail, string sourceData)
        {
            
        }
    }
}
