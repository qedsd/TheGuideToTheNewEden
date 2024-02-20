using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.PreviewIPC.Memory
{
    public class MemoryIPC: IPreviewIPC
    {
        public MemoryIPC(string sourceHWnd)
        {
            _memoryMappedFile = MemoryMappedFile.CreateOrOpen(sourceHWnd, 1024);
        }
        //定义共享内存第一个int32为标志位，对应IPCOp
        //定义共享内存第二个int32为参数长度，第三个int32开始为具体参数，默认传入参数均为int32
        //各端接收到其需要处理的信息后，需要将标志位重置None，若需要返回参数，还需写入参数长度、参数数据
        //winui端接收的操作仅有UpdateSizeAndPos，由用户调整预览窗口后传进来以修改保存配置
        private MemoryMappedFile _memoryMappedFile;
        public void SendMsg(IPCOp op)
        {
            SendMsg(op, null);
        }
        public void SendMsg(IPCOp op, params int[] p)
        {
            using (MemoryMappedViewStream stream = _memoryMappedFile.CreateViewStream())
            {
                StreamWriter sw = new StreamWriter(stream);
                // 向内存映射文件种写入数据
                StringBuilder stringBuilder= new StringBuilder();
                stringBuilder.Append((int)op);
                if (p?.Length > 0)//有参数
                {
                    for (int i = 0; i < p.Length; i++)
                    {
                        stringBuilder.Append(' ');
                        stringBuilder.Append(p[i]);
                    }
                }
                sw.WriteLine(stringBuilder);
                // 这一句是必须的，在某些情况下，如果不调用Flush 方法会造成进程B读取不到数据
                // 它的作用是立即写入数据
                // 这样在此进程释放 Mutex 的时候，进程B就能正确读取数据了
                sw.Flush();
            }
        }
        public int[] SendAndGetMsg(IPCOp op)
        {
            SendMsg(op, null);
            while (true)
            {
                GetMsg(out IPCOp outOp, out int[] msgs);
                if (outOp == IPCOp.ResultMsg)
                {
                    return msgs;
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
        }
        public void GetMsg(out IPCOp op, out int[] msgs)
        {
            op = IPCOp.None;
            msgs = null;
            using (MemoryMappedViewStream stream = _memoryMappedFile.CreateViewStream())
            {
                StreamReader sr = new StreamReader(stream);
                string str = sr.ReadLine();
                if (!string.IsNullOrEmpty(str))
                {
                    var array = str.Split(' ');
                    if (int.TryParse(array[0], out var outOp))
                    {
                        op = (IPCOp)outOp;
                        if (array.Length > 1)
                        {
                            msgs = new int[array.Length - 1];
                            for (int i = 1; i < array.Length; i++)
                            {
                                int p = int.Parse(array[i]);
                                msgs[i - 1] = p;
                            }
                        }
                    }
                }
            }
        }
        public int[] CheckMsg()
        {
            using (var accessor = _memoryMappedFile.CreateViewAccessor())
            {
                int currentOp = 0;
                while (true)
                {
                    currentOp = accessor.ReadInt32(0);
                    if (currentOp != (int)IPCOp.None)
                    {
                        List<int> msgs = new List<int>()
                        {
                            currentOp
                        };
                        //读取传回来的参数
                        int length = accessor.ReadInt32(sizeof(int));
                        if (length > 0)
                        {
                            for (int i = 0; i < length; i++)
                            {
                                msgs.Add(accessor.ReadInt32((i + 2) * sizeof(int)));
                            }
                        }
                        accessor.Write(0, (int)IPCOp.None);
                        return msgs.ToArray();
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
            }
        }
        public void Dispose()
        {
            _memoryMappedFile?.Dispose();
        }
    }
}
