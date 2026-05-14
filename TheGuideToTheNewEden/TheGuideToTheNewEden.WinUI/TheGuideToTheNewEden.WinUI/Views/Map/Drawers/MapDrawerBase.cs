using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Models.Map;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace TheGuideToTheNewEden.WinUI.Views.Map.Drawers
{
    public abstract class MapDrawerBase : IMapDrawer
    {
        public bool Enable { get; set; } = true;
        public event EventHandler DrawRequsted;
        public event EventHandler<string> OnError;
        protected MapCanvas _mapCanvas;
        public void RequstDraw()
        {
            DrawRequsted?.Invoke(this, EventArgs.Empty);
        }
        public void Error(string error)
        {
            OnError?.Invoke(this, error);
        }
        public abstract void Draw(CanvasControl sender, CanvasDrawEventArgs args, Dictionary<int, MapData> allDatas, IEnumerable<MapData> visibleDatas, float zoom, bool drawBorder, Windows.UI.Color mainTextColor);
        public abstract void Close();
        public virtual void Stop()
        {

        }

        public virtual void Start()
        {

        }

        public virtual bool GetEnable()
        {
            return Enable;
        }

        public virtual void SetEnable(bool enable)
        {
            Enable = enable;
        }

        public void SetMapCanvas(MapCanvas mapCanvas)
        {
            _mapCanvas = mapCanvas;
        }

        /// <summary>
        /// 解压缩从图片服务器下载过来的图片
        /// </summary>
        /// <param name="compressedImageData"></param>
        /// <returns></returns>
        public static byte[] GetRawPixelDataSync(byte[] compressedImageData)
        {
            // 创建内存流
            using (var memoryStream = new MemoryStream(compressedImageData))
            using (var randomAccessStream = memoryStream.AsRandomAccessStream())
            {
                // 创建BitmapDecoder
                var decoder = BitmapDecoder.CreateAsync(randomAccessStream).GetAwaiter().GetResult();

                // 获取像素数据
                var pixelDataProvider = decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    new BitmapTransform(),
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.DoNotColorManage
                ).GetAwaiter().GetResult();

                return pixelDataProvider.DetachPixelData().ToArray();
            }
        }
        public static SoftwareBitmap GetRawPixelDataFromSoftwareBitmap(byte[] compressedImageData)
        {
            //using (var stream = new InMemoryRandomAccessStream())
            {
                var stream = new InMemoryRandomAccessStream();
                // 写入数据
                stream.WriteAsync(compressedImageData.AsBuffer()).AsTask().Wait();
                stream.Seek(0);

                // 创建解码器
                var decoder = BitmapDecoder.CreateAsync(stream).AsTask().Result;

                // 获取SoftwareBitmap
                var softwareBitmap = decoder.GetSoftwareBitmapAsync().AsTask().Result;

                // 转换为BGRA8格式（如果需要）
                if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
                {
                    softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8);
                }
                return softwareBitmap;
                //// 获取像素数据
                //byte[] pixelData = new byte[softwareBitmap.PixelWidth * softwareBitmap.PixelHeight * 4];
                //softwareBitmap.CopyToBuffer(pixelData.AsBuffer());

                //return pixelData;
            }
        }
        public static SoftwareBitmap CreateFromBytesViaWriteableBitmap(byte[] pixelData, int width, int height)
        {
            // 创建WriteableBitmap
            var writeableBitmap = new WriteableBitmap(width, height);

            // 使用Stream复制像素数据
            using (var stream = writeableBitmap.PixelBuffer.AsStream())
            {
                stream.Write(pixelData, 0, pixelData.Length);
            }

            // 从WriteableBitmap创建SoftwareBitmap
            var softwareBitmap = SoftwareBitmap.CreateCopyFromBuffer(
                writeableBitmap.PixelBuffer,
                BitmapPixelFormat.Bgra8,
                width,
                height
            );

            return softwareBitmap;
        }
        public static CanvasBitmap CreateCanvasBitmap(CanvasControl sender, CanvasDrawEventArgs args, byte[] img, int pixel)
        {
            //return CanvasBitmap.CreateFromSoftwareBitmap(sender.Device, CreateFromBytesViaWriteableBitmap(GetRawPixelDataSync(img), pixel, pixel));
            return CanvasBitmap.CreateFromBytes(sender.Device, GetRawPixelDataSync(img), pixel, pixel, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8X8UIntNormalized);
        }
    }
}
