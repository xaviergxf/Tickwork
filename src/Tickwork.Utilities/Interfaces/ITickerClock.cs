using System;

namespace Tickwork.Utilities.Interfaces
{
    public interface ITickerClock
    {
        DateTime UtcNow { get; }
    }
}
