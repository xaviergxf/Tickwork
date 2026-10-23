# Tickwork.EntityFrameworkCore

Entity Framework Core persistence for [Tickwork](https://github.com/xaviergxf/Tickwork): time jobs,
cron jobs, their occurrences and their history in your own database - PostgreSQL, SQL Server,
SQLite or MySQL.

```bash
dotnet add package Tickwork.EntityFrameworkCore
```

```csharp
builder.Services.AddTickwork(options =>
    options.AddOperationalStore(ef =>
        ef.UseTickworkDbContext<TickworkDbContext>(db => db.UseNpgsql(connectionString))));
```

Or keep the tables in your own `DbContext` with
`ef.UseApplicationDbContext<AppDbContext>(ConfigurationType.UseModelCustomizer)`. The tables are in
the `ticker` schema unless `SetSchema` names another.

The schema is the one TickerQ 10.4.0 created: a TickerQ database needs no migration.

Tickwork is forked from TickerQ 10.4.0 by Arcenox LLC and is not affiliated with it. MIT License.
