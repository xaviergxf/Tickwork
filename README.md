# Tickwork

Tickwork is a background job scheduler for .NET. It runs one-off jobs at a given time and recurring
jobs on a cron expression, with retries, priorities, per-function concurrency limits and chained
jobs (a child that runs when its parent succeeds, fails or is cancelled). Jobs are registered at
compile time by a source generator, and are kept in memory or in your own database through
Entity Framework Core.

Tickwork targets **.NET 10** and **.NET 11** and is distributed under the MIT License.

It is a fork of [TickerQ](https://github.com/Arcenox-co/TickerQ) 10.4.0 by Arcenox LLC, taken at
commit `c6ed1e7`, when TickerQ was MIT / Apache-2.0; 1.1 adds reliability fixes adapted from
TickerQ commits up to `b2aefd0`, still under those licences. Tickwork keeps the scheduler, the EF Core
provider, the source generator and the OpenTelemetry instrumentation; it does not carry the
dashboard, the Redis provider or the hub. Tickwork is not affiliated with or endorsed by Arcenox
LLC or the TickerQ project. See [NOTICE](NOTICE).

## Packages

| Package | What it is |
|---|---|
| `Tickwork` | The scheduler, its hosted services and the source generator (shipped inside it as an analyzer) |
| `Tickwork.Utilities` | The entities, managers and contracts the other packages share |
| `Tickwork.EntityFrameworkCore` | Persistence in your database through EF Core: PostgreSQL, SQL Server, SQLite, MySQL |
| `Tickwork.Instrumentation.OpenTelemetry` | OpenTelemetry traces for every job |

All packages are versioned together; keep them on the same version.

## Install

```bash
dotnet add package Tickwork
dotnet add package Tickwork.EntityFrameworkCore   # to keep jobs in your database
```

## A minimal example

```csharp
using Tickwork.DependencyInjection;
using Tickwork.Utilities.Base;
using Tickwork.Utilities.Entities;
using Tickwork.Utilities.Interfaces.Managers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTickwork();          // in memory; see below for a database

var app = builder.Build();
app.UseTickwork();

app.MapPost("/remind", async (ITimeTickerManager<TimeTickerEntity> tickers) =>
    await tickers.AddAsync(new TimeTickerEntity
    {
        Function = "SendReminder",
        ExecutionTime = DateTime.UtcNow.AddMinutes(5)
    }));

app.Run();

public class Jobs
{
    [TickerFunction("SendReminder")]
    public Task SendReminder(TickerFunctionContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Reminder {context.Id}");
        return Task.CompletedTask;
    }

    [TickerFunction("Cleanup", "0 3 * * *")]   // every day at 03:00
    public Task Cleanup(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

`UseTickwork` extends `IHost`, so a worker or console app on the generic host calls it the same way:

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddTickwork();

var host = builder.Build();
host.UseTickwork();
await host.RunAsync();
```

To keep jobs in a database, add the EF Core store. With Tickwork's own context:

```csharp
using Tickwork.EntityFrameworkCore.DbContextFactory;
using Tickwork.EntityFrameworkCore.DependencyInjection;

builder.Services.AddTickwork(options =>
    options.AddOperationalStore(ef =>
        ef.UseTickworkDbContext<TickworkDbContext>(db => db.UseNpgsql(connectionString))));
```

or inside your application's own `DbContext`, with
`ef.UseApplicationDbContext<AppDbContext>(ConfigurationType.UseModelCustomizer)`. The tables live
in the `ticker` schema unless you set another with `SetSchema`. SQLite has no schemas: there EF Core
ignores the schema and logs warning 30000 (`SchemaConfiguredWarning`) for each table, which is
expected and harmless.

## Moving from TickerQ 10.4

Tickwork 1.0 is TickerQ 10.4.0 under another name, so moving is a rename:

1. Replace the packages: `TickerQ` → `Tickwork`, `TickerQ.Utilities` → `Tickwork.Utilities`,
   `TickerQ.EntityFrameworkCore` → `Tickwork.EntityFrameworkCore`,
   `TickerQ.Instrumentation.OpenTelemetry` → `Tickwork.Instrumentation.OpenTelemetry`.
2. Replace `TickerQ` with `Tickwork` in your `using` directives.
3. Rename the few identifiers that carry the brand: `AddTickerQ` → `AddTickwork`, `UseTickerQ` →
   `UseTickwork`, `TickerQDbContext` → `TickworkDbContext`, `UseTickerQDbContext` →
   `UseTickworkDbContext`, `ITickerQHostScheduler` → `ITickworkHostScheduler`,
   `TickerQStartMode` → `TickworkStartMode`, `[TickerQConstructor]` → `[TickworkConstructor]`,
   `TickerQEfCoreOptionBuilder` → `TickworkEfCoreOptionBuilder`. Everything named *Ticker*
   (`TimeTickerEntity`, `CronTickerEntity`, `[TickerFunction]`, `TickerFunctionContext`,
   `TickerStatus`, ...) keeps its name.

**The database does not change.** The schema, tables, columns, indexes and stored enum values are
the ones TickerQ 10.4.0 created, so `dotnet ef migrations add` after the upgrade produces an empty
migration (the model snapshot is rewritten with the new type names, which is expected). The tests
hold the model to TickerQ 10.4.0's snapshots on PostgreSQL, SQL Server and SQLite.

What changes for an operator: OpenTelemetry spans now come from the `Tickwork` activity source and
their tags start with `tickwork.` instead of `tickerq.`, and log messages say Tickwork.

## Building and testing

```bash
dotnet build Tickwork.slnx
dotnet test Tickwork.slnx
```

The SDK is pinned by `global.json` (a .NET 11 SDK builds both targets). The tests run on both
targets. The tests in the `Category=Database` trait start PostgreSQL and SQL Server in containers
through Testcontainers, so they need Docker or Podman; `--filter "Category!=Database"` leaves them out.

## Releasing

Packages go to nuget.org through **Trusted Publishing**: there is no stored API key. Publishing a
GitHub release runs `.github/workflows/release.yml`, which builds, tests and packs both targets,
exchanges the workflow's OIDC token for a one-hour nuget.org key, and pushes the packages and their
symbols.

It needs, once:

- a trusted publishing policy on nuget.org for this repository and the workflow file `release.yml`;
- a GitHub environment named `release`, which the publishing job runs in;
- a repository secret `NUGET_USER`: the nuget.org profile name (not the e-mail address) that owns
  the policy.

The version is `<Version>` in `Directory.Build.props`; tag the release `v<version>`.

## License

MIT. Copyright (c) 2025-present Arcenox LLC, copyright (c) 2026 Xavier Klausener. See
[LICENSE](LICENSE) and [NOTICE](NOTICE).
