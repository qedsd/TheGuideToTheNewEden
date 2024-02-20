using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.PreviewIPC
{
    public interface IPreviewIPC
    {
        public void SendMsg(IPCOp op, params int[] p);
        public void SendMsg(IPCOp op);
        public int[] GetMsg(IPCOp op);
        public int[] CheckMsg();
        public void Dispose();
    }
}
