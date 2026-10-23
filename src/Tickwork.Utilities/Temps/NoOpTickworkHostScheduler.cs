using System;
using System.Threading;
using System.Threading.Tasks;
using Tickwork.Utilities.Interfaces;

namespace Tickwork.Utilities.Temps;

/// <summary>
/// No-operation implementation of ITickworkHostScheduler.
/// Used when background services are disabled (queue-only mode).
/// </summary>
internal class NoOpTickworkHostScheduler : ITickworkHostScheduler
{
    public bool IsRunning => false;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void RestartIfNeeded(DateTime? dateTime)
    {
        // No-op: scheduler not running
    }

    public void Restart()
    {
        // No-op: scheduler not running
    }
}
