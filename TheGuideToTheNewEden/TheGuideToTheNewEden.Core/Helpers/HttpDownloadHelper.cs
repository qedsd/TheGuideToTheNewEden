using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public static class HttpDownloadHelper
    {
        public delegate void DownloadDelegate(double downloaded, double total);
        public static async Task DownloadFileAsync(string url,string destinationPath, DownloadDelegate downloadCallback, CancellationToken cancellationToken = default)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes > 0;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var totalRead = 0L;
                var buffer = new byte[8192]; // 8KB 缓冲区
                var isMoreToRead = true;

                downloadCallback.Invoke(totalRead, totalBytes);

                do
                {
                    var read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (read == 0)
                    {
                        isMoreToRead = false;
                        break;
                    }

                    await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                    totalRead += read;

                    downloadCallback.Invoke(totalRead, totalBytes);

                } while (isMoreToRead && !cancellationToken.IsCancellationRequested);

                if (!cancellationToken.IsCancellationRequested)
                {
                    downloadCallback.Invoke(totalRead, totalBytes);
                }
            }
            catch (Exception ex)
            {
                downloadCallback.Invoke(0, 0);
                throw ex;
            }
        }
    }
}
