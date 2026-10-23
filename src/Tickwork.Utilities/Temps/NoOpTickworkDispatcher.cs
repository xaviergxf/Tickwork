using System.Threading;
using System.Threading.Tasks;
using Tickwork.Utilities.Interfaces;
using Tickwork.Utilities.Models;

namespace Tickwork.Utilities.Temps;

/// <summary>
/// No-operation implementation of ITickworkDispatcher.
/// Used when background services are disabled (queue-only mode).
/// </summary>
internal class NoOpTickworkDispatcher : ITickworkDispatcher
{
    public bool IsEnabled => false;
    
    public Task DispatchAsync(InternalFunctionContext[] contexts, CancellationToken cancellationToken = default)
    {
        // No-op: dispatcher not available in queue-only mode
        return Task.CompletedTask;
    }

    public Task DispatchAsync(InternalFunctionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
