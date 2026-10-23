using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Tickwork.BackgroundServices;
using Tickwork.Dispatcher;
using Tickwork.Provider;
using Tickwork.TickworkThreadPool;
using Tickwork.Utilities;
using Tickwork.Utilities.Entities;
using Tickwork.Utilities.Enums;
using Tickwork.Utilities.Instrumentation;
using Tickwork.Utilities.Interfaces;
using Tickwork.Utilities.Interfaces.Managers;
using Tickwork.Utilities.Managers;
using Tickwork.Utilities.Temps;

namespace Tickwork.DependencyInjection
{
    public static class TickworkServiceExtensions
    {
        public static IServiceCollection AddTickwork(this IServiceCollection services, Action<TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity>> optionsBuilder = null)
            => AddTickwork<TimeTickerEntity, CronTickerEntity>(services, optionsBuilder);

        public static IServiceCollection AddTickwork<TTimeTicker, TCronTicker>(this IServiceCollection services, Action<TickerOptionsBuilder<TTimeTicker, TCronTicker>> optionsBuilder = null)
            where TTimeTicker : TimeTickerEntity<TTimeTicker>, new()
            where TCronTicker : CronTickerEntity, new()
        {
            var tickerExecutionContext = new TickerExecutionContext();
            var schedulerOptionsBuilder = new SchedulerOptionsBuilder();
            var optionInstance = new TickerOptionsBuilder<TTimeTicker,TCronTicker>(tickerExecutionContext, schedulerOptionsBuilder);
            optionsBuilder?.Invoke(optionInstance);
            CronScheduleCache.TimeZoneInfo = schedulerOptionsBuilder.SchedulerTimeZone;

            // Apply JSON serializer options for ticker requests if configured during service registration
            if (optionInstance.RequestJsonSerializerOptions != null)
            {
                TickerHelper.RequestJsonSerializerOptions = optionInstance.RequestJsonSerializerOptions;
            }

            // Configure whether ticker request payloads should use GZip compression
            TickerHelper.UseGZipCompression = optionInstance.RequestGZipCompressionEnabled;
            services.AddSingleton<ITimeTickerManager<TTimeTicker>, TickerManager<TTimeTicker, TCronTicker>>();
            services.AddSingleton<ICronTickerManager<TCronTicker>, TickerManager<TTimeTicker, TCronTicker>>();
            services.AddSingleton<IInternalTickerManager, InternalTickerManager<TTimeTicker, TCronTicker>>();
            services.AddSingleton<ITickworkRedisContext, NoOpTickworkRedisContext>();
            services.AddSingleton<ITickworkNotificationHubSender, NoOpTickworkNotificationHubSender>();
            services.AddSingleton<ITickerClock, TickerSystemClock>();

            // Register the initializer hosted service BEFORE scheduler services
            // to guarantee seeding completes before the scheduler starts polling.
            // Registered as a singleton so UseTickwork can resolve it to set the initialization flag.
            services.AddSingleton<TickworkInitializerHostedService>();
            services.AddHostedService(sp => sp.GetRequiredService<TickworkInitializerHostedService>());

            // Only register background services if enabled (default is true)
            if (optionInstance.RegisterBackgroundServices)
            {
                services.AddSingleton<TickworkSchedulerBackgroundService>();
                services.AddSingleton<ITickworkHostScheduler>(provider =>
                    provider.GetRequiredService<TickworkSchedulerBackgroundService>());
                services.AddHostedService(provider =>
                    provider.GetRequiredService<TickworkSchedulerBackgroundService>());
                services.AddHostedService(provider => provider.GetRequiredService<TickworkFallbackBackgroundService>());
                services.AddSingleton<TickworkFallbackBackgroundService>();
                services.AddSingleton<ITickworkDispatcher, TickworkDispatcher>();
                services.AddSingleton<ITickworkTaskScheduler>(sp =>
                {
                    var notification = sp.GetRequiredService<ITickworkNotificationHubSender>();
                    var notifyDebounce = new SoftSchedulerNotifyDebounce((value) => notification.UpdateActiveThreads(value));
                    return new TickworkTaskScheduler(schedulerOptionsBuilder.MaxConcurrency, schedulerOptionsBuilder.IdleWorkerTimeOut, notifyDebounce);
                });
            }
            else
            {
                services.AddSingleton<ITickworkTaskScheduler>(_ => new TickworkTaskScheduler(schedulerOptionsBuilder.MaxConcurrency, schedulerOptionsBuilder.IdleWorkerTimeOut));
                // Register NoOp implementations when background services are disabled
                services.AddSingleton<ITickworkHostScheduler, NoOpTickworkHostScheduler>();
                services.AddSingleton<ITickworkDispatcher, NoOpTickworkDispatcher>();
            }
            services.AddSingleton<ITickerFunctionConcurrencyGate, TickerFunctionConcurrencyGate>();
            services.AddSingleton<ITickerExecutionTaskHandler, TickerExecutionTaskHandler>();
            services.AddSingleton<ITickworkInstrumentation, LoggerInstrumentation>();

            optionInstance.ExternalProviderConfigServiceAction?.Invoke(services);

            // Register in-memory persistence as fallback — only used when no external provider
            // (EF Core, Redis, SDK) has been configured via ExternalProviderConfigServiceAction above.
            services.TryAddSingleton<ITickerPersistenceProvider<TTimeTicker, TCronTicker>, TickerInMemoryPersistenceProvider<TTimeTicker, TCronTicker>>();

            if (optionInstance.TickerExceptionHandlerType != null)
                services.AddSingleton(typeof(ITickerExceptionHandler), optionInstance.TickerExceptionHandlerType);

            services.AddSingleton(_ => optionInstance);
            services.AddSingleton(_ => tickerExecutionContext);
            services.AddSingleton(_ => schedulerOptionsBuilder);

            // Register AFTER initializer and scheduler to ensure it runs last
            services.AddHostedService<TickworkStartupValidator>();

            return services;
        }

