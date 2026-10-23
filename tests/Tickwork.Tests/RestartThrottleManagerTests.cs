using Microsoft.Extensions.Time.Testing;

namespace Tickwork.Tests;

public class RestartThrottleManagerTests
{
    // The debounce window is 50 ms. Time is moved by a FakeTimeProvider: the tests used to wait on
    // the wall clock, and a slow machine let the first timer fire before the second request reset it.

    [Fact]
    public void RequestRestart_TriggersCallback_AfterDebounceWindow()
    {
        var time = new FakeTimeProvider();
        var triggered = false;
        using var manager = new RestartThrottleManager(() => triggered = true, time);

        manager.RequestRestart();
        time.Advance(TimeSpan.FromMilliseconds(49));
        Assert.False(triggered);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(triggered);
    }

    [Fact]
    public void MultipleRequests_CoalesceIntoSingleCallback()
    {
        var time = new FakeTimeProvider();
        var triggerCount = 0;
        using var manager = new RestartThrottleManager(() => Interlocked.Increment(ref triggerCount), time);

        // Multiple rapid requests should coalesce
        manager.RequestRestart();
        manager.RequestRestart();
        manager.RequestRestart();

        time.Advance(TimeSpan.FromMilliseconds(200));

        Assert.Equal(1, triggerCount);
    }

    [Fact]
    public void RequestRestart_ResetsTimer_OnSubsequentCalls()
    {
        var time = new FakeTimeProvider();
        var triggerCount = 0;
        using var manager = new RestartThrottleManager(() => Interlocked.Increment(ref triggerCount), time);

        manager.RequestRestart();
        time.Advance(TimeSpan.FromMilliseconds(30)); // Less than debounce window (50ms)
        manager.RequestRestart(); // Should reset the timer
        time.Advance(TimeSpan.FromMilliseconds(30)); // 60ms after the first request, 30ms after the second

        // Should not have triggered yet since timer was reset
        Assert.Equal(0, triggerCount);

        // After full debounce from the last request it should trigger, once
        time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(1, triggerCount);
    }
    [Fact]
    public void Dispose_CanBeCalledSafely()
    {
        var manager = new RestartThrottleManager(() => { });
        var exception = Record.Exception(() => manager.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_BeforeAnyRequest_DoesNotThrow()
    {
        var manager = new RestartThrottleManager(() => { });

        // Timer hasn't been created yet (lazy creation)
        var exception = Record.Exception(() => manager.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_AfterRequest_DoesNotThrow()
    {
        var manager = new RestartThrottleManager(() => { });
        manager.RequestRestart();

        var exception = Record.Exception(() => manager.Dispose());
        Assert.Null(exception);
    }
}
