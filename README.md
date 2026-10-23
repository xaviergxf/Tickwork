<h1 align="center">
  <img src="https://tickwork.net/tickwork-logo.svg" alt="Tickwork Logo" width="140" />
  <br />
  Tickwork
</h1>

<p align="center">
  <strong>The modern job scheduler for .NET</strong><br />
  Source-generated task scheduling with built-in persistence, cron & time-based execution, and real-time monitoring.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/tickwork"><img src="https://img.shields.io/nuget/dt/tickwork.svg?style=flat-square" alt="NuGet Downloads" /></a>
  <a href="https://www.nuget.org/packages/tickwork"><img src="https://img.shields.io/nuget/vpre/tickwork.svg?style=flat-square" alt="NuGet Version" /></a>
  <a href="https://github.com/Arcenox-co/Tickwork/actions/workflows/build.yml"><img src="https://img.shields.io/github/actions/workflow/status/Arcenox-co/Tickwork/build.yml?branch=main&style=flat-square" alt="Build Status" /></a>
  <a href="https://tickwork.net"><img src="https://img.shields.io/badge/docs-tickwork.net-blue?style=flat-square" alt="Documentation" /></a>
  <a href="https://discord.gg/ZJemWvp9MK"><img src="https://img.shields.io/badge/discord-Tickwork-5865F2?style=flat-square&logo=discord&logoColor=white" alt="Discord" /></a>
  <a href="https://opencollective.com/tickwork"><img src="https://opencollective.com/tickwork/tiers/badge.svg?style=flat-square" alt="OpenCollective" /></a>
</p>

---

## Why Tickwork?

| | |
|---|---|
| **Zero reflection, AOT ready** | Source generators at compile time. No runtime reflection, no magic strings, fully trimmable. |
| **Your database** | EF Core (PostgreSQL, SQL Server, SQLite, MySQL) or Redis. No separate storage. |
| **Real-time dashboard** | Built-in SignalR dashboard. Monitor, inspect, manage — no paid add-ons. |
| **Multi-node** | Redis heartbeats, dead-node cleanup, lock-based coordination. Just add instances. |
| **Minimal setup** | `AddTickwork()` → decorate a method → schedule. Minutes, not hours. |

## Features

- **Time & cron scheduling** — one-off and recurring jobs
- **Source-generated** — compile-time function registration for maximum performance
- **Dual persistence** — EF Core (PostgreSQL, SQL Server, SQLite, MySQL) or Redis
- **Live dashboard** — real-time UI with SignalR — [screenshots](https://tickwork.net/features/dashboard.html#dashboard-screenshots)
- **Retry & throttling** — configurable retry policies with backoff
- **Dependency injection** — first-class DI support
- **Multi-node** — distributed coordination via Redis heartbeats and dead-node cleanup
- **Hub** — centralized scheduling across applications via [Tickwork Hub](https://hub.tickwork.net)

## Quick Start

```bash
dotnet add package Tickwork
```

### 1. Register services

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTickwork();

var app = builder.Build();
app.UseTickwork();
app.Run();
```

### 2. Create a job

```csharp
using Tickwork.Utilities.Base;

public class MyJobs
{
    [TickerFunction("HelloWorld")]
    public async Task HelloWorld(
        TickerFunctionContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Hello from Tickwork! Job ID: {context.Id}");
    }
}
```

### 3. Schedule it

```csharp
public class MyService(ITimeTickerManager<TimeTickerEntity> manager)
{
    public async Task Schedule()
    {
        await manager.AddAsync(new TimeTickerEntity
        {
            Function = "HelloWorld",
            ExecutionTime = DateTime.UtcNow.AddSeconds(10)
        });
    }
}
```

## Packages

| Package | Description |
|---------|------------|
| [`Tickwork`](https://www.nuget.org/packages/Tickwork) | Core scheduler engine |
| [`Tickwork.Utilities`](https://www.nuget.org/packages/Tickwork.Utilities) | Shared types, entities, and interfaces |
| [`Tickwork.EntityFrameworkCore`](https://www.nuget.org/packages/Tickwork.EntityFrameworkCore) | EF Core persistence provider |
| [`Tickwork.Caching.StackExchangeRedis`](https://www.nuget.org/packages/Tickwork.Caching.StackExchangeRedis) | Redis persistence and distributed coordination |
| [`Tickwork.Dashboard`](https://www.nuget.org/packages/Tickwork.Dashboard) | Real-time dashboard UI |
| [`Tickwork.Instrumentation.OpenTelemetry`](https://www.nuget.org/packages/Tickwork.Instrumentation.OpenTelemetry) | OpenTelemetry tracing |
| [`Tickwork.SourceGenerator`](https://www.nuget.org/packages/Tickwork.SourceGenerator) | Compile-time function registration |

> **Note:** All packages are versioned together. Always update all packages to the same version.

## Tickwork Hub

Centralized scheduling across applications — [hub.tickwork.net](https://hub.tickwork.net)

## Documentation

Full documentation at **[tickwork.net](https://tickwork.net)** — docs are open-source at [Tickwork-UI](https://github.com/Arcenox-co/Tickwork-UI).

## Sponsors & Backers

Support Tickwork through [OpenCollective](https://opencollective.com/tickwork).

<a href="https://opencollective.com/tickwork"><img src="https://opencollective.com/tickwork/backers.svg?width=890" /></a>

## Contributing

PRs, ideas, and issues are welcome! Please read our [Contributing Guide](CONTRIBUTING.md) and sign the [CLA](CLA.md) before submitting a pull request.

## Contributors

Thanks to all our wonderful contributors! See [CONTRIBUTORS.md](CONTRIBUTORS.md) for details.

<a href="https://github.com/Arcenox-co/Tickwork/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=Arcenox-co/Tickwork" />
</a>

## License

Dual licensed under **MIT** and **Apache 2.0** © [Arcenox LLC](https://arcenox.com)
