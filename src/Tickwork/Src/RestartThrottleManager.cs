using System;
using System.Threading;

namespace Tickwork;

public sealed class RestartThrottleManager : IDisposable
{
    private readonly Action _onRestartTriggered;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    private ITimer _debounceTimer;
    private volatile bool _restartPending;

    private readonly TimeSpan _debounceWindow = TimeSpan.FromMilliseconds(50);

    public RestartThrottleManager(Action onRestartTriggered)
        : this(onRestartTriggered, TimeProvider.System)
    {
    }

    internal RestartThrottleManager(Action onRestartTriggered, TimeProvider timeProvider)
    {
        _onRestartTriggered = onRestartTriggered;
        _timeProvider = timeProvider;
    }

    public void RequestRestart()
    {
        lock (_lock)
        {
            _restartPending = true;

            // Create timer only when first needed
            if (_debounceTimer == null)
            {
                _debounceTimer = _timeProvider.CreateTimer(OnTimerCallback, null,
                    _debounceWindow, Timeout.InfiniteTimeSpan);
            }
            else
            {
                // Just reset existing timer
                _debounceTimer.Change(_debounceWindow, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnTimerCallback(object state)
    {
        lock (_lock)
        {
            if (_restartPending)
            {
                _restartPending = false;
                _debounceTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _onRestartTriggered();
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }
}
