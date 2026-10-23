using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tickwork.Utilities;
using Tickwork.Utilities.Enums;

namespace Tickwork.BackgroundServices;

internal class TickworkStartupValidator : IHostedService
{
    private readonly TickerExecutionContext _executionContext;
    private readonly TickworkInitializerHostedService _initializer;
    private readonly ILogger<TickworkStartupValidator> _logger;

    public TickworkStartupValidator(
        TickerExecutionContext executionContext,
        TickworkInitializerHostedService initializer,
        ILogger<TickworkStartupValidator> logger)
    {
        _executionContext = executionContext;
        _initializer = initializer;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_initializer.InitializationRequested)
        {
            const string message = "Tickwork — UseTickwork() was not called. Call app.UseTickwork() before app.Run() to initialize the scheduler.";
            _logger.LogWarning(message);
            _executionContext.NotifyCoreAction?.Invoke(message, CoreNotifyActionType.NotifyHostExceptionMessage);
        }
        else if (TickerFunctionProvider.TickerFunctions.Count == 0)
        {
            const string message = "Tickwork — No ticker functions registered. Ensure you have methods decorated with [TickerFunction].";
            _logger.LogWarning(message);
            _executionContext.NotifyCoreAction?.Invoke(message, CoreNotifyActionType.NotifyHostExceptionMessage);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
