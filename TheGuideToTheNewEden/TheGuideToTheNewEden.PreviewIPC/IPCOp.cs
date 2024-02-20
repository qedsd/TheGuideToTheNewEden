using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.PreviewIPC
{
    public enum IPCOp
    {
        None,
        ActiveSourceWindow,
        Close,
        Hide,
        Show,
        UpdateThumbnail,
        Highlight,
        CancelHighlight,
        SetSize,
        SetPos,
        GetSizeAndPos,
        GetWidth,
        GetHeight,
        UpdateSizeAndPos,
        ResultMsg
    }
}
