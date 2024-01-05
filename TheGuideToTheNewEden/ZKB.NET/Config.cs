using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET
{
    public static class Config
    {
        public static bool UseGZip { get; set; } = true;
        public static string ApiUrl { get; set; } = "https://zkillboard.com/api/";
        public static string UserAgent { get; set; } = "ZKB.NET";
        /// <summary>
        /// WebSocket
        /// </summary>
        public static string WWS { get; set; } = "wss://zkillboard.com/websocket/";
    }
}
