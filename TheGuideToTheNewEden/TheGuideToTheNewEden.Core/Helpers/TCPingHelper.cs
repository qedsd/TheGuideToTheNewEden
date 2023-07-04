using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public static class TCPingHelper
    {
        private static Stopwatch _stopwatch;
        public static async Task<double> Ping(string host, int port)
        {
            if(_stopwatch == null)
            {
                _stopwatch = new Stopwatch();
            }
            _stopwatch.Restart();
            var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.ReceiveTimeout = 5000;
            sock.Blocking = true;
            try
            {
                await sock.ConnectAsync(host, port);
                _stopwatch.Stop();
                sock.Disconnect(false);
                sock.Dispose();
                return Math.Round(_stopwatch.Elapsed.TotalMilliseconds, 0);
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                _stopwatch.Stop();
                sock.Dispose();
                return 0;
            }
        }
    }
}
