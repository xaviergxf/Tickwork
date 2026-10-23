# Tickwork.Instrumentation.OpenTelemetry

OpenTelemetry instrumentation package for Tickwork job scheduler with distributed tracing support.

## Features

- **Distributed Tracing**: Full OpenTelemetry activity/span creation for job execution lifecycle
- **Structured Logging**: Rich logging with job context through ILogger integration
- **Parent-Child Relationships**: Maintains trace relationships between parent and child jobs
- **Retry Tracking**: Tracks retry attempts with detailed context
- **Performance Metrics**: Comprehensive execution time and outcome tracking
- **Error Tracking**: Detailed exception and cancellation tracking
- **Caller Information**: Automatic detection of where jobs are enqueued from

## Installation

```bash
dotnet add package Tickwork.Instrumentation.OpenTelemetry
```

## Usage

### Basic Setup

```csharp
using Tickwork.Instrumentation.OpenTelemetry;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry with Tickwork ActivitySource
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Tickwork") // Add Tickwork ActivitySource
               .AddConsoleExporter()
               .AddJaegerExporter();
    });

// Add Tickwork with OpenTelemetry instrumentation
builder.Services.AddTickwork<MyTimeTicker, MyCronTicker>(options => { })
    .AddOperationalStore(ef => { })
    .AddOpenTelemetryInstrumentation(); // 👈 Enable tracing

var app = builder.Build();
app.Run();
```

### With Jaeger

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddTickworkInstrumentation()
               .AddJaegerExporter(options =>
               {
                   options.Endpoint = new Uri("http://localhost:14268/api/traces");
               });
    });
```

### With Application Insights

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddTickworkInstrumentation()
               .AddAzureMonitorTraceExporter();
    });
```

## Trace Structure

### Job Execution Activities
```
tickwork.job.execute.timeticker (main job execution span)
├── tickwork.job.enqueued (when job starts execution)
├── tickwork.job.completed (on successful completion)
├── tickwork.job.failed (on failure)
├── tickwork.job.cancelled (on cancellation)
├── tickwork.job.skipped (when skipped)
├── tickwork.seeding.started (for data seeding)
└── tickwork.seeding.completed (seeding completion)
```

### Tags Added to Activities

| Tag | Description | Example |
|-----|-------------|---------|
| `tickwork.job.id` | Unique job identifier | `123e4567-e89b-12d3-a456-426614174000` |
| `tickwork.job.type` | Type of ticker | `TimeTicker`, `CronTicker` |
| `tickwork.job.function` | Function name being executed | `ProcessEmails` |
| `tickwork.job.priority` | Job priority | `Normal`, `High`, `LongRunning` |
| `tickwork.job.machine` | Machine executing the job | `web-server-01` |
| `tickwork.job.parent_id` | Parent job ID (for child jobs) | `parent-job-guid` |
| `tickwork.job.enqueued_from` | Where the job was enqueued from | `UserController.CreateUser (Program.cs:42)` |
| `tickwork.job.is_due` | Whether the job was due | `true`, `false` |
| `tickwork.job.is_child` | Whether this is a child job | `true`, `false` |
| `tickwork.job.retries` | Maximum retry attempts | `3` |
| `tickwork.job.current_attempt` | Current retry attempt | `1`, `2`, `3` |
| `tickwork.job.final_status` | Final execution status | `Done`, `Failed`, `Cancelled`, `Skipped` |
| `tickwork.job.final_retry_count` | Final retry count reached | `2` |
| `tickwork.job.execution_time_ms` | Execution time in milliseconds | `1250` |
| `tickwork.job.success` | Whether execution was successful | `true`, `false` |
| `tickwork.job.error_type` | Exception type for failures | `SqlException`, `TimeoutException` |
| `tickwork.job.error_message` | Error message | `Connection timeout` |
| `tickwork.job.error_stack_trace` | Full stack trace | `at MyService.ProcessData()...` |
| `tickwork.job.cancellation_reason` | Reason for cancellation | `Task was cancelled` |
| `tickwork.job.skip_reason` | Reason for skipping | `Another instance is already running` |

## Logging Output

The instrumentation provides structured logging for all job events:

```
[INF] Tickwork Job enqueued: TimeTicker - ProcessEmails (123e4567-e89b-12d3-a456-426614174000) from ExecutionTaskHandler
[INF] Tickwork Job completed: ProcessEmails (123e4567-e89b-12d3-a456-426614174000) in 1250ms - Success: True
[ERR] Tickwork Job failed: ProcessEmails (123e4567-e89b-12d3-a456-426614174000) - Retry 1 - Connection timeout
[INF] Tickwork Job completed: ProcessEmails (123e4567-e89b-12d3-a456-426614174000) in 2500ms - Success: False
[WRN] Tickwork Job cancelled: ProcessEmails (123e4567-e89b-12d3-a456-426614174000) - Task was cancelled
[INF] Tickwork Job skipped: ProcessEmails (123e4567-e89b-12d3-a456-426614174000) - Another CronOccurrence is already running!
[INF] Tickwork start seeding data: TimeTicker (production-node-01)
[INF] Tickwork completed seeding data: TimeTicker (production-node-01)
```

## Integration with Logging Frameworks

This package works seamlessly with any logging framework that integrates with `ILogger`:

### Serilog
```csharp
builder.Host.UseSerilog((context, config) =>
{
    config.WriteTo.Console()
          .WriteTo.File("logs/tickwork-.txt", rollingInterval: RollingInterval.Day)
          .Enrich.FromLogContext();
});
```

### NLog
```csharp
builder.Logging.ClearProviders();
builder.Logging.AddNLog();
```

## Performance Impact

- **Minimal Overhead**: Activities are only created when OpenTelemetry listeners are active
- **Efficient Logging**: Uses structured logging with minimal string allocations
- **Conditional Tracing**: No performance impact when tracing is disabled

## Requirements

- .NET 8.0 or later
- OpenTelemetry 1.7.0 or later
- Tickwork.Utilities (automatically included)
