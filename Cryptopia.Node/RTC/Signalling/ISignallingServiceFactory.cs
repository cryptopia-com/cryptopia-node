using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cryptopia.Node.RTC.Signalling
{
    public interface ISignallingServiceFactory
    {
        ISignallingService Create(string endpoint);
    }
}
