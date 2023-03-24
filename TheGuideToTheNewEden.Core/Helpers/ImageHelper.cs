using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public static class ImageHelper
    {
        /// <summary>
        /// 修改尺寸
        /// </summary>
        /// <param name="inputStream">输入照片流</param>
        /// <param name="width">如果为0则依据height等比例缩放</param>
        /// <param name="height">如果为0则依据width等比例缩放</param>
        public static async Task<MemoryStream> ResetSizeAsync(MemoryStream inputStream, int width, int height)
        {
            using (Image image = Image.Load(inputStream.GetBuffer()))
            {
                if (width != 0 && height != 0)
                {
                    image.Mutate(x => x.Resize(width, height));
                }
                else if (width != 0)
                {
                    image.Mutate(x => x.Resize(width, image.Height * (width / image.Width)));
                }
                else if (height != 0)
                {
                    image.Mutate(x => x.Resize(image.Width * (height / image.Height), height));
                }
                MemoryStream stream = new MemoryStream();
                await image.SaveAsync(stream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                stream.Seek(0, SeekOrigin.Begin);
                return stream;
            }
        }
        /// <summary>
        /// 修改尺寸
        /// </summary>
        /// <param name="inputStream">输入照片流</param>
        /// <param name="width">如果为0则依据height等比例缩放</param>
        /// <param name="height">如果为0则依据width等比例缩放</param>
        public static MemoryStream ResetSize(MemoryStream inputStream, int width, int height)
        {
            using (Image image = Image.Load(inputStream.GetBuffer()))
            {
                if (width != 0 && height != 0)
                {
                    image.Mutate(x => x.Resize(width, height));
                }
                else if (width != 0)
                {
                    image.Mutate(x => x.Resize(width, image.Height * (width / image.Width)));
                }
                else if (height != 0)
                {
                    image.Mutate(x => x.Resize(image.Width * (height / image.Height), height));
                }
                MemoryStream stream = new MemoryStream();
                image.Save(stream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                stream.Seek(0, SeekOrigin.Begin);
                return stream;
            }
        }
    }
}
