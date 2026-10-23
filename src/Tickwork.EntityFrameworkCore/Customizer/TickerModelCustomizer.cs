using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Tickwork.EntityFrameworkCore.Configurations;
using Tickwork.Utilities.Entities;

namespace Tickwork.EntityFrameworkCore.Customizer
{
    internal class TickerModelCustomizer<TTimeTicker, TCronTicker> : RelationalModelCustomizer
        where TTimeTicker : TimeTickerEntity<TTimeTicker>, new()
        where TCronTicker : CronTickerEntity, new()
    {
        public TickerModelCustomizer(ModelCustomizerDependencies dependencies)
            : base(dependencies)
        {
        }

        public override void Customize(ModelBuilder builder, DbContext context)
        {
            string schema;
            try
            {
                schema = context.GetService<TickworkEfCoreOptionBuilder<TTimeTicker, TCronTicker>>()?.Schema ?? Constants.DefaultSchema;
            }
            catch (InvalidOperationException)
            {
                schema = Constants.DefaultSchema;
            }

            builder.ApplyConfiguration(new TimeTickerConfigurations<TTimeTicker>(schema));
            builder.ApplyConfiguration(new CronTickerConfigurations<TCronTicker>(schema));
            builder.ApplyConfiguration(new CronTickerOccurrenceConfigurations<TCronTicker>(schema));

            base.Customize(builder, context);
        }
    }
}