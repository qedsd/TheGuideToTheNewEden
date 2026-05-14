using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Extensions
{
    public static class DispatcherQueueExtension
    {
        public static void SafelyTryEnqueue(this Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue, Action action)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action?.Invoke();
                }
                catch(Exception ex)
                {
                    ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowMsg(string.Empty, ex.Message, Controls.InfoBarControl.InfoType.Error, false);
                }
            });
        }
    }
}
