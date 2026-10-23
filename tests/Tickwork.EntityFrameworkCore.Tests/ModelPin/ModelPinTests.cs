using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Tickwork.EntityFrameworkCore.DbContextFactory;
using Tickwork.EntityFrameworkCore.Tests.ModelPin.Snapshots;
using Tickwork.Utilities.Enums;

namespace Tickwork.EntityFrameworkCore.Tests.ModelPin;

/// <summary>
/// The database Tickwork reads and writes is the one TickerQ 10.4.0 (commit c6ed1e7) created, so an
/// application that moves from TickerQ to Tickwork must get an empty migration. These tests hold the
/// model to that: against the snapshots EF wrote for TickerQ 10.4.0, and against the names and enum
/// numbers written out by hand.
/// </summary>
public class ModelPinTests
{
    public static TheoryData<string> Providers => new() { "PostgreSql", "SqlServer", "Sqlite" };

    [Theory]
    [MemberData(nameof(Providers))]
    public void The_model_has_no_difference_from_the_TickerQ_10_4_0_snapshot(string provider)
    {
        using var context = NewContext(provider);
        ModelSnapshot snapshot = provider switch
        {
            "PostgreSql" => new TickerQ1040PostgreSqlSnapshot(),
            "SqlServer" => new TickerQ1040SqlServerSnapshot(),
            _ => new TickerQ1040SqliteSnapshot()
        };

        var snapshotModel = snapshot.Model;
        if (snapshotModel is IMutableModel mutable)
            snapshotModel = mutable.FinalizeModel();
        snapshotModel = context.GetService<IModelRuntimeInitializer>().Initialize(snapshotModel);

        var differences = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshotModel.GetRelationalModel(),
            context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.True(differences.Count == 0,
            "A migration from TickerQ 10.4.0 would not be empty: " + string.Join(", ", differences.Select(d => d.GetType().Name)));
    }

    [Fact]
    public void Tables_columns_keys_and_indexes_keep_their_TickerQ_10_4_0_names()
    {
        using var context = NewContext("PostgreSql");
        var model = context.GetService<IDesignTimeModel>().Model.GetRelationalModel();

        var tables = model.Tables.ToDictionary(t => $"{t.Schema}.{t.Name}");
        Assert.Equal(["ticker.CronTickerOccurrences", "ticker.CronTickers", "ticker.TimeTickers"], tables.Keys.Order());

        AssertTable(tables["ticker.CronTickers"],
            columns: ["CreatedAt", "Description", "Expression", "Function", "Id", "InitIdentifier", "IsEnabled", "IsSystemPaused",
                "Request", "Retries", "RetryIntervals", "UpdatedAt"],
            primaryKey: "PK_CronTickers",
            indexes: ["IX_CronTickers_Expression(Expression)", "IX_Function_Expression(Function,Expression)"],
            foreignKeys: []);

        AssertTable(tables["ticker.CronTickerOccurrences"],
            columns: ["CreatedAt", "CronTickerId", "ElapsedTime", "ExceptionMessage", "ExecutedAt", "ExecutionTime", "Id", "LockHolder",
                "LockedAt", "RetryCount", "SkippedReason", "Status", "UpdatedAt"],
            primaryKey: "PK_CronTickerOccurrences",
            indexes: ["IX_CronTickerOccurrence_CronTickerId(CronTickerId)", "IX_CronTickerOccurrence_ExecutionTime(ExecutionTime)",
                "IX_CronTickerOccurrence_Status_ExecutionTime(Status,ExecutionTime)", "UQ_CronTickerId_ExecutionTime(CronTickerId,ExecutionTime) unique"],
            foreignKeys: ["FK_CronTickerOccurrences_CronTickers_CronTickerId(CronTickerId) -> CronTickers Cascade"]);

        AssertTable(tables["ticker.TimeTickers"],
            columns: ["CreatedAt", "Description", "ElapsedTime", "ExceptionMessage", "ExecutedAt", "ExecutionTime", "Function", "Id",
                "InitIdentifier", "LockHolder", "LockedAt", "ParentId", "Request", "Retries", "RetryCount", "RetryIntervals",
                "RunCondition", "SkippedReason", "Status", "UpdatedAt"],
            primaryKey: "PK_TimeTickers",
            indexes: ["IX_TimeTicker_ExecutionTime(ExecutionTime)", "IX_TimeTicker_Status_ExecutionTime(Status,ExecutionTime)",
                "IX_TimeTickers_ParentId(ParentId)"],
            foreignKeys: ["FK_TimeTickers_TimeTickers_ParentId(ParentId) -> TimeTickers NoAction"]);
    }

    [Fact]
    public void The_default_schema_is_still_ticker()
        => Assert.Equal("ticker", Constants.DefaultSchema);

    [Fact]
    public void Ticker_statuses_keep_the_numbers_stored_by_TickerQ_10_4_0()
        => Assert.Equal(
            [("Idle", 0), ("Queued", 1), ("InProgress", 2), ("Done", 3), ("DueDone", 4), ("Failed", 5), ("Cancelled", 6), ("Skipped", 7)],
            Enum.GetValues<TickerStatus>().Select(v => (v.ToString(), (int)v)));

    [Fact]
    public void Run_conditions_keep_the_numbers_stored_by_TickerQ_10_4_0()
        => Assert.Equal(
            [("OnSuccess", 0), ("OnFailure", 1), ("OnCancelled", 2), ("OnFailureOrCancelled", 3), ("OnAnyCompletedStatus", 4), ("InProgress", 5)],
            Enum.GetValues<RunCondition>().Select(v => (v.ToString(), (int)v)));

    private static void AssertTable(ITable table, string[] columns, string primaryKey, string[] indexes, string[] foreignKeys)
    {
        Assert.Equal(columns, table.Columns.Select(c => c.Name).Order(StringComparer.Ordinal));
        Assert.Equal(primaryKey, table.PrimaryKey?.Name);
        Assert.Equal(indexes, table.Indexes
            .Select(i => $"{i.Name}({string.Join(",", i.Columns.Select(c => c.Name))}){(i.IsUnique ? " unique" : "")}")
            .Order(StringComparer.Ordinal));
        Assert.Equal(foreignKeys, table.ForeignKeyConstraints
            .Select(f => $"{f.Name}({string.Join(",", f.Columns.Select(c => c.Name))}) -> {f.PrincipalTable.Name} {f.OnDeleteAction}")
            .Order(StringComparer.Ordinal));
    }

    private static TickworkDbContext NewContext(string provider)
    {
        var options = new DbContextOptionsBuilder<TickworkDbContext>();
        _ = provider switch
        {
            "PostgreSql" => options.UseNpgsql("Host=localhost;Database=none"),
            "SqlServer" => options.UseSqlServer("Server=localhost;Database=none"),
            _ => options.UseSqlite("Data Source=:memory:")
        };
        return new TickworkDbContext(options.Options);
    }
}
