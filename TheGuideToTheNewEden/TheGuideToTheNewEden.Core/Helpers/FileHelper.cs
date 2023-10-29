using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public static class FileHelper
    {
        /// <summary>
        /// 共享模式读取文本文件行
        /// </summary>
        /// <param name="path"></param>
        /// <param name="offset"></param>
        /// <param name="readOffset"></param>
        /// <returns></returns>
        public static List<string> ReadLines(string path, long offset, Encoding encoding, out int readOffset)
        {
            List<string> newLines = null;
            readOffset = 0;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] b = new byte[1024];
                int curReadCount = 0;
                StringBuilder stringBuilder = new StringBuilder();
                fs.Seek(offset, SeekOrigin.Begin);
                while ((curReadCount = fs.Read(b, 0, b.Length)) > 0)
                {
                    readOffset += curReadCount;
                    var content = encoding.GetString(b, 0, curReadCount);
                    stringBuilder.Append(content);
                }
                if (stringBuilder.Length > 0)
                {
                    newLines = stringBuilder.ToString().Split(new char[] { '\n', '\r' }).ToList();
                }
            }
            return newLines;
        }
        public static long GetStreamLength(string path)
        {
            long length = 0;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                length = fs.Length;
            }
            return length;
        }
        public static ulong GetFileLength(string file)
        {
            //用来获取高位数字(只有在读取超过4GB的文件才需要用到该参数)
            uint h = 0;
            //用来获取低位数据
            uint l = GetCompressedFileSize(file, ref h);
            //将两个int32拼接成一个int64
            return ((ulong)h << 32) + l;
        }
        [DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
        private static extern uint GetCompressedFileSize(string fileName, ref uint fileSizeHigh);
    }
}
