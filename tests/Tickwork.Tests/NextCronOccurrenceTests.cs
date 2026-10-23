using Tickwork.Utilities.Models;

namespace Tickwork.Tests;

public class NextCronOccurrenceTests
{
    // The constructor used to drop createdAt, so a re-queued occurrence came back created at DateTime.MinValue.
    [Fact]
    public void Keeps_the_creation_time_it_is_given()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        var occurrence = new NextCronOccurrence(id, createdAt);

        Assert.Equal(id, occurrence.Id);
        Assert.Equal(createdAt, occurrence.CreatedAt);
    }
}
