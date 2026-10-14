using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using schoolmusic_backend.Hubs;
using schoolmusic_backend.Models;
using schoolmusic_backend.Services;
using StackExchange.Redis;

namespace schoolmusic_backend.Tests;

public class QueueServiceTests
{
    private schoolmusicContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<schoolmusicContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new schoolmusicContext(options);
    }

    private (IQueueService service, schoolmusicContext context) CreateService(string dbName)
    {
        var context = CreateInMemoryContext(dbName);
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockHub = new Mock<IHubContext<QueueHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

        var mockBreakService = new Mock<IBreakService>();

        var service = new QueueService(context, mockRedis.Object, mockHub.Object, mockBreakService.Object);
        return (service, context);
    }

    [Fact]
    public async Task VoteAsync_WhenUserIsBanned_ReturnsForbidden()
    {
        var (service, context) = CreateService(nameof(VoteAsync_WhenUserIsBanned_ReturnsForbidden));

        context.BannedUsers.Add(new BannedUser
        {
            UserId = 10,
            BannedAt = DateTime.UtcNow,
            Reason = "Spam"
        });
        await context.SaveChangesAsync();

        var dto = new VoteRequestDto(QueueItemId: 1);
        var result = await service.VoteAsync(dto, currentUserId: 10);

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("zablokowane", result.Message);
    }

    [Fact]
    public async Task VoteAsync_WhenQueueItemNotFound_ReturnsNotFound()
    {
        var (service, context) = CreateService(nameof(VoteAsync_WhenQueueItemNotFound_ReturnsNotFound));

        var dto = new VoteRequestDto(QueueItemId: 999);
        var result = await service.VoteAsync(dto, currentUserId: 1);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task VoteAsync_WhenAlreadyVoted_ReturnsConflict()
    {
        var (service, context) = CreateService(nameof(VoteAsync_WhenAlreadyVoted_ReturnsConflict));

        var brk = new Break
        {
            Id = 1,
            BreakDate = DateOnly.FromDateTime(DateTime.Now),
            BreakNumber = 1,
            StartAt = DateTime.Now.AddHours(1),
            EndsAt = DateTime.Now.AddHours(2),
            VoteEnds = DateTime.Now.AddHours(1),
            MusicEnabled = true
        };
        var item = new QueueItem
        {
            Id = 100,
            BreakId = 1,
            SongId = 1,
            UserId = 2,
            ModerationStatus = "approved",
            Status = "queued",
            OrderIndex = 1
        };
        var existingVote = new Vote
        {
            Id = 1,
            UserId = 5,
            QueueItemId = 100,
            VotedAt = DateTime.UtcNow
        };

        context.Breaks.Add(brk);
        context.QueueItems.Add(item);
        context.Votes.Add(existingVote);
        await context.SaveChangesAsync();

        var dto = new VoteRequestDto(QueueItemId: 100);
        var result = await service.VoteAsync(dto, currentUserId: 5);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("dwukrotnie", result.Message);
    }

    [Fact]
    public async Task VoteAsync_WhenValidVote_SucceedsAndRecordsVote()
    {
        var (service, context) = CreateService(nameof(VoteAsync_WhenValidVote_SucceedsAndRecordsVote));

        var artist = new Artist { Id = 1, Name = "Test Artist", Source = "other" };
        var song = new Song { Id = 1, Title = "Test Song", ArtistId = 1, Artist = artist, DurationMs = 180000, Source = "other", CreatedAt = DateTime.UtcNow };
        var user = new User { Id = 2, UserLogin = "testuser", UserPassword = "pwd", RankId = 1, CreatedAt = DateTime.UtcNow };
        var brk = new Break
        {
            Id = 2,
            BreakDate = DateOnly.FromDateTime(DateTime.Now),
            BreakNumber = 2,
            StartAt = DateTime.Now.AddHours(1),
            EndsAt = DateTime.Now.AddHours(2),
            VoteEnds = DateTime.Now.AddHours(1),
            MusicEnabled = true
        };
        var item = new QueueItem
        {
            Id = 200,
            BreakId = 2,
            SongId = 1,
            Song = song,
            UserId = 2,
            User = user,
            ModerationStatus = "approved",
            Status = "queued",
            OrderIndex = 1
        };

        context.Artists.Add(artist);
        context.Songs.Add(song);
        context.Users.Add(user);
        context.Breaks.Add(brk);
        context.QueueItems.Add(item);
        await context.SaveChangesAsync();

        var dto = new VoteRequestDto(QueueItemId: 200);
        var result = await service.VoteAsync(dto, currentUserId: 5);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        var recordedVote = await context.Votes.FirstOrDefaultAsync(v => v.UserId == 5 && v.QueueItemId == 200);
        Assert.NotNull(recordedVote);
    }
}
