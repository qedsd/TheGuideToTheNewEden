using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WinUI.ViewModels.KB
{
    public class ZKBHomeViewModel:BaseViewModel
    {
        private KillStream _killStream;
        public ObservableCollection<Core.Models.KB.KBItemInfo> KBItemInfos { get; set; }
        public ZKBHomeViewModel()
        {
            KBItemInfos = new ObservableCollection<Core.Models.KB.KBItemInfo>();
        }
        public async Task InitAsync()
        {
            try
            {
                _killStream = await ZKB.NET.ZKB.SubKillStreamAsync();
                _killStream.OnMessage += KillStream_OnMessage;
                Window?.ShowSuccess(Helpers.ResourcesHelper.GetString("ZKBHomePage_Connected"));
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                Window?.ShowError(ex.Message);
            }
        }

        private void KillStream_OnMessage(object sender, SKBDetail detail, string sourceData)
        {
            var info = Core.Helpers.KBHelpers.CreateKBItemInfo(detail);
            if(info != null)
            {
                Window?.DispatcherQueue?.TryEnqueue(() =>
                {
                    KBItemInfos.Insert(0,info);
                });
            }
        }

        public void Dispose()
        {
            _killStream.Close();
        }
    }
}
