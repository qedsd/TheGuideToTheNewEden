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
            // 使用 MemoryMappedViewAccessor 类来创建一个视图，可以通过它来读写共享内存区域
            using (var accessor = _memoryMappedFile.CreateViewAccessor())
            {
                accessor.Write(0, (int)op);
                if (p?.Length > 0)//有参数
                {
                    accessor.Write(sizeof(int), p.Length);
                    //List<int> ps = new List<int>(p.Length + 1)
                    //{
                    //    (int)op
                    //};
                    //ps.AddRange(p);
                    long position = 2;
                    for (int i = 0; i < p.Length; i++)
                    {
                        accessor.Write(position + i * sizeof(int), p[i]);
                    }
                }
                else//无参数
                {
                    accessor.Write(0, (int)op);
                }
            }
        }
        public int[] GetMsg(IPCOp op)
        {
            // 使用 MemoryMappedViewAccessor 类来创建一个视图，可以通过它来读写共享内存区域
            using (var accessor = _memoryMappedFile.CreateViewAccessor())
            {
                accessor.Write(0, (int)op);
                int currentOp = 0;
                while (true)
                {
                    currentOp = accessor.ReadInt32(0);
                    if (currentOp == (int)IPCOp.None)
                    {
                        //读取传回来的参数
                        int length = accessor.ReadInt32(1);
                        if (length > 0)
                        {
                            int[] results = new int[length];
                            for (int i = 0; i < length; i++)
                            {
                                results[i] = accessor.ReadInt32(i + 2);
                            }
                            return results;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
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
