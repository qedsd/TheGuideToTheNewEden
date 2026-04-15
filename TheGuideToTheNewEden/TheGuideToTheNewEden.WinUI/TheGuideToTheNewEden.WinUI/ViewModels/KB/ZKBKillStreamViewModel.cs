using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Services;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WinUI.ViewModels.KB
{
    public class ZKBKillStreamViewModel : BaseViewModel
    {
        public ObservableCollection<Core.Models.KB.KBItemInfo> KBItemInfos { get; set; }
        public ZKBStreamConfig Config { get; set; }
        public ZKBKillStreamViewModel()
        {
            KBItemInfos = new ObservableCollection<Core.Models.KB.KBItemInfo>();
            Config = Services.Settings.ZKBSettingService.Setting;
        }
        private Task _killStreamMessageThread;
        private CancellationTokenSource _cancellationTokenSource;
        public async Task<bool> InitAsync()
        {
            try
            {
                await Core.Services.ZKBStreamService.Current.Sub();
                Core.Services.ZKBStreamService.Current.OnMessage += KillStream_OnMessage;
                Core.Services.ZKBStreamService.Current.OnError += KillStream_OnError;
                _killStreamMsgQueue ??= new ConcurrentQueue<SKBDetail>();
                _cancellationTokenSource ??= new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;
                //使用一个线程来执行查询KB具体信息，避免ESI查名字时数据库冲突
                _killStreamMessageThread ??= new Task(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (_killStreamMsgQueue.TryDequeue(out var detail))
                        {
                            var info = Core.Helpers.KBHelpers.CreateKBItemInfo(detail);
                            if (info != null)
                            {
                                ZKBNotifyService.TryNotify(info);
                                Window?.DispatcherQueue?.SafelyTryEnqueue(() =>
                                {
                                    KBItemInfos.Insert(0, info);
                                    if (KBItemInfos.Count > Services.Settings.ZKBSettingService.Setting.MaxKBItems)
                                    {
                                        KBItemInfos.RemoveAt(KBItemInfos.Count - 1);
                                    }
                                });
                            }
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                    }
                });
                _killStreamMessageThread.Start();
                ShowSuccess(Helpers.ResourcesHelper.GetString("ZKBHomePage_Connected"));
                return true;
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                ShowError(ex.Message);
                return false;
            }
        }
        public void Stop()
        {
            Core.Services.ZKBStreamService.Current.UnSub();
            Core.Services.ZKBStreamService.Current.OnMessage -= KillStream_OnMessage;
            Core.Services.ZKBStreamService.Current.OnError -= KillStream_OnError;
        }
        private void KillStream_OnError(object sender, Exception e)
        {
            Core.Log.Error(e);
            ShowError(e);
        }

        private ConcurrentQueue<SKBDetail> _killStreamMsgQueue;
        private void KillStream_OnMessage(object sender, SKBDetail detail, string sourceData)
        {
            _killStreamMsgQueue.Enqueue(detail);
        }

        public void Dispose()
        {
            Core.Services.ZKBStreamService.Current.UnSub();
            Core.Services.ZKBStreamService.Current.OnMessage -= KillStream_OnMessage;
            Core.Services.ZKBStreamService.Current.OnError -= KillStream_OnError;
            _cancellationTokenSource?.Cancel();
        }
    }
}
