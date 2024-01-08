using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZKB.NET.Models.KillStream
{
    public class KillStream
    {
        public ClientWebSocket WebSocket { get; set; }
        public string Url { get;private set; }
        public KillStream(string url)
        {
            Url = url;
            WebSocket = new ClientWebSocket();
        }

        public delegate void MessageEventHandler(object sender, SKBDetail detail, string sourceData);
        public event MessageEventHandler OnMessage;
        public async Task ConnectAsync()
        {
            await WebSocket.ConnectAsync(new Uri(Url),CancellationToken.None);
            _=Task.Run(async() =>
            {
                //全部消息容器
                List<byte> bs = new List<byte>();
                //缓冲区
                var buffer = new byte[1024 * 4];
                //监听Socket信息
                WebSocketReceiveResult result = await WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                //是否关闭
                while (!result.CloseStatus.HasValue)
                {
                    //文本消息
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        bs.AddRange(buffer.Take(result.Count));

                        //消息是否已接收完全
                        if (result.EndOfMessage)
                        {
                            //发送过来的消息
                            string userMsg = Encoding.UTF8.GetString(bs.ToArray(), 0, bs.Count);
                            var detail = JsonConvert.DeserializeObject<SKBDetail>(userMsg);
                            OnMessage?.Invoke(WebSocket, detail, userMsg);
                            //清空消息容器
                            bs = new List<byte>();
                        }
                    }
                    //继续监听Socket信息
                    result = await WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }
            });
        }

        public async Task SendMessageAsync(string Msg)
        {
            if (WebSocket.State != WebSocketState.Open)
            {
                await ConnectAsync();
            }
            if (WebSocket.State == WebSocketState.Open)
            {
                ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(Msg));
                await WebSocket.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public void Close()
        {
            Task.Run(async() =>
            {
                await WebSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "User Close", CancellationToken.None);
                WebSocket?.Abort();
                WebSocket?.Dispose();
            });
        }
    }
}
