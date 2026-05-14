using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public interface IService : IDisposable
    {
        void Init();
    }
}
