using System;
using System.Threading;
using Tickwork.Utilities;
using Tickwork.Utilities.Enums;
using Tickwork.Utilities.Models;
using Xunit;

namespace Tickwork.Tests;

[Collection("TickerCancellationTokenState")]
public class TickerCancellationTokenManagerTests : IDisposable
{
    public void Dispose()
    {
        TickerCancellationTokenManager.CleanUpTickerCancellationTokens();
    }

    [Fact]
    public void RequestCancellationById_Cancels_The_Token()
    {
        var cts = new CancellationTokenSource();
        var tickerId = Guid.NewGuid();
        var context = MakeContext(tickerId);

        TickerCancellationTokenManager.AddTickerCancellationToken(cts, context, isDue: false);

        var token = cts.Token;
        Assert.False(token.IsCancellationRequested);

        var result = TickerCancellationTokenManager.RequestTickerCancellationById(tickerId);

        Assert.True(result);
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void RequestCancellationById_Returns_False_For_Unknown_Id()
    {
        var result = TickerCancellationTokenManager.RequestTickerCancellationById(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public void IsParentRunning_Returns_True_When_Ticker_Registered()
    {
        var cts = new CancellationTokenSource();
        var parentId = Guid.NewGuid();
        var tickerId = Guid.NewGuid();
        var context = MakeContext(tickerId, parentId);

        TickerCancellationTokenManager.AddTickerCancellationToken(cts, context, isDue: false);

        Assert.True(TickerCancellationTokenManager.IsParentRunning(parentId));
    }

    [Fact]
    public void IsParentRunning_Returns_False_After_Removal()
    {
        var cts = new CancellationTokenSource();
        var parentId = Guid.NewGuid();
        var tickerId = Guid.NewGuid();
        var context = MakeContext(tickerId, parentId);

        TickerCancellationTokenManager.AddTickerCancellationToken(cts, context, isDue: false);
        TickerCancellationTokenManager.RemoveTickerCancellationToken(tickerId);

        Assert.False(TickerCancellationTokenManager.IsParentRunning(parentId));
    }

    [Fact]
    public void IsParentRunningExcludingSelf_Returns_False_When_Only_Self()
    {
        var cts = new CancellationTokenSource();
        var parentId = Guid.NewGuid();
        var tickerId = Guid.NewGuid();
        var context = MakeContext(tickerId, parentId);

        TickerCancellationTokenManager.AddTickerCancellationToken(cts, context, isDue: false);

        Assert.False(TickerCancellationTokenManager.IsParentRunningExcludingSelf(parentId, tickerId));
    }

    [Fact]
    public void IsParentRunningExcludingSelf_Returns_True_When_Sibling_Exists()
    {
        var parentId = Guid.NewGuid();
        var ticker1 = Guid.NewGuid();
        var ticker2 = Guid.NewGuid();

        TickerCancellationTokenManager.AddTickerCancellationToken(
            new CancellationTokenSource(), MakeContext(ticker1, parentId), isDue: false);
        TickerCancellationTokenManager.AddTickerCancellationToken(
            new CancellationTokenSource(), MakeContext(ticker2, parentId), isDue: false);

        Assert.True(TickerCancellationTokenManager.IsParentRunningExcludingSelf(parentId, ticker1));
    }

    [Fact]
    public void CancelById_Keeps_Entry_Tracked_During_Cancellation()
    {
        // Verifies cancel-before-remove: IsParentRunning should be true
        // at the moment Cancel fires on the CTS
        var parentId = Guid.NewGuid();
        var tickerId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        var context = MakeContext(tickerId, parentId);

        bool wasTrackedDuringCancel = false;
        cts.Token.Register(() =>
        {
            wasTrackedDuringCancel = TickerCancellationTokenManager.IsParentRunning(parentId);
        });

        TickerCancellationTokenManager.AddTickerCancellationToken(cts, context, isDue: false);

        TickerCancellationTokenManager.RequestTickerCancellationById(tickerId);

        Assert.True(wasTrackedDuringCancel, "the entry should still be in the dictionary when Cancel fires");
    }

    // The bug: a cancellation request also removed and disposed the source while the execution was
    // still running - its next read of the token threw ObjectDisposedException instead of observing
    // a cancellation, and IsParentRunning said the occurrence had stopped while it still ran.
    [Fact]
    public void RequestCancellationById_Leaves_The_Source_To_The_Execution_That_Owns_It()
    {
        var parentId = Guid.NewGuid();
        var tickerId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        TickerCancellationTokenManager.AddTickerCancellationToken(cts, MakeContext(tickerId, parentId), isDue: false);

        TickerCancellationTokenManager.RequestTickerCancellationById(tickerId);

        Assert.True(cts.Token.IsCancellationRequested);
        Assert.True(TickerCancellationTokenManager.IsParentRunning(parentId));

        Assert.True(TickerCancellationTokenManager.RemoveTickerCancellationToken(tickerId, cts));
        Assert.False(TickerCancellationTokenManager.IsParentRunning(parentId));
    }

    // The bug: an execution that ended removed whatever was registered under its id, including the
    // registration of a newer execution of the same ticker, whose source it then disposed.
    [Fact]
    public void An_Ending_Execution_Does_Not_Remove_A_Newer_Registration_Of_The_Same_Id()
    {
        var tickerId = Guid.NewGuid();
        var older = new CancellationTokenSource();
        var newer = new CancellationTokenSource();
        TickerCancellationTokenManager.AddTickerCancellationToken(newer, MakeContext(tickerId), isDue: false);

        var removed = TickerCancellationTokenManager.RemoveTickerCancellationToken(tickerId, older);

        Assert.False(removed);
        Assert.True(TickerCancellationTokenManager.RequestTickerCancellationById(tickerId));
        Assert.True(newer.Token.IsCancellationRequested);
    }

    [Fact]
    public void A_Second_Registration_Of_An_Id_In_Flight_Is_Refused_And_Indexes_Nothing()
    {
        var tickerId = Guid.NewGuid();
        var firstParent = Guid.NewGuid();
        var secondParent = Guid.NewGuid();
        TickerCancellationTokenManager.AddTickerCancellationToken(new CancellationTokenSource(), MakeContext(tickerId, firstParent), isDue: false);

        var added = TickerCancellationTokenManager.AddTickerCancellationToken(
            new CancellationTokenSource(), MakeContext(tickerId, secondParent), isDue: false);

        Assert.False(added);
        Assert.False(TickerCancellationTokenManager.IsParentRunning(secondParent));
    }

    private static InternalFunctionContext MakeContext(Guid tickerId, Guid? parentId = null)
    {
        return new InternalFunctionContext
        {
            TickerId = tickerId,
            ParentId = parentId,
            FunctionName = "Test",
            Type = TickerType.CronTickerOccurrence
        };
    }
}
