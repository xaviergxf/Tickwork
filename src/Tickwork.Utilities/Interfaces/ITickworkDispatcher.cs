using System.Threading;
using System.Threading.Tasks;
using Tickwork.Utilities.Models;

namespace Tickwork.Utilities.Interfaces
{
    public interface ITickworkDispatcher
    {
        /// <summary>
        /// Indicates whether the dispatcher is functional (background services enabled).
        /// When false, DispatchAsync will be a no-op.
        /// </summary>
        bool IsEnabled { get; }
        
        Task DispatchAsync(InternalFunctionContext[] contexts, CancellationToken cancellationToken = default);
    }
}

