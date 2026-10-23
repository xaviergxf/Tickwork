using System.Threading;

namespace Tickwork.TickworkThreadPool;

/// <summary>
/// Custom SynchronizationContext that keeps async continuations on Tickwork worker threads.
/// Ensures true concurrency control by preventing execution from switching to ThreadPool.
/// Uses flexible thread assignment for optimal performance and load balancing.
/// </summary>
internal sealed class TickworkSynchronizationContext : SynchronizationContext
{
    private readonly TickworkTaskScheduler _scheduler;

    public TickworkSynchronizationContext(TickworkTaskScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <summary>
    /// Posts async continuations to Tickwork workers with circular dependency protection.
    /// Uses direct execution if called from within a Tickwork worker to prevent deadlocks.
    /// </summary>
    public override void Post(SendOrPostCallback d, object state)
    {
        // Check if we're already on a Tickwork worker thread using ThreadStatic flag
        if (TickworkTaskScheduler.IsTickworkWorkerThread)
        {
            // We're on a Tickwork worker - execute directly to avoid circular queueing
            try 
            { 
                d(state); 
            } 
            catch 
            { 
                /* swallow continuation exceptions */ 
            }
        }
        else
        {
            // We're not on a Tickwork worker - safe to queue the continuation
            _scheduler.PostContinuation(d, state);
        }
    }

    /// <summary>
    /// Sends synchronous operations (not typically used in async scenarios).
    /// </summary>
    public override void Send(SendOrPostCallback d, object state)
    {
        // For synchronous operations, execute directly
        d(state);
    }
}