namespace Tickwork.Tests;

/// <summary>
/// CronScheduleCache.TimeZoneInfo is process-wide, and AddTickwork sets it too. The classes that set
/// or depend on it run here, alone and after the parallel tests: a class changing the zone while
/// another converts a time made GetNextOccurrenceOrDefault return null now and then.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SchedulerTimeZoneCollection
{
    public const string Name = "Scheduler time zone";
}
