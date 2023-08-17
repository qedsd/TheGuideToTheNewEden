using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    public static class ImageHelper
    {
        public static Bitmap ResizeBitmap(Bitmap source,int w, int h)
        {
            return new Bitmap(source, new Size(w, h));
        }
        public static Bitmap CutBitmap(Bitmap source, Rectangle destinationRect)
        {
            var destinationImage = new Bitmap(destinationRect.Width, destinationRect.Height);
            destinationImage.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            using (var graphics = Graphics.FromImage(destinationImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, destinationImage.Width, destinationImage.Height), destinationRect, GraphicsUnit.Pixel);
            }
            return destinationImage;
        }

        public static BitmapImage ImageConvertToBitmapImage(Image img)
        {
            using (var memory = new MemoryStream())
            {
                img.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.SetSource(memory.AsRandomAccessStream());
                return bitmapImage;
            }
        }
        public static WriteableBitmap ImageConvertToWriteableBitmap(Image img)
        {
            using (var memory = new MemoryStream())
            {
                img.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                var writeableBitmap = new WriteableBitmap(img.Width, img.Height);
                writeableBitmap.SetSource(memory.AsRandomAccessStream());
                return writeableBitmap;
            }
        }
        public static WriteableBitmap MemoryStreamConvertToWriteableBitmap(int w, int h, MemoryStream stream)
        {
            var writeableBitmap = new WriteableBitmap(w, h);
            writeableBitmap.SetSource(stream.AsRandomAccessStream());
            return writeableBitmap;
        }

        
    }
}
