using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WoLApp
{
    class StopController
    {
        public static ManualResetEvent Stop { get; } = new ManualResetEvent(false);
    }
}
