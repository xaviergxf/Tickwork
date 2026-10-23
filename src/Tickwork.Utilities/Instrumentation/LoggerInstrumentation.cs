using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tickwork.Utilities.Models;

namespace Tickwork.Utilities.Instrumentation
{
    /// <summary>
    /// No-operation implementation of ITickworkInstrumentation
    /// </summary>
    public sealed class LoggerInstrumentation : TickworkBaseLoggerInstrumentation, ITickworkInstrumentation
    {
        public LoggerInstrumentation(ILogger<LoggerInstrumentation> logger, SchedulerOptionsBuilder optionsBuilder) : base(logger,  optionsBuilder.NodeIdentifier)
        {
        }

        public override Activity StartJobActivity(string activityName, InternalFunctionContext context)
            => null;
    }
}