using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.PreviewIPC;
using TheGuideToTheNewEden.PreviewIPC.Memory;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal static class MemoryIPCService
    {
        private static List<MemoryIPC> _memoryIPCs;
        private static List<MemoryIPC> MemoryIPCs
        {
            get
            {
                _memoryIPCs ??= new List<MemoryIPC>();
                return _memoryIPCs;
            }
        }

        public static MemoryIPC Create(IntPtr hwnd)
        {
            var m = new MemoryIPC(hwnd.ToString());
            MemoryIPCs.Add(m);
            return m;
        }

        public static void Dispose(IPreviewIPC previewIPC)
        {
            var memoryIPC = previewIPC as MemoryIPC;
            if (memoryIPC != null)
            {
                memoryIPC.SendMsg(IPCOp.Close);
                memoryIPC.Dispose();
                MemoryIPCs.Remove(memoryIPC);
            }
        }
        public static void Dispose()
        {
            foreach(var p in MemoryIPCs)
            {
                p.SendMsg(IPCOp.Close);
                p.Dispose();
            }
        }
    }
}
