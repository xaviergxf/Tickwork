using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Tickwork.Utilities.Interfaces;

internal interface ITickworkRedisContext
{
    IDistributedCache DistributedCache { get; }
    public bool HasRedisConnection { get; }
    Task<TResult[]> GetOrSetArrayAsync<TResult>(
        string cacheKey,
        Func<CancellationToken, Task<TResult[]>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) where TResult : class;

    Task<string[]>GetDeadNodesAsync();

    Task NotifyNodeAliveAsync();
}