        /// <summary>
        /// Initializes Tickwork for generic host applications (Console, MAUI, WPF, Worker Services, etc.).
        ///
        /// This method configures middleware and scheduler settings. All I/O-bound
        /// initialization (function discovery, database seeding, external provider startup)
        /// is deferred to <see cref="TickworkInitializerHostedService"/> which runs when
        /// the host starts. This means design-time tools that build the host without
        /// starting it (OpenAPI generators, EF migration tools, etc.) will not trigger
        /// database operations.
        /// </summary>
        public static IHost UseTickwork(this IHost host, TickworkStartMode qStartMode = TickworkStartMode.Immediate)
        {
            var serviceProvider = host.Services;
            var tickerExecutionContext = serviceProvider.GetService<TickerExecutionContext>();
            var notificationHubSender = serviceProvider.GetService<ITickworkNotificationHubSender>();
            var backgroundScheduler = serviceProvider.GetService<TickworkSchedulerBackgroundService>();

            // Signal the initializer hosted service that UseTickwork was called,
            // so it should perform startup I/O when the host starts.
            var initializer = serviceProvider.GetService<TickworkInitializerHostedService>();
            if (initializer != null)
                initializer.InitializationRequested = true;

            // If background services are registered, configure them
            if (backgroundScheduler != null)
            {
                backgroundScheduler.SkipFirstRun = qStartMode == TickworkStartMode.Manual;

                tickerExecutionContext.NotifyCoreAction += (value, type) =>
                {
                    if (type == CoreNotifyActionType.NotifyHostExceptionMessage)
                    {
                        notificationHubSender.UpdateHostException((string)value);
                        tickerExecutionContext.LastHostExceptionMessage = (string)value;
                    }
                    else if (type == CoreNotifyActionType.NotifyNextOccurence)
                        notificationHubSender.UpdateNextOccurrence(value is DateTime dt ? (DateTime?)dt : null);
                    else if (type == CoreNotifyActionType.NotifyHostStatus)
                        notificationHubSender.UpdateHostStatus(value is bool b && b);
                    else if (type == CoreNotifyActionType.NotifyThreadCount)
                        notificationHubSender.UpdateActiveThreads((string)value);
                };
            }
            // If background services are not registered (due to DisableBackgroundServices()),
            // silently skip background service configuration. This is expected behavior.

            return host;
        }
    }
}
