using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Tickwork.Utilities.Entities;
using Tickwork.Utilities.Interfaces;
using Tickwork.Utilities.Models;

namespace Tickwork.Utilities.Temps
{
    internal class NoOpTickworkNotificationHubSender : ITickworkNotificationHubSender
    {
        public Task AddCronTickerNotifyAsync(object cronTicker)
        {
            return Task.CompletedTask;
        }

        public Task UpdateCronTickerNotifyAsync(object cronTicker)
        {
            return Task.CompletedTask;
        }

        public Task RemoveCronTickerNotifyAsync(Guid id)
        {
            return Task.CompletedTask;
        }

        public Task AddTimeTickerNotifyAsync(Guid id)
        {
            return Task.CompletedTask;
        }
        
        public Task AddTimeTickersBatchNotifyAsync()
        {
            return Task.CompletedTask;
        }

        public Task UpdateTimeTickerNotifyAsync(object timeTicker)
        {
            return Task.CompletedTask;
        }

        public Task RemoveTimeTickerNotifyAsync(Guid id)
        {
            return Task.CompletedTask;
        }

        public void UpdateActiveThreads(string activeThreads)
        {
        }

        public void UpdateNextOccurrence(DateTime? nextOccurrence)
        {
        }

        public void UpdateHostStatus(bool active)
        {
        }

        public void UpdateHostException(string exceptionMessage)
        {
        }

        public Task UpdateNodeHeartBeatAsync(JsonElement nodeHeartBeat)
        {
            return Task.CompletedTask;
        }

        public Task AddCronOccurrenceAsync(Guid groupId, object occurrence)
        { 
            return Task.CompletedTask;
        }

        public Task UpdateCronOccurrenceAsync(Guid groupId, object occurrence)
        {
            return Task.CompletedTask;
        }

        public Task UpdateTimeTickerFromInternalFunctionContext<TTimeTickerEntity>(InternalFunctionContext internalFunctionContext) where TTimeTickerEntity : TimeTickerEntity<TTimeTickerEntity>, new()
        {
            return Task.CompletedTask;
        }

        public Task UpdateCronOccurrenceFromInternalFunctionContext<TCronTickerEntity>(
            InternalFunctionContext internalFunctionContext) where TCronTickerEntity : CronTickerEntity, new()
        {
            return Task.CompletedTask;
        }

        public Task CanceledTickerNotifyAsync(Guid id)
        {
            return Task.CompletedTask;
        }
    }
}
