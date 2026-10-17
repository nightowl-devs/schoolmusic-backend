using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using schoolmusic_backend.Controllers;
using schoolmusic_backend.Hubs;
using schoolmusic_backend.Models;
using StackExchange.Redis;
using Xunit;

namespace schoolmusic_backend.Tests;

public class ModerationListTests
{
    private static schoolmusicContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<schoolmusicContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new schoolmusicContext(options);
    }

    private static (ModerationController controller, schoolmusicContext context) CreateController(string dbName, int userId = 1, string role = "admin")
    {
        var context = CreateInMemoryContext(dbName);
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockHubContext = new Mock<IHubContext<QueueHub>>();

        var controller = new ModerationController(context, mockRedis.Object, mockHubContext.Object);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return (controller, context);
    }

    private static async Task<(User user, Moderator mod, Artist artist, Song song)> SeedBasicDataAsync(schoolmusicContext db, int userId = 1, int songId = 10)
    {
        var user = new User
        {
            Id = userId,
            UserLogin = $"mod_{userId}",
            UserPassword = "hash",
            RankId = 3,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);

        var mod = new Moderator
        {
            Id = userId,
            UserId = userId,
            SongsApproved = 0,
            SongsBanned = 0,
            ArtistsBanned = 0
        };
        db.Moderators.Add(mod);

        var artist = new Artist
        {
            Id = 5,
            Name = "Bad Artist",
            Source = "other"
        };
        db.Artists.Add(artist);

        var song = new Song
        {
            Id = songId,
            Title = "Explicit Track",
            ArtistId = artist.Id,
            Artist = artist,
            DurationMs = 210000,
            Genre = "Rock",
            Source = "other",
            IsActive = true
        };
        db.Songs.Add(song);

        await db.SaveChangesAsync();
        return (user, mod, artist, song);
    }

    [Fact]
    public async Task GetBlacklistedSongs_ReturnsPagedList_AndFiltersBySearch()
    {
        // Arrange
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        var (_, mod, artist, song1) = await SeedBasicDataAsync(db, 1, 101);

        var song2 = new Song
        {
            Id = 102,
            Title = "Calm Melody",
            ArtistId = artist.Id,
            Artist = artist,
            DurationMs = 180000,
            Genre = "Classical",
            Source = "other",
            IsActive = true
        };
        db.Songs.Add(song2);

        db.SongBlacklists.Add(new SongBlacklist { Id = 1, SongId = song1.Id, ArtistId = artist.Id, ModId = mod.Id });
        db.SongBlacklists.Add(new SongBlacklist { Id = 2, SongId = song2.Id, ArtistId = artist.Id, ModId = mod.Id });
        await db.SaveChangesAsync();

        // Act - search filter
        var result = await controller.GetBlacklistedSongs(search: "Explicit", page: 1, pageSize: 10);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<ModerationController.BlacklistPagedResponseDto>(ok.Value);
        Assert.Equal(1, paged.Total);
        var item = Assert.Single(paged.Items);
        Assert.Equal("Explicit Track", item.Title);
    }

    [Fact]
    public async Task AddToBlacklist_WhenSongValid_AddsToBlacklistAndRemovesFromGreenlist()
    {
        // Arrange
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        var (_, mod, artist, song) = await SeedBasicDataAsync(db, 1, 201);

        // Utwór pierwotnie na greenliście
        db.SongGreenlists.Add(new SongGreenlist { Id = 50, SongId = song.Id, ArtistId = artist.Id, ModId = mod.Id });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.AddToBlacklist(new ModerationController.AddSongToBlacklistDto(SongId: song.Id, ArtistId: null));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);

        // Sprawdzenie dodania do czarnej listy
        bool onBlacklist = await db.SongBlacklists.AnyAsync(b => b.SongId == song.Id);
        Assert.True(onBlacklist);

        // Sprawdzenie usunięcia z zielonej listy
        bool onGreenlist = await db.SongGreenlists.AnyAsync(g => g.SongId == song.Id);
        Assert.False(onGreenlist);
    }

    [Fact]
    public async Task AddToBlacklist_WhenSongAlreadyBlacklisted_ReturnsConflict()
    {
        // Arrange
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        var (_, mod, artist, song) = await SeedBasicDataAsync(db, 1, 202);

        db.SongBlacklists.Add(new SongBlacklist { Id = 10, SongId = song.Id, ArtistId = artist.Id, ModId = mod.Id });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.AddToBlacklist(new ModerationController.AddSongToBlacklistDto(SongId: song.Id, ArtistId: null));

        // Assert
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task AddToBlacklist_WhenSongNotFound_ReturnsNotFound()
    {
        // Arrange
        var (controller, _) = CreateController(Guid.NewGuid().ToString());

        // Act
        var result = await controller.AddToBlacklist(new ModerationController.AddSongToBlacklistDto(SongId: 99999, ArtistId: null));

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RemoveFromBlacklist_WhenExists_RemovesEntry()
    {
        // Arrange
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        var (_, mod, artist, song) = await SeedBasicDataAsync(db, 1, 203);

        var blacklistEntry = new SongBlacklist { Id = 33, SongId = song.Id, ArtistId = artist.Id, ModId = mod.Id };
        db.SongBlacklists.Add(blacklistEntry);
        await db.SaveChangesAsync();

        // Act - usuwamy podając ID utworu lub ID wpisu
        var result = await controller.RemoveFromBlacklist(song.Id);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        bool exists = await db.SongBlacklists.AnyAsync(b => b.SongId == song.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task RemoveFromBlacklist_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var (controller, _) = CreateController(Guid.NewGuid().ToString());

        // Act
        var result = await controller.RemoveFromBlacklist(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetGreenlistedSongs_ReturnsPagedList_AndFiltersBySearch()
    {
        // Arrange
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        var (_, mod, artist, song1) = await SeedBasicDataAsync(db, 1, 301);

        db.SongGreenlists.Add(new SongGreenlist { Id = 1, SongId = song1.Id, ArtistId = artist.Id, ModId = mod.Id });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.GetGreenlistedSongs(search: "Explicit", page: 1, pageSize: 10);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<ModerationController.GreenlistPagedResponseDto>(ok.Value);
        Assert.Equal(1, paged.Total);
        Assert.Single(paged.Items);
    }

    [Fact]
    public async Task AddSongToGreenlist_WhenValid_AddsEntry()
    {
        // Arrange
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        var (_, _, _, song) = await SeedBasicDataAsync(db, 1, 302);

        // Act
        var result = await controller.AddSongToGreenlist(new ModerationController.AddSongToGreenlistDto(SongId: song.Id));

        // Assert
        Assert.IsType<OkObjectResult>(result);
        bool isGreen = await db.SongGreenlists.AnyAsync(g => g.SongId == song.Id);
        Assert.True(isGreen);
    }

    [Fact]
    public async Task AddSongToGreenlist_WhenSongIsBlacklisted_ReturnsBadRequest()
    {
        // Arrange
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        var (_, mod, artist, song) = await SeedBasicDataAsync(db, 1, 303);

        db.SongBlacklists.Add(new SongBlacklist { Id = 15, SongId = song.Id, ArtistId = artist.Id, ModId = mod.Id });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.AddSongToGreenlist(new ModerationController.AddSongToGreenlistDto(SongId: song.Id));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task AddSongToGreenlist_WhenAlreadyGreenlisted_ReturnsConflict()
    {
        // Arrange
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        var (_, mod, artist, song) = await SeedBasicDataAsync(db, 1, 304);

        db.SongGreenlists.Add(new SongGreenlist { Id = 16, SongId = song.Id, ArtistId = artist.Id, ModId = mod.Id });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.AddSongToGreenlist(new ModerationController.AddSongToGreenlistDto(SongId: song.Id));

        // Assert
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task RemoveFromGreenlist_WhenExists_RemovesEntry()
    {
        // Arrange
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        var (_, mod, artist, song) = await SeedBasicDataAsync(db, 1, 305);

        db.SongGreenlists.Add(new SongGreenlist { Id = 17, SongId = song.Id, ArtistId = artist.Id, ModId = mod.Id });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.RemoveFromGreenlist(song.Id);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        bool exists = await db.SongGreenlists.AnyAsync(g => g.SongId == song.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task ArtistBlacklist_Add_Conflict_And_Remove_Flow()
    {
        // Arrange
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        var (_, _, artist, _) = await SeedBasicDataAsync(db, 1, 401);

        // 1. Dodanie wykonawcy do czarnej listy
        var addResult = await controller.AddArtistToBlacklist(artist.Id);
        Assert.IsType<OkObjectResult>(addResult);
        Assert.True(await db.ArtistBlacklists.AnyAsync(a => a.ArtistId == artist.Id));

        // 2. Próba ponownego dodania -> 409 Conflict
        var conflictResult = await controller.AddArtistToBlacklist(artist.Id);
        Assert.IsType<ConflictObjectResult>(conflictResult);

        // 3. Sprawdzenie pobierania listy
        var listResult = await controller.GetBlacklistedArtists(search: "Bad", page: 1, pageSize: 10);
        var okList = Assert.IsType<OkObjectResult>(listResult);
        var paged = Assert.IsType<ModerationController.BlacklistedArtistsPagedResponseDto>(okList.Value);
        Assert.Equal(1, paged.Total);

        // 4. Usunięcie wykonawcy z czarnej listy
        var removeResult = await controller.RemoveArtistFromBlacklist(artist.Id);
        Assert.IsType<OkObjectResult>(removeResult);
        Assert.False(await db.ArtistBlacklists.AnyAsync(a => a.ArtistId == artist.Id));
    }

    [Fact]
    public void ModerationController_HasAuthorizeAttribute_WithModAdminHeadAdminRoles()
    {
        // Act
        var authAttribute = typeof(ModerationController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

        // Assert
        Assert.NotNull(authAttribute);
        Assert.NotNull(authAttribute.Roles);
        Assert.Contains("mod", authAttribute.Roles);
        Assert.Contains("admin", authAttribute.Roles);
        Assert.Contains("head_admin", authAttribute.Roles);
    }
}
