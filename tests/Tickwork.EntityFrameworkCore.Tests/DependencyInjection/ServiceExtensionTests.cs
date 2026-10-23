using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tickwork.DependencyInjection;
using Tickwork.EntityFrameworkCore.Customizer;
using Tickwork.EntityFrameworkCore.DependencyInjection;
using Tickwork.Utilities.Entities;

namespace Tickwork.EntityFrameworkCore.Tests.DependencyInjection;

// Test DbContext that will be used by Tickwork
public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}

public class ServiceExtensionTests
{
    /// <summary>
    /// Uses WebApplicationFactory to create fake application with fake DbContext TestDbContext
    /// and adds Tickwork with AddOperationalStore and UseApplicationDbContext to the application.
    /// It needs to verify that DbContext is successfully used and Tickwork sets are accessible from it.
    /// </summary>
    [Fact]
    public void AddOperationalStore_UseApplicationDbContext_UsesDbContext()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddDbContext<TestDbContext>(options =>
        {
            options.UseSqlite("Data Source=ServiceExtensionTest;Mode=Memory;Cache=Shared");
        });

        builder.Services.AddTickwork(tickerOptions =>
        {
            tickerOptions.AddOperationalStore(efOptions =>
            {
                efOptions.UseApplicationDbContext<TestDbContext>(ConfigurationType.UseModelCustomizer);
            });
        });

        var app = builder.Build();

        // Act
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        // Assert
        var model = dbContext.Model;

        var timeTickerEntityType = model.FindEntityType(typeof(TimeTickerEntity));
        var cronTickerEntityType = model.FindEntityType(typeof(CronTickerEntity));
        var cronTickerOccurrenceEntityType = model.FindEntityType(typeof(CronTickerOccurrenceEntity<CronTickerEntity>));

        Assert.NotNull(timeTickerEntityType);
        Assert.NotNull(cronTickerEntityType);
        Assert.NotNull(cronTickerOccurrenceEntityType);

        // Additional verification - ensure we can query the sets (they should be queryable)
        var timeTickerSet = dbContext.Set<TimeTickerEntity>();
        var cronTickerSet = dbContext.Set<CronTickerEntity>();
        var cronTickerOccurrenceSet = dbContext.Set<CronTickerOccurrenceEntity<CronTickerEntity>>();

        Assert.NotNull(timeTickerSet);
        Assert.NotNull(cronTickerSet);
        Assert.NotNull(cronTickerOccurrenceSet);
    }
}
