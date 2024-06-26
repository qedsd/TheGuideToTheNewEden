﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Services;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WinUI.ViewModels.KB
{
    public class ZKBHomeViewModel:BaseViewModel
    {
        public ObservableCollection<Core.Models.KB.KBItemInfo> KBItemInfos { get; set; }
        public ZKBHomeViewModel()
        {
            KBItemInfos = new ObservableCollection<Core.Models.KB.KBItemInfo>();
        }
        private Task _killStreamMessageThread;
        private CancellationTokenSource _cancellationTokenSource;
        public async Task InitAsync()
        {
            try
            {
                await Core.Services.ZKBStreamService.Current.Sub();
                Core.Services.ZKBStreamService.Current.OnMessage += KillStream_OnMessage;
                _killStreamMsgQueue = new ConcurrentQueue<SKBDetail>();
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;
                //使用一个线程来执行查询KB具体信息，避免ESI查名字时数据库冲突
                _killStreamMessageThread = new Task(() =>
                {
                    while(!token.IsCancellationRequested)
                    {
                        if(_killStreamMsgQueue.TryDequeue(out var detail))
                        {
                            var info = Core.Helpers.KBHelpers.CreateKBItemInfo(detail);
                            if (info != null)
                            {
                                ZKBNotifyService.TryNotify(info);
                                Window?.DispatcherQueue?.TryEnqueue(() =>
                                {
                                    KBItemInfos.Insert(0, info);
                                    if(KBItemInfos.Count > Services.Settings.ZKBSettingService.Setting.MaxKBItems)
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
                Window?.ShowSuccess(Helpers.ResourcesHelper.GetString("ZKBHomePage_Connected"));
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                Window?.ShowError(ex.Message);
            }
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
            _cancellationTokenSource?.Cancel();
        }
    }
}
