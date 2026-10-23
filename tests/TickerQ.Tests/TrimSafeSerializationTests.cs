using System.Text.Json;
using TickerQ.Utilities.Interfaces;

namespace TickerQ.Tests;

public sealed class TrimSafeSerializationTests
{
    [Fact]
    public void UpdateNodeHeartBeatAsync_AcceptsJsonElement()
    {
        var method = typeof(ITickerQNotificationHubSender).GetMethod("UpdateNodeHeartBeatAsync");

        Assert.NotNull(method);

        var parameters = method!.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(JsonElement), parameters[0].ParameterType);
    }

    [Fact]
    public void UpdateHostStatus_AcceptsBool()
    {
        var method = typeof(ITickerQNotificationHubSender).GetMethod("UpdateHostStatus");

        Assert.NotNull(method);

        var parameters = method!.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(bool), parameters[0].ParameterType);
    }
}
