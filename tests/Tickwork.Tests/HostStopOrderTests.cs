using Microsoft.Extensions.Hosting;
using Tickwork.DependencyInjection;

namespace Tickwork.Tests;

// The bug (1.1.0): the scheduler's StopAsync waited on a semaphore that Dispose had already
// disposed, so a host stopped after it was disposed - what a WebApplicationFactory harness does at
// teardown - failed with "One or more hosted services failed to stop" (ObjectDisposedException:
// SemaphoreSlim). A BackgroundService tolerates every one of these orders; Tickwork must too.
public class HostStopOrderTests
{
    // Bounds a wait that only a hang would exceed; a healthy run never reaches it.
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(30);

    private static async Task<IHost> StartHostAsync()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddTickwork();
        var host = builder.Build();
        host.UseTickwork();
        await host.StartAsync().WaitAsync(HangGuard);
        return host;
    }

    [Fact]
    public async Task A_host_stopped_disposed_and_stopped_again_stops_without_throwing()
    {
        var host = await StartHostAsync();

        await host.StopAsync().WaitAsync(HangGuard);
        host.Dispose();

        var exception = await Record.ExceptionAsync(() => host.StopAsync().WaitAsync(HangGuard));

        Assert.Null(exception);
    }

    [Fact]
    public async Task A_host_disposed_before_it_is_stopped_stops_without_throwing()
    {
        var host = await StartHostAsync();

        host.Dispose();

        var exception = await Record.ExceptionAsync(() => host.StopAsync().WaitAsync(HangGuard));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Two_stops_at_once_both_complete_without_throwing()
    {
        var host = await StartHostAsync();
        try
        {
            var first = host.StopAsync();
            var second = host.StopAsync();

            var exception = await Record.ExceptionAsync(() => Task.WhenAll(first, second).WaitAsync(HangGuard));

            Assert.Null(exception);
        }
        finally
        {
            host.Dispose();
        }
    }

    [Fact]
    public async Task A_stop_whose_token_is_already_cancelled_stops_without_throwing()
    {
        var host = await StartHostAsync();
        try
        {
            var exception = await Record.ExceptionAsync(() => host.StopAsync(new CancellationToken(canceled: true)).WaitAsync(HangGuard));

            Assert.Null(exception);
        }
        finally
        {
            host.Dispose();
        }
    }
}
