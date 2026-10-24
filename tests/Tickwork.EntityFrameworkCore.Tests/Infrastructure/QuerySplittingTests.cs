using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Tickwork.Utilities;
using Tickwork.Utilities.Entities;
using Tickwork.Utilities.Enums;
using Tickwork.Utilities.Interfaces;

namespace Tickwork.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// Every provider query that loads more than one collection says how it is split. Without it EF Core
/// logs warning 20504 (MultipleCollectionIncludeWarning) in the consumer's application; here the
/// warning is turned into an exception, so a query that does not state it fails.
/// </summary>
public class QuerySplittingTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<TestTickworkDbContext> _options = null!;
    private TestableProvider _provider = null!;
    private ITickerClock _clock = null!;
    private readonly DateTime _now = new(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<TestTickworkDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Throw(RelationalEventId.MultipleCollectionIncludeWarning))
            .Options;

        await using (var context = new TestTickworkDbContext(_options))
        {
            await context.Database.EnsureCreatedAsync();
            var parent = NewTicker(null);
            var child = NewTicker(parent.Id);
            var grandchild = NewTicker(child.Id);
            context.Set<TimeTickerEntity>().AddRange(parent, child, grandchild);
            await context.SaveChangesAsync();
        }

        _clock = Substitute.For<ITickerClock>();
        _clock.UtcNow.Returns(_now);
        var redisContext = Substitute.For<ITickworkRedisContext>();
        redisContext.HasRedisConnection.Returns(false);

        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<TestTickworkDbContext>>(
            new PooledDbContextFactory<TestTickworkDbContext>(_options));

        _provider = new TestableProvider(services.BuildServiceProvider(), _clock,
            new SchedulerOptionsBuilder { NodeIdentifier = "node" }, redisContext);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private TimeTickerEntity NewTicker(Guid? parentId) => new()
    {
        Id = Guid.NewGuid(),
        Function = "F",
        ParentId = parentId,
        ExecutionTime = parentId == null ? _now : null,
        Status = TickerStatus.Idle,
        CreatedAt = _now.AddHours(-1),
        UpdatedAt = _now.AddHours(-1),
        Request = []
    };

    [Fact]
    public async Task GetTimeTickers_States_Its_Query_Splitting()
    {
        var tickers = await _provider.GetTimeTickers(null!, CancellationToken.None);

        Assert.Single(tickers);
    }

    [Fact]
    public async Task GetTimeTickersPaginated_States_Its_Query_Splitting()
    {
        var page = await _provider.GetTimeTickersPaginated(null!, 1, 10, CancellationToken.None);

        Assert.Single(page.Items);
    }

    [Fact]
    public async Task RemoveTimeTickers_States_Its_Query_Splitting()
    {
        Guid parentId;
        await using (var context = new TestTickworkDbContext(_options))
            parentId = await context.Set<TimeTickerEntity>().Where(x => x.ParentId == null).Select(x => x.Id).SingleAsync();

        var removed = await _provider.RemoveTimeTickers([parentId], CancellationToken.None);

        Assert.True(removed > 0);
    }

    // The scheduler's own read: children and grandchildren come through a projection.
    [Fact]
    public async Task GetEarliestTimeTickers_States_Its_Query_Splitting()
    {
        var tickers = await _provider.GetEarliestTimeTickers(CancellationToken.None);

        Assert.Single(tickers);
    }

    [Fact]
    public async Task QueueTimedOutTimeTickers_States_Its_Query_Splitting()
    {
        _clock.UtcNow.Returns(_now.AddSeconds(5));

        var tickers = new List<TimeTickerEntity>();
        await foreach (var ticker in _provider.QueueTimedOutTimeTickers(CancellationToken.None))
            tickers.Add(ticker);

        Assert.Single(tickers);
    }

    [Fact]
    public async Task AcquireImmediateTimeTickers_States_Its_Query_Splitting()
    {
        Guid parentId;
        await using (var context = new TestTickworkDbContext(_options))
            parentId = await context.Set<TimeTickerEntity>().Where(x => x.ParentId == null).Select(x => x.Id).SingleAsync();

        var tickers = await _provider.AcquireImmediateTimeTickersAsync([parentId], CancellationToken.None);

        Assert.Single(tickers);
    }

    [Fact]
    public async Task TimeTickersQuery_With_Grandchildren_States_Its_Query_Splitting()
    {
        var tickers = await _provider.TimeTickersQuery()
            .Where(x => x.ParentId == null)
            .WithRelated(TickerRelation.ChildrenDeep)
            .ToArrayAsync(CancellationToken.None);

        Assert.Single(tickers);
    }
}
