using System;
using Tickwork.Utilities.Interfaces;

namespace Tickwork
{
    internal class TickerSystemClock : ITickerClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
