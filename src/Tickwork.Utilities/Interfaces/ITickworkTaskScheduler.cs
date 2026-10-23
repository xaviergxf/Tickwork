using System;
using System.Threading;
using System.Threading.Tasks;
using Tickwork.Utilities.Enums;

namespace Tickwork.Utilities.Interfaces
{
    /// <summary>
    /// Abstraction for the internal Tickwork task scheduler.
    /// </summary>
    public interface ITickworkTaskScheduler
    {
        ValueTask QueueAsync(
            Func<CancellationToken, Task> work,
            TickerTaskPriority priority,
            CancellationToken cancellationToken = default);

        void Freeze();

        void Resume();

        bool IsFrozen { get; }

        int ActiveWorkers { get; }

        int TotalQueuedTasks { get; }

        bool IsDisposed { get; }

        string GetDiagnostics();

        Task<bool> WaitForRunningTasksAsync(TimeSpan? timeout = null);
    }
}

