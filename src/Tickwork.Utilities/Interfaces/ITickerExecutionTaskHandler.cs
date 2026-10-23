using System.Threading;
using System.Threading.Tasks;
using Tickwork.Utilities.Models;

namespace Tickwork.Utilities.Interfaces
{
    public interface ITickerExecutionTaskHandler
    {
        Task ExecuteTaskAsync(InternalFunctionContext context, bool isDue, CancellationToken cancellationToken = default);
    }
}

