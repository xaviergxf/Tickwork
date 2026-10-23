using System;
using System.Threading.Tasks;
using Tickwork.Utilities.Enums;

namespace Tickwork.Utilities.Interfaces
{
    public interface ITickerExceptionHandler
    {
        public Task HandleExceptionAsync(Exception exception, Guid tickerId, TickerType tickerType);
        public Task HandleCanceledExceptionAsync(Exception exception, Guid tickerId, TickerType tickerType);
    }
}
