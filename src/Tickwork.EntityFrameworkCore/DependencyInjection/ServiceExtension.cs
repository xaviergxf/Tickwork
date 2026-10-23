using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Tickwork.Utilities;
using Tickwork.Utilities.Entities;
using Tickwork.Utilities.Instrumentation;
using Tickwork.Utilities.Interfaces.Managers;
using Tickwork.Utilities.Managers;
using Tickwork.Utilities.Interfaces;

namespace Tickwork.EntityFrameworkCore.DependencyInjection;

public static class ServiceExtension
{
        
    public static TickerOptionsBuilder<TTimeTicker, TCronTicker> AddOperationalStore<TTimeTicker, TCronTicker>(this TickerOptionsBuilder<TTimeTicker,TCronTicker> tickerConfiguration, Action<TickworkEfCoreOptionBuilder<TTimeTicker, TCronTicker>> efConfiguration = null)
        where TTimeTicker : TimeTickerEntity<TTimeTicker>, new()
        where TCronTicker : CronTickerEntity, new()
    {
        var efCoreOptionBuilder = new TickworkEfCoreOptionBuilder<TTimeTicker, TCronTicker>();

        efConfiguration?.Invoke(efCoreOptionBuilder);
            
        if (efCoreOptionBuilder.PoolSize <= 0) 
            throw new ArgumentOutOfRangeException(nameof(efCoreOptionBuilder.PoolSize), "Pool size must be greater than 0");
            
        tickerConfiguration.ExternalProviderConfigServiceAction += (services) 
            => services.AddSingleton(_ => efCoreOptionBuilder);
            
        tickerConfiguration.ExternalProviderConfigServiceAction += efCoreOptionBuilder.ConfigureServices;
            
        UseApplicationService(tickerConfiguration, efCoreOptionBuilder);
            
        return tickerConfiguration;
    }
        
    private static void UseApplicationService<TTimeTicker, TCronTicker>(TickerOptionsBuilder<TTimeTicker, TCronTicker> tickerConfiguration, TickworkEfCoreOptionBuilder<TTimeTicker, TCronTicker> options)
        where TTimeTicker : TimeTickerEntity<TTimeTicker>, new()
        where TCronTicker : CronTickerEntity, new()
    {
        tickerConfiguration.UseExternalProviderApplication((serviceProvider) =>
        {
            var internalTickerManager = serviceProvider.GetRequiredService<IInternalTickerManager>();
            var hostLifetime = serviceProvider.GetService<IHostApplicationLifetime>();
            var schedulerOptions = serviceProvider.GetService<SchedulerOptionsBuilder>();
            var hostScheduler = serviceProvider.GetService<ITickworkHostScheduler>();

            hostLifetime.ApplicationStarted.Register(() =>
            {
                Task.Run(async () =>
                {
                    // Release resources held by dead nodes before the scheduler starts processing.
                    await internalTickerManager.ReleaseDeadNodeResources(schedulerOptions.NodeIdentifier);
                                        
                    // After cleanup, restart the host scheduler so it immediately
                    // picks up newly seeded cron tickers and jobs configured via the core pipeline.
                    if (hostScheduler != null && hostScheduler.IsRunning)
                    {
                        hostScheduler.Restart();
                    }
                });
            });
        });
    }
}
