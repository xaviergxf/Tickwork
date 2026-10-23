using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Tickwork.EntityFrameworkCore.Infrastructure;
using Tickwork.Utilities.Entities;
using Tickwork.Utilities.Enums;

namespace Tickwork.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// What a cron occurrence insert must do on every provider, as FlexLabs
/// <c>Upsert(occurrence).On(x => new { x.ExecutionTime, x.CronTickerId }).NoUpdate()</c> did before
/// it was removed: insert when the (CronTickerId, ExecutionTime) key is free, report 0 and change
/// nothing when it is taken - including when another node takes it at the same moment - and still
/// fail on any other constraint.
/// </summary>
public abstract class InsertIfAbsentContract
{
    private static readonly string[] Key =
    [
        nameof(CronTickerOccurrenceEntity<CronTickerEntity>.CronTickerId),
        nameof(CronTickerOccurrenceEntity<CronTickerEntity>.ExecutionTime)
    ];

    private static readonly DateTime At = new(2026, 3, 1, 10, 30, 0, DateTimeKind.Utc);

    protected abstract TestTickworkDbContext NewContext();

    protected virtual Task<int> InsertAsync(TestTickworkDbContext context, CronTickerOccurrenceEntity<CronTickerEntity> occurrence)
        => context.InsertIfAbsentAsync(occurrence, Key);

    [Fact]
    public async Task Inserts_the_occurrence_with_every_value_when_its_key_is_free()
    {
        var cronTickerId = await SeedCronTickerAsync();
        var occurrence = Occurrence(cronTickerId, At, "node-a");
        occurrence.ExceptionMessage = "kept";
        occurrence.ElapsedTime = 42;
        occurrence.RetryCount = 3;

        int inserted;
        await using (var context = NewContext())
            inserted = await InsertAsync(context, occurrence);

        Assert.Equal(1, inserted);
        await using var read = NewContext();
        var row = await read.Set<CronTickerOccurrenceEntity<CronTickerEntity>>().AsNoTracking().SingleAsync(x => x.Id == occurrence.Id);
        Assert.Equal(cronTickerId, row.CronTickerId);
        Assert.Equal(At, row.ExecutionTime);
        Assert.Equal(TickerStatus.Queued, row.Status);
        Assert.Equal("node-a", row.LockHolder);
        Assert.Equal(At, row.LockedAt);
        Assert.Null(row.ExecutedAt);
        Assert.Equal("kept", row.ExceptionMessage);
        Assert.Null(row.SkippedReason);
        Assert.Equal(42, row.ElapsedTime);
        Assert.Equal(3, row.RetryCount);
        Assert.Equal(At, row.CreatedAt);
        Assert.Equal(At.AddSeconds(1), row.UpdatedAt);
    }

    [Fact]
    public async Task Returns_zero_and_leaves_the_existing_row_alone_when_the_key_is_taken()
    {
        var cronTickerId = await SeedCronTickerAsync();
        var existing = Occurrence(cronTickerId, At, "node-a");
        await using (var context = NewContext())
        {
            context.Add(existing);
            await context.SaveChangesAsync();
        }

        int inserted;
        await using (var context = NewContext())
            inserted = await InsertAsync(context, Occurrence(cronTickerId, At, "node-b"));

        Assert.Equal(0, inserted);
        var rows = await OccurrencesOfAsync(cronTickerId);
        var row = Assert.Single(rows);
        Assert.Equal(existing.Id, row.Id);
        Assert.Equal("node-a", row.LockHolder);
    }

    [Fact]
    public async Task Inserts_when_only_part_of_the_key_matches()
    {
        var cronTickerId = await SeedCronTickerAsync();

        await using (var context = NewContext())
        {
            Assert.Equal(1, await InsertAsync(context, Occurrence(cronTickerId, At, "node-a")));
        }

        await using (var context = NewContext())
        {
            Assert.Equal(1, await InsertAsync(context, Occurrence(cronTickerId, At.AddMinutes(1), "node-a")));
        }

        Assert.Equal(2, (await OccurrencesOfAsync(cronTickerId)).Count);
    }

    [Fact]
    public async Task Exactly_one_of_many_concurrent_writers_inserts_the_key()
    {
        var cronTickerId = await SeedCronTickerAsync();
        const int writers = 8;

        var results = await Task.WhenAll(Enumerable.Range(0, writers).Select(i => Task.Run(async () =>
        {
            await using var context = NewContext();
            return await InsertAsync(context, Occurrence(cronTickerId, At, $"node-{i}"));
        })));

        Assert.Equal(1, results.Sum());
        Assert.Single(await OccurrencesOfAsync(cronTickerId));
    }

