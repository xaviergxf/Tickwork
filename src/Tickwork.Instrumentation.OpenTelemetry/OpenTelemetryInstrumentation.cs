using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tickwork.Utilities;
using Tickwork.Utilities.Enums;
using Tickwork.Utilities.Instrumentation;
using Tickwork.Utilities.Models;

namespace Tickwork.Instrumentation.OpenTelemetry;

internal class OpenTelemetryInstrumentation : TickworkBaseLoggerInstrumentation , ITickworkInstrumentation
{
    private readonly ILogger<OpenTelemetryInstrumentation> _logger;
    private static readonly ActivitySource ActivitySource = new("Tickwork", "1.0.0");

    public OpenTelemetryInstrumentation(ILogger<OpenTelemetryInstrumentation> logger, SchedulerOptionsBuilder optionsBuilder) : base(logger, optionsBuilder.NodeIdentifier)
    {
        _logger = logger;
    }

    public override Activity? StartJobActivity(string activityName, InternalFunctionContext context)
    {
        var activity = ActivitySource.StartActivity(activityName);
            
        if (activity != null)
        {
            activity.SetTag("tickwork.job.id", context.TickerId.ToString());
            activity.SetTag("tickwork.job.type", context.Type.ToString());
            activity.SetTag("tickwork.job.function", context.FunctionName);
            activity.SetTag("tickwork.job.priority", context.CachedPriority.ToString());
            activity.SetTag("tickwork.job.machine", InstanceIdentifier);
            activity.SetTag("tickwork.job.retries", context.Retries);
                
            if (context.ParentId.HasValue)
            {
                activity.SetTag("tickwork.job.parent_id", context.ParentId.Value.ToString());
            }

            if (context.Type == TickerType.TimeTicker && context.ParentId.HasValue)
            {
                activity.SetTag("tickwork.job.run_condition", context.RunCondition.ToString());
            }
        }
            
        return activity;
    }

    public override void LogJobEnqueued(string jobType, string functionName, Guid jobId, string? enqueuedFrom = null)
    {
        using var activity = ActivitySource.StartActivity("tickwork.job.enqueued");
        activity?.SetTag("tickwork.job.id", jobId.ToString());
        activity?.SetTag("tickwork.job.type", jobType);
        activity?.SetTag("tickwork.job.function", functionName);
            
        // Get detailed caller information for OpenTelemetry
        var callerInfo = string.IsNullOrEmpty(enqueuedFrom) ? CallerInfoHelper.GetCallerInfo(6) : enqueuedFrom;
        activity?.SetTag("tickwork.job.enqueued_from", callerInfo);
            
        _logger.LogInformation("Tickwork Job enqueued: {JobType} - {Function} ({JobId}) from {EnqueuedFrom}", 
            jobType, functionName, jobId, callerInfo);
    }

    public override void LogJobCompleted(Guid jobId, string functionName, long executionTimeMs, bool success)
    {
        using var activity = ActivitySource.StartActivity("tickwork.job.completed");
        activity?.SetTag("tickwork.job.id", jobId.ToString());
        activity?.SetTag("tickwork.job.function", functionName);
        activity?.SetTag("tickwork.job.execution_time_ms", executionTimeMs);
        activity?.SetTag("tickwork.job.success", success);
            
        // Set activity status based on success
        if (activity != null)
        {
            activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }
            
        base.LogJobCompleted(jobId, functionName, executionTimeMs, success);
    }

    public override void LogJobFailed(Guid jobId, string functionName, Exception exception, int retryCount)
    {
        using var activity = ActivitySource.StartActivity("tickwork.job.failed");
        activity?.SetTag("tickwork.job.id", jobId.ToString());
        activity?.SetTag("tickwork.job.function", functionName);
        activity?.SetTag("tickwork.job.retry_count", retryCount);
        activity?.SetTag("tickwork.job.error_type", exception.GetType().Name);
        activity?.SetTag("tickwork.job.error_message", exception.Message);
            
        if (activity != null)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            // Record exception information in tags instead of RecordException (not available in all .NET versions)
            if (exception.StackTrace != null)
            {
                activity.SetTag("tickwork.job.error_stack_trace", exception.StackTrace);
            }
        }
            
        base.LogJobFailed(jobId, functionName, exception, retryCount);
    }

    public override void LogJobCancelled(Guid jobId, string functionName, string reason)
    {
        using var activity = ActivitySource.StartActivity("tickwork.job.cancelled");
        activity?.SetTag("tickwork.job.id", jobId.ToString());
        activity?.SetTag("tickwork.job.function", functionName);
        activity?.SetTag("tickwork.job.cancellation_reason", reason);
            
        if (activity != null)
        {
            activity.SetStatus(ActivityStatusCode.Error, reason);
        }
            
        base.LogJobCancelled(jobId, functionName, reason);
    }

    public override void LogJobSkipped(Guid jobId, string functionName, string reason)
    {
        using var activity = ActivitySource.StartActivity("tickwork.job.skipped");
        activity?.SetTag("tickwork.job.id", jobId.ToString());
        activity?.SetTag("tickwork.job.function", functionName);
        activity?.SetTag("tickwork.job.skip_reason", reason);
            
        base.LogJobSkipped(jobId, functionName, reason);
    }

    public override void LogSeedingDataStarted(string seedingDataType)
    {
        using var activity = ActivitySource.StartActivity("tickwork.seeding.started");
        activity?.SetTag("tickwork.seeding.type", seedingDataType);
        activity?.SetTag("tickwork.seeding.environment", InstanceIdentifier);
            
        base.LogSeedingDataStarted(seedingDataType);
    }

    public override void LogSeedingDataCompleted(string seedingDataType)
    {
        using var activity = ActivitySource.StartActivity("tickwork.seeding.completed");
        activity?.SetTag("tickwork.seeding.type", seedingDataType);
        activity?.SetTag("tickwork.seeding.environment", InstanceIdentifier);
            
        base.LogSeedingDataCompleted(seedingDataType);
    }

    public override void LogRequestDeserializationFailure(string requestType, string functionName, Guid tickerId, TickerType type, Exception exception)
    {
        using var activity = ActivitySource.StartActivity("tickwork.job_request_serialization.failed");
        activity?.SetTag("tickwork.job.id", tickerId.ToString());
        activity?.SetTag("tickwork.job.function", functionName);
        activity?.SetTag("tickwork.job.cancellation_reason", exception.Message);
        base.LogRequestDeserializationFailure(requestType, functionName, tickerId, type, exception);
    }
}