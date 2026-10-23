using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tickwork.TickworkThreadPool;

/// <summary>
/// Simple work item structure for the scheduler
/// </summary>
public readonly struct WorkItem
{
    public readonly Func<CancellationToken, Task> Work;
    public readonly CancellationToken UserToken;
    
    public WorkItem(Func<CancellationToken, Task> work, CancellationToken userToken)
    {
        Work = work ?? throw new ArgumentNullException(nameof(work));
        UserToken = userToken;
    }
}