    [Fact]
    public async Task Still_fails_when_the_occurrence_points_at_no_cron_ticker()
    {
        await using var context = NewContext();

        await Assert.ThrowsAnyAsync<Exception>(() => InsertAsync(context, Occurrence(Guid.NewGuid(), At, "node-a")));
    }

    [Fact]
    public async Task Does_not_track_the_inserted_occurrence()
    {
        var cronTickerId = await SeedCronTickerAsync();
        await using var context = NewContext();

        await InsertAsync(context, Occurrence(cronTickerId, At, "node-a"));

        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static CronTickerOccurrenceEntity<CronTickerEntity> Occurrence(Guid cronTickerId, DateTime executionTime, string lockHolder) => new()
    {
        Id = Guid.NewGuid(),
        CronTickerId = cronTickerId,
        ExecutionTime = executionTime,
        Status = TickerStatus.Queued,
        LockHolder = lockHolder,
        LockedAt = executionTime,
        CreatedAt = executionTime,
        UpdatedAt = executionTime.AddSeconds(1)
    };

    private async Task<Guid> SeedCronTickerAsync()
    {
        await using var context = NewContext();
        var cronTicker = new CronTickerEntity { Function = "Job", Expression = "* * * * *" };
        context.Add(cronTicker);
        await context.SaveChangesAsync();
        return cronTicker.Id;
    }

    private async Task<List<CronTickerOccurrenceEntity<CronTickerEntity>>> OccurrencesOfAsync(Guid cronTickerId)
    {
        await using var context = NewContext();
        return await context.Set<CronTickerOccurrenceEntity<CronTickerEntity>>()
            .AsNoTracking()
            .Where(x => x.CronTickerId == cronTickerId)
            .ToListAsync();
    }
}

/// <summary>A SQLite database in a file, so concurrent writers each have their own connection.</summary>
public sealed class SqliteFileDatabase : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"tickwork-{Guid.NewGuid():N}.db");

    public string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();

    public TestTickworkDbContext NewContext(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<TestTickworkDbContext>()
            .UseSqlite(ConnectionString)
            .AddInterceptors(interceptors)
            .Options);

    public async Task InitializeAsync()
    {
        await using var context = NewContext();
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        File.Delete(_path);
        return Task.CompletedTask;
    }
}

public sealed class SqliteInsertIfAbsentTests : InsertIfAbsentContract, IClassFixture<SqliteFileDatabase>
{
    private readonly SqliteFileDatabase _database;

    public SqliteInsertIfAbsentTests(SqliteFileDatabase database) => _database = database;

    protected override TestTickworkDbContext NewContext() => _database.NewContext();
}

/// <summary>
/// The form used for a provider with no known single statement, held to the same contract on
/// SQLite, plus the race it exists for: another writer inserting the key between its check and its
/// insert.
/// </summary>
public sealed class PortableInsertIfAbsentTests : InsertIfAbsentContract, IClassFixture<SqliteFileDatabase>
{
    private readonly SqliteFileDatabase _database;

    public PortableInsertIfAbsentTests(SqliteFileDatabase database) => _database = database;

    protected override TestTickworkDbContext NewContext() => _database.NewContext();

    protected override Task<int> InsertAsync(TestTickworkDbContext context, CronTickerOccurrenceEntity<CronTickerEntity> occurrence)
        => InsertIfAbsentExtensions.InsertIfAbsentPortableAsync(context, occurrence,
            [nameof(occurrence.CronTickerId), nameof(occurrence.ExecutionTime)], CancellationToken.None);

    [Fact]
    public async Task Returns_zero_when_another_writer_takes_the_key_between_the_check_and_the_insert()
    {
        var at = new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc);
        Guid cronTickerId;
        await using (var seed = NewContext())
        {
            var cronTicker = new CronTickerEntity { Function = "Job", Expression = "* * * * *" };
            seed.Add(cronTicker);
            await seed.SaveChangesAsync();
            cronTickerId = cronTicker.Id;
        }

        var rival = new CronTickerOccurrenceEntity<CronTickerEntity>
        {
            Id = Guid.NewGuid(), CronTickerId = cronTickerId, ExecutionTime = at, Status = TickerStatus.Queued,
            LockHolder = "rival", CreatedAt = at, UpdatedAt = at
        };
        var mine = new CronTickerOccurrenceEntity<CronTickerEntity>
        {
            Id = Guid.NewGuid(), CronTickerId = cronTickerId, ExecutionTime = at, Status = TickerStatus.Queued,
            LockHolder = "mine", CreatedAt = at, UpdatedAt = at
        };

