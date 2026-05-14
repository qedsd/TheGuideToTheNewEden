using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Helpers
{
    /// <summary>
    /// 文件合并改变事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void FileMergeProgressChangedEventHandler(object sender, int e);

    /// <summary>
    /// 多线程下载器
    /// https://github.com/ldqk/Masuit.Tools/
    /// </summary>
    public class MultiThreadDownloader
    {
        #region 属性

        private string _url;
        private bool _rangeAllowed;
        private readonly HttpWebRequest _request;
        private Action<HttpWebRequest> _requestConfigure = req => req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.122 Safari/537.36";

        #endregion 属性

        #region 公共属性

        /// <summary>
        /// RangeAllowed
        /// </summary>
        public bool RangeAllowed
        {
            get => _rangeAllowed;
            set => _rangeAllowed = value;
        }

        /// <summary>
        /// 临时文件夹
        /// </summary>
        public string TempFileDirectory { get; set; }

        /// <summary>
        /// url地址
        /// </summary>
        public string Url
        {
            get => _url;
            set => _url = value;
        }

        /// <summary>
        /// 第几部分
        /// </summary>
        public int NumberOfParts { get; set; }

        /// <summary>
        /// 已接收字节数
        /// </summary>
        public long TotalBytesReceived
        {
            get
            {
                try
                {
                    lock (this)
                    {
                        return PartialDownloaderList.Where(t => t != null).Sum(t => t.TotalBytesRead);
                    }
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// 总进度
        /// </summary>
        public float TotalProgress { get; private set; }

        /// <summary>
        /// 文件大小
        /// </summary>
        public long Size { get; private set; }

        /// <summary>
        /// 下载速度
        /// </summary>
        public float TotalSpeedInBytes
        {
            get
            {
                try
                {
                    lock (this)
                    {
                        return PartialDownloaderList.Sum(t => t.SpeedInBytes);
                    }
                }
                catch (Exception e)
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// 下载块
        /// </summary>
        public List<PartialDownloader> PartialDownloaderList { get; }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; }

        #endregion 公共属性

        #region 变量

        /// <summary>
        /// 总下载进度更新事件
        /// </summary>
        public event EventHandler TotalProgressChanged;

        /// <summary>
        /// 文件合并完成事件
        /// </summary>
        public event EventHandler FileMergedComplete;

        /// <summary>
        /// 文件合并事件
        /// </summary>
        public event FileMergeProgressChangedEventHandler FileMergeProgressChanged;

        private readonly AsyncOperation _aop;

        #endregion 变量

        #region 下载管理器

        /// <summary>
        /// 多线程下载管理器
        /// </summary>
        /// <param name="sourceUrl"></param>
        /// <param name="tempDir"></param>
        /// <param name="savePath"></param>
        /// <param name="numOfParts"></param>
        public MultiThreadDownloader(string sourceUrl, string tempDir, string savePath, int numOfParts)
        {
            _url = sourceUrl;
            NumberOfParts = numOfParts;
            TempFileDirectory = tempDir;
            PartialDownloaderList = new List<PartialDownloader>();
            _aop = AsyncOperationManager.CreateOperation(null);
            FilePath = savePath;
            _request = WebRequest.Create(sourceUrl) as HttpWebRequest;
        }

        /// <summary>
        /// 多线程下载管理器
        /// </summary>
        /// <param name="sourceUrl"></param>
        /// <param name="savePath"></param>
        /// <param name="numOfParts"></param>
        public MultiThreadDownloader(string sourceUrl, string savePath, int numOfParts) : this(sourceUrl, null, savePath, numOfParts)
        {
            TempFileDirectory = Environment.GetEnvironmentVariable("temp");
        }

        /// <summary>
        /// 多线程下载管理器
        /// </summary>
        /// <param name="sourceUrl"></param>
        /// <param name="savePath"></param>
        public MultiThreadDownloader(string sourceUrl, string savePath) : this(sourceUrl, savePath, Environment.ProcessorCount * 2)
        {
            TempFileDirectory = Environment.GetEnvironmentVariable("temp");
        }

        #endregion 下载管理器

        #region 事件

        private void temp_DownloadPartCompleted(object sender, EventArgs e)
        {
            WaitOrResumeAll(PartialDownloaderList, true);

            if (TotalBytesReceived == Size)
            {
                UpdateProgress();
                MergeParts();
                return;
            }

            PartialDownloaderList.Sort((x, y) => (int)(y.RemainingBytes - x.RemainingBytes));
            var rem = PartialDownloaderList[0].RemainingBytes;
            if (rem < 50 * 1024)
            {
                WaitOrResumeAll(PartialDownloaderList, false);
                return;
            }

            var from = PartialDownloaderList[0].CurrentPosition + rem / 2;
            var to = PartialDownloaderList[0].To;
            if (from > to)
            {
                WaitOrResumeAll(PartialDownloaderList, false);
                return;
            }

            PartialDownloaderList[0].To = from - 1;
            WaitOrResumeAll(PartialDownloaderList, false);
            var temp = new PartialDownloader(_url, TempFileDirectory, Guid.NewGuid().ToString(), from, to, true);
            temp.DownloadPartCompleted += temp_DownloadPartCompleted;
            temp.DownloadPartProgressChanged += temp_DownloadPartProgressChanged;
            lock (this)
            {
                PartialDownloaderList.Add(temp);
            }
            temp.Start(_requestConfigure);
        }

        private void temp_DownloadPartProgressChanged(object sender, EventArgs e)
        {
            UpdateProgress();
        }

        private void UpdateProgress()
        {
            int pr = (int)(TotalBytesReceived * 1d / Size * 100);
            if (TotalProgress != pr)
            {
                TotalProgress = pr;
                if (TotalProgressChanged != null)
                {
                    _aop.Post(state => TotalProgressChanged(this, EventArgs.Empty), null);
                }
            }
        }

        #endregion 事件

        #region 方法

        private void CreateFirstPartitions()
        {
            Size = GetContentLength(ref _rangeAllowed, ref _url);
            int maximumPart = (int)(Size / (25 * 1024));
            maximumPart = maximumPart == 0 ? 1 : maximumPart;
            if (!_rangeAllowed)
            {
                NumberOfParts = 1;
            }
            else if (NumberOfParts > maximumPart)
            {
                NumberOfParts = maximumPart;
            }

            for (int i = 0; i < NumberOfParts; i++)
            {
                var temp = CreateNew(i, NumberOfParts, Size);
                temp.DownloadPartProgressChanged += temp_DownloadPartProgressChanged;
                temp.DownloadPartCompleted += temp_DownloadPartCompleted;
                lock (this)
                {
                    PartialDownloaderList.Add(temp);
                }
                temp.Start(_requestConfigure);
            }
        }

        private void MergeParts()
        {
            var mergeOrderedList = PartialDownloaderList.OrderBy(x => x.From);
            var dir = new FileInfo(FilePath).DirectoryName;
            Directory.CreateDirectory(dir);
            using var fs = File.OpenWrite(FilePath);
            long totalBytesWrite = 0;
            int mergeProgress = 0;
            foreach (var item in mergeOrderedList)
            {
                var pdi = File.OpenRead(item.FullPath);
                byte[] buffer = new byte[4096 * 1024];
                int read;
                while ((read = pdi.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fs.Write(buffer, 0, read);
                    totalBytesWrite += read;
                    int temp = (int)(totalBytesWrite * 1d / Size * 100);
                    if (temp != mergeProgress && FileMergeProgressChanged != null)
                    {
                        mergeProgress = temp;
                        _aop.Post(_ => FileMergeProgressChanged(this, temp), null);
                    }
                }
                try
                {
                    pdi.Dispose();
                    File.Delete(item.FullPath);
                }
                catch
                {
                    // ignored
                }
            }

            if (FileMergedComplete != null)
            {
                _aop.Post(state => FileMergedComplete(state, EventArgs.Empty), this);
            }
        }

        private PartialDownloader CreateNew(int order, int parts, long contentLength)
        {
            var division = contentLength / parts;
            var remaining = contentLength % parts;
            var start = division * order;
            var end = start + division - 1;
            end += order == parts - 1 ? remaining : 0;
            return new PartialDownloader(_url, TempFileDirectory, Guid.NewGuid().ToString(), start, end, true);
        }

        /// <summary>
        /// 暂停或继续
        /// </summary>
        /// <param name="list"></param>
        /// <param name="wait"></param>
        public static void WaitOrResumeAll(List<PartialDownloader> list, bool wait)
        {
            for (var index = 0; index < list.Count; index++)
            {
                if (wait)
                {
                    list[index].Wait();
                }
                else
                {
                    list[index].ResumeAfterWait();
                }
            }
        }

        /// <summary>
        /// 配置请求头
        /// </summary>
        /// <param name="config"></param>
        public void Configure(Action<HttpWebRequest> config)
        {
            _requestConfigure = config;
        }

        /// <summary>
        /// 获取内容长度
        /// </summary>
        /// <param name="rangeAllowed"></param>
        /// <param name="redirectedUrl"></param>
        /// <returns></returns>
        public long GetContentLength(ref bool rangeAllowed, ref string redirectedUrl)
        {
            _request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.122 Safari/537.36";
            _request.ServicePoint.ConnectionLimit = 4;
            _requestConfigure(_request);
            using var resp = _request.GetResponse() as HttpWebResponse;
            redirectedUrl = resp.ResponseUri.OriginalString;
            var ctl = resp.ContentLength;
            rangeAllowed = resp.Headers.AllKeys.Select((v, i) => new
            {
                HeaderName = v,
                HeaderValue = resp.Headers[i]
            }).Any(k => k.HeaderName.ToLower().Contains("range") && k.HeaderValue.ToLower().Contains("byte"));
            _request.Abort();
            return ctl;
        }

        #endregion 方法

        #region 公共方法

        /// <summary>
        /// 暂停下载
        /// </summary>
        public void Pause()
        {
            lock (this)
            {
                foreach (var t in PartialDownloaderList.Where(t => !t.Completed))
                {
                    t.Stop();
                }
            }

            Thread.Sleep(200);
        }

        /// <summary>
        /// 开始下载
        /// </summary>
        public void Start()
        {
            Task th = new Task(CreateFirstPartitions);
            th.Start();
        }

        /// <summary>
        /// 唤醒下载
        /// </summary>
        public void Resume()
        {
            int count = PartialDownloaderList.Count;
            for (int i = 0; i < count; i++)
            {
                if (PartialDownloaderList[i].Stopped)
                {
                    var from = PartialDownloaderList[i].CurrentPosition + 1;
                    var to = PartialDownloaderList[i].To;
                    if (from > to)
                    {
                        continue;
                    }

                    var temp = new PartialDownloader(_url, TempFileDirectory, Guid.NewGuid().ToString(), from, to, _rangeAllowed);
                    temp.DownloadPartProgressChanged += temp_DownloadPartProgressChanged;
                    temp.DownloadPartCompleted += temp_DownloadPartCompleted;
                    lock (this)
                    {
                        PartialDownloaderList.Add(temp);
                    }
                    PartialDownloaderList[i].To = PartialDownloaderList[i].CurrentPosition;
                    temp.Start(_requestConfigure);
                }
            }
        }

        #endregion 公共方法
    }

    public class PartialDownloader
    {
        /// <summary>
        /// 这部分完成事件
        /// </summary>
        public event EventHandler DownloadPartCompleted;

        /// <summary>
        /// 部分下载进度改变事件
        /// </summary>
        public event EventHandler DownloadPartProgressChanged;

        /// <summary>
        /// 部分下载停止事件
        /// </summary>
        public event EventHandler DownloadPartStopped;

        private readonly AsyncOperation _aop = AsyncOperationManager.CreateOperation(null);
        private readonly int[] _lastSpeeds;
        private long _counter;
        private long _to;
        private long _totalBytesRead;
        private bool _wait;

        /// <summary>
        /// 下载已停止
        /// </summary>
        public bool Stopped { get; private set; }

        /// <summary>
        /// 下载已完成
        /// </summary>
        public bool Completed { get; private set; }

        /// <summary>
        /// 下载进度
        /// </summary>
        public int Progress { get; private set; }

        /// <summary>
        /// 下载目录
        /// </summary>
        public string Directory { get; }

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// 已读字节数
        /// </summary>
        public long TotalBytesRead => _totalBytesRead;

        /// <summary>
        /// 内容长度
        /// </summary>
        public long ContentLength { get; private set; }

        /// <summary>
        /// RangeAllowed
        /// </summary>
        public bool RangeAllowed { get; }

        /// <summary>
        /// url
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// to
        /// </summary>
        public long To
        {
            get => _to;

            set
            {
                _to = value;
                ContentLength = _to - From + 1;
            }
        }

        /// <summary>
        /// from
        /// </summary>
        public long From { get; }

        /// <summary>
        /// 当前位置
        /// </summary>
        public long CurrentPosition => From + _totalBytesRead - 1;

        /// <summary>
        /// 剩余字节数
        /// </summary>
        public long RemainingBytes => ContentLength - _totalBytesRead;

        /// <summary>
        /// 完整路径
        /// </summary>
        public string FullPath => Path.Combine(Directory, FileName);

        /// <summary>
        /// 下载速度
        /// </summary>
        public int SpeedInBytes
        {
            get
            {
                if (Completed)
                {
                    return 0;
                }

                int totalSpeeds = _lastSpeeds.Sum();
                return totalSpeeds / 10;
            }
        }

        /// <summary>
        /// 部分块下载
        /// </summary>
        /// <param name="url"></param>
        /// <param name="dir"></param>
        /// <param name="fileGuid"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="rangeAllowed"></param>
        public PartialDownloader(string url, string dir, string fileGuid, long from, long to, bool rangeAllowed)
        {
            From = from;
            _to = to;
            Url = url;
            RangeAllowed = rangeAllowed;
            FileName = fileGuid;
            Directory = dir;
            _lastSpeeds = new int[10];
        }

        private void DownloadProcedure(Action<HttpWebRequest> config)
        {
            using (var file = new FileStream(FullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete))
            {
                var sw = new Stopwatch();
                if (WebRequest.Create(Url) is HttpWebRequest req)
                {
                    req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.122 Safari/537.36";
                    req.AllowAutoRedirect = true;
                    req.MaximumAutomaticRedirections = 5;
                    req.ServicePoint.ConnectionLimit += 1;
                    req.ServicePoint.Expect100Continue = true;
                    req.ProtocolVersion = HttpVersion.Version11;
                    req.Proxy = WebRequest.GetSystemWebProxy();
                    config(req);
                    if (RangeAllowed)
                    {
                        req.AddRange(From, _to);
                    }

                    if (req.GetResponse() is HttpWebResponse resp)
                    {
                        ContentLength = resp.ContentLength;
                        if (ContentLength <= 0 || (RangeAllowed && ContentLength != _to - From + 1))
                        {
                            throw new Exception("Invalid response content");
                        }

                        using var tempStream = resp.GetResponseStream();
                        int bytesRead;
                        byte[] buffer = new byte[4096];
                        sw.Start();
                        while ((bytesRead = tempStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (_totalBytesRead + bytesRead > ContentLength)
                            {
                                bytesRead = (int)(ContentLength - _totalBytesRead);
                            }

                            file.Write(buffer, 0, bytesRead);
                            _totalBytesRead += bytesRead;
                            _lastSpeeds[_counter] = (int)(_totalBytesRead / Math.Ceiling(sw.Elapsed.TotalSeconds));
                            _counter = (_counter >= 9) ? 0 : _counter + 1;
                            int tempProgress = (int)(_totalBytesRead * 100 / ContentLength);
                            if (Progress != tempProgress)
                            {
                                Progress = tempProgress;
                                _aop.Post(state =>
                                {
                                    DownloadPartProgressChanged?.Invoke(this, EventArgs.Empty);
                                }, null);
                            }

                            if (Stopped || (RangeAllowed && _totalBytesRead == ContentLength))
                            {
                                break;
                            }
                        }
                    }

                    req.Abort();
                }

                sw.Stop();
                if (!Stopped && DownloadPartCompleted != null)
                {
                    _aop.Post(state =>
                    {
                        Completed = true;
                        DownloadPartCompleted(this, EventArgs.Empty);
                    }, null);
                }

                if (Stopped && DownloadPartStopped != null)
                {
                    _aop.Post(state => DownloadPartStopped(this, EventArgs.Empty), null);
                }
            }
        }

        /// <summary>
        /// 启动下载
        /// </summary>
        public void Start(Action<HttpWebRequest> config)
        {
            Stopped = false;
            var procThread = new Thread(_ => DownloadProcedure(config));
            procThread.Start();
        }

        /// <summary>
        /// 下载停止
        /// </summary>
        public void Stop()
        {
            Stopped = true;
        }

        /// <summary>
        /// 暂停等待下载
        /// </summary>
        public void Wait()
        {
            _wait = true;
        }

        /// <summary>
        /// 稍后唤醒
        /// </summary>
        public void ResumeAfterWait()
        {
            _wait = false;
        }
    }
}
