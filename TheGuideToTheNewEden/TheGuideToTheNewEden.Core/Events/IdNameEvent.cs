using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Events
{
    public class IdNameEvent
    {
        public delegate void IdNameClickedEventHandel(IdName idName);
    }
}
