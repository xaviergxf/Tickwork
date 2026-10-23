using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tickwork.Utilities.Interfaces
{
    public interface ITickworkHostScheduler
    {
        public bool IsRunning { get; }
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
        void RestartIfNeeded(DateTime? dateTime);
        void Restart();
    }
}
