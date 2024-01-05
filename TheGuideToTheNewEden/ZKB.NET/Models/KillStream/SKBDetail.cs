using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZKB.NET.Models.Killmails;

namespace ZKB.NET.Models.KillStream
{
    /// <summary>
    /// websocket数据流kb信息
    /// </summary>
    public class SKBDetail: KillmailDetail
    {
        public ZkbInfo Zkb {  get; set; }
    }
}
