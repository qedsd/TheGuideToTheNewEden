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
                    (Helpers.WindowHelper.MainWindow as BaseWindow)?.ShowError(ex.Message);
                }
            });
        }
    }
}