        int inserted;
        await using (var context = _database.NewContext(new InsertBeforeSaving(_database, rival)))
            inserted = await InsertAsync(context, mine);

        Assert.Equal(0, inserted);
        await using var read = NewContext();
        var row = await read.Set<CronTickerOccurrenceEntity<CronTickerEntity>>().AsNoTracking().SingleAsync(x => x.CronTickerId == cronTickerId);
        Assert.Equal("rival", row.LockHolder);
    }

    /// <summary>Inserts the rival row through its own context the moment the writer starts saving.</summary>
    private sealed class InsertBeforeSaving(SqliteFileDatabase database, CronTickerOccurrenceEntity<CronTickerEntity> rival) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await using var other = database.NewContext();
            other.Add(rival);
            await other.SaveChangesAsync(cancellationToken);
            return result;
        }
    }
}

public sealed class PostgreSqlDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public TestTickworkDbContext NewContext() => new(
        new DbContextOptionsBuilder<TestTickworkDbContext>().UseNpgsql(_container.GetConnectionString()).Options);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = NewContext();
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[Trait("Category", "Database")]
public sealed class PostgreSqlInsertIfAbsentTests : InsertIfAbsentContract, IClassFixture<PostgreSqlDatabase>
{
    private readonly PostgreSqlDatabase _database;

    public PostgreSqlInsertIfAbsentTests(PostgreSqlDatabase database) => _database = database;

    protected override TestTickworkDbContext NewContext() => _database.NewContext();
}

public sealed class SqlServerDatabase : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public TestTickworkDbContext NewContext() => new(
        new DbContextOptionsBuilder<TestTickworkDbContext>().UseSqlServer(_container.GetConnectionString()).Options);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = NewContext();
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[Trait("Category", "Database")]
public sealed class SqlServerInsertIfAbsentTests : InsertIfAbsentContract, IClassFixture<SqlServerDatabase>
{
    private readonly SqlServerDatabase _database;

    public SqlServerInsertIfAbsentTests(SqlServerDatabase database) => _database = database;

    protected override TestTickworkDbContext NewContext() => _database.NewContext();
}

/// <summary>The statement each dialect sends, including MySQL, which has no server in this suite.</summary>
public class InsertIfAbsentSqlTests
{
    private static readonly (string, string)[] Values = [("[Id]", "@p0"), ("[CronTickerId]", "@p1"), ("[ExecutionTime]", "@p2")];
    private static readonly (string, string)[] Key = [("[CronTickerId]", "@p1"), ("[ExecutionTime]", "@p2")];

    [Theory]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL", "PostgreSql")]
    [InlineData("Microsoft.EntityFrameworkCore.Sqlite", "Sqlite")]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer", "SqlServer")]
    [InlineData("Pomelo.EntityFrameworkCore.MySql", "MySql")]
    [InlineData("MySql.EntityFrameworkCore", "MySql")]
    [InlineData("Oracle.EntityFrameworkCore", "Portable")]
    [InlineData("Microsoft.EntityFrameworkCore.InMemory", "Portable")]
    public void Each_provider_gets_its_dialect(string providerName, string expected)
        => Assert.Equal(expected, InsertIfAbsentExtensions.DialectOf(providerName).ToString());

    [Fact]
    public void PostgreSql_does_nothing_on_a_conflict_on_the_key()
        => Assert.Equal(
            "INSERT INTO T ([Id], [CronTickerId], [ExecutionTime]) VALUES (@p0, @p1, @p2) ON CONFLICT ([CronTickerId], [ExecutionTime]) DO NOTHING",
            InsertIfAbsentExtensions.BuildSql(InsertIfAbsentDialect.PostgreSql, "T", Values, Key));

    [Fact]
    public void SqlServer_checks_the_key_under_a_range_lock()
        => Assert.Equal(
            "INSERT INTO T ([Id], [CronTickerId], [ExecutionTime]) SELECT @p0, @p1, @p2 WHERE NOT EXISTS (SELECT 1 FROM T WITH (UPDLOCK, HOLDLOCK) WHERE [CronTickerId] = @p1 AND [ExecutionTime] = @p2)",
            InsertIfAbsentExtensions.BuildSql(InsertIfAbsentDialect.SqlServer, "T", Values, Key));

    [Fact]
    public void MySql_ignores_the_duplicate()
        => Assert.Equal(
            "INSERT IGNORE INTO T ([Id], [CronTickerId], [ExecutionTime]) VALUES (@p0, @p1, @p2)",
            InsertIfAbsentExtensions.BuildSql(InsertIfAbsentDialect.MySql, "T", Values, Key));
}
