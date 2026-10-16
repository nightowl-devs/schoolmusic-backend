using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolmusic_backend.Controllers;
using schoolmusic_backend.Models;

namespace schoolmusic_backend.Tests;

public class AdminTracksControllerTests
{
    private schoolmusicContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<schoolmusicContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new schoolmusicContext(options);
    }

    private (AdminTracksController controller, schoolmusicContext context) CreateController(string dbName)
    {
        var context = CreateInMemoryContext(dbName);
        var controller = new AdminTracksController(context);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "admin")
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

    [Fact]
    public async Task GetAdminTracks_ReturnsBothActiveAndInactive_WhenRequested()
    {
        var (controller, context) = CreateController(nameof(GetAdminTracks_ReturnsBothActiveAndInactive_WhenRequested));

        var artist = new Artist { Id = 1, Name = "Artist 1", Source = "other" };
        context.Artists.Add(artist);
        context.Songs.AddRange(
            new Song { Id = 1, Title = "Active Song", ArtistId = 1, Artist = artist, DurationMs = 180000, Genre = "Rock", Source = "other", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Song { Id = 2, Title = "Inactive Song", ArtistId = 1, Artist = artist, DurationMs = 200000, Genre = "Pop", Source = "other", IsActive = false, CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        // 1. Wszystkie utwory (isActive = null)
        var allResult = await controller.GetAdminTracks();
        var okAll = Assert.IsType<OkObjectResult>(allResult);
        var allDto = Assert.IsType<AdminTracksPagedResponseDto>(okAll.Value);
        Assert.Equal(2, allDto.Total);

        // 2. Tylko nieaktywne (isActive = false)
        var inactiveResult = await controller.GetAdminTracks(isActive: false);
        var okInactive = Assert.IsType<OkObjectResult>(inactiveResult);
        var inactiveDto = Assert.IsType<AdminTracksPagedResponseDto>(okInactive.Value);
        Assert.Single(inactiveDto.Items);
        Assert.Equal("Inactive Song", inactiveDto.Items[0].Title);
        Assert.False(inactiveDto.Items[0].IsActive);
    }

    [Fact]
    public async Task GetAdminTrackById_WhenExists_ReturnsSongDetails()
    {
        var (controller, context) = CreateController(nameof(GetAdminTrackById_WhenExists_ReturnsSongDetails));

        var artist = new Artist { Id = 1, Name = "Queen", Source = "other" };
        var song = new Song { Id = 10, Title = "Bohemian Rhapsody", ArtistId = 1, Artist = artist, DurationMs = 354000, Genre = "Rock", Source = "other", IsActive = true, CreatedAt = DateTime.UtcNow };

        context.Artists.Add(artist);
        context.Songs.Add(song);
        await context.SaveChangesAsync();

        var result = await controller.GetAdminTrackById(10);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<AdminTrackItemDto>(okResult.Value);

        Assert.Equal(10, dto.Id);
        Assert.Equal("Bohemian Rhapsody", dto.Title);
        Assert.Equal("Queen", dto.ArtistName);
    }

    [Fact]
    public async Task GetAdminTrackById_WhenNotFound_ReturnsNotFound()
    {
        var (controller, _) = CreateController(nameof(GetAdminTrackById_WhenNotFound_ReturnsNotFound));

        var result = await controller.GetAdminTrackById(999);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CreateTrack_WhenValid_CreatesArtistAndSong_ReturnsCreatedAtAction()
    {
        var (controller, context) = CreateController(nameof(CreateTrack_WhenValid_CreatesArtistAndSong_ReturnsCreatedAtAction));

        var dto = new CreateTrackDto(
            Title: "Clair de Lune",
            ArtistName: "Claude Debussy",
            Genre: "Classical",
            DurationMs: 300000,
            SongUrl: "https://archive.org/debussy.mp3",
            Cover: "https://example.com/cover.jpg",
            Source: "other"
        );

        var result = await controller.CreateTrack(dto);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var responseDto = Assert.IsType<AdminTrackItemDto>(createdResult.Value);

        Assert.Equal("Clair de Lune", responseDto.Title);
        Assert.Equal("Claude Debussy", responseDto.ArtistName);
        Assert.True(responseDto.IsActive);

        // Weryfikacja w bazie
        var savedSong = await context.Songs.Include(s => s.Artist).FirstOrDefaultAsync(s => s.Id == responseDto.Id);
        Assert.NotNull(savedSong);
        Assert.Equal("Clair de Lune", savedSong.Title);
        Assert.Equal("Claude Debussy", savedSong.Artist.Name);
    }

    [Fact]
    public async Task CreateTrack_WhenTitleOrArtistMissing_ReturnsBadRequest()
    {
        var (controller, _) = CreateController(nameof(CreateTrack_WhenTitleOrArtistMissing_ReturnsBadRequest));

        var dtoEmptyTitle = new CreateTrackDto("", "Artist", "Pop", 1000, null, null);
        var res1 = await controller.CreateTrack(dtoEmptyTitle);
        Assert.IsType<BadRequestObjectResult>(res1);

        var dtoEmptyArtist = new CreateTrackDto("Title", "   ", "Pop", 1000, null, null);
        var res2 = await controller.CreateTrack(dtoEmptyArtist);
        Assert.IsType<BadRequestObjectResult>(res2);
    }

    [Fact]
    public async Task CreateTrack_WhenDurationInvalid_ReturnsBadRequest()
    {
        var (controller, _) = CreateController(nameof(CreateTrack_WhenDurationInvalid_ReturnsBadRequest));

        var dtoInvalidDuration = new CreateTrackDto("Title", "Artist", "Pop", -5, null, null);
        var res = await controller.CreateTrack(dtoInvalidDuration);
        Assert.IsType<BadRequestObjectResult>(res);
    }

    [Fact]
    public async Task UpdateTrack_WhenValid_UpdatesSongAndChangesArtist()
    {
        var (controller, context) = CreateController(nameof(UpdateTrack_WhenValid_UpdatesSongAndChangesArtist));

        var artist1 = new Artist { Id = 1, Name = "Old Artist", Source = "other" };
        var song = new Song { Id = 5, Title = "Old Title", ArtistId = 1, Artist = artist1, DurationMs = 200000, Genre = "Jazz", Source = "other", IsActive = true, CreatedAt = DateTime.UtcNow };

        context.Artists.Add(artist1);
        context.Songs.Add(song);
        await context.SaveChangesAsync();

        var updateDto = new UpdateTrackDto(
            Title: "New Title",
            ArtistName: "New Brand Artist",
            Genre: "Blues",
            DurationMs: 250000,
            SongUrl: "https://new.url",
            Cover: null,
            Source: null,
            SpotifyId: null,
            IsActive: null
        );

        var result = await controller.UpdateTrack(5, updateDto);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<AdminTrackItemDto>(okResult.Value);

        Assert.Equal("New Title", dto.Title);
        Assert.Equal("New Brand Artist", dto.ArtistName);
        Assert.Equal("Blues", dto.Genre);
        Assert.Equal(250000, dto.DurationMs);

        // Weryfikacja bazy
        var updated = await context.Songs.Include(s => s.Artist).FirstAsync(s => s.Id == 5);
        Assert.Equal("New Title", updated.Title);
        Assert.Equal("New Brand Artist", updated.Artist.Name);
    }

    [Fact]
    public async Task UpdateTrack_WhenNotFound_ReturnsNotFound()
    {
        var (controller, _) = CreateController(nameof(UpdateTrack_WhenNotFound_ReturnsNotFound));

        var updateDto = new UpdateTrackDto("Title", null, null, null, null, null, null, null, null);
        var result = await controller.UpdateTrack(999, updateDto);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeleteTrack_SoftDeletes_WhenSongHasReferences()
    {
        var (controller, context) = CreateController(nameof(DeleteTrack_SoftDeletes_WhenSongHasReferences));

        var artist = new Artist { Id = 1, Name = "Artist", Source = "other" };
        var song = new Song { Id = 20, Title = "Played Song", ArtistId = 1, Artist = artist, DurationMs = 150000, Source = "other", IsActive = true, CreatedAt = DateTime.UtcNow };
        var history = new History { Id = 1, SongId = 20, PlayedAt = DateTime.UtcNow, BreakId = 1 };

        context.Artists.Add(artist);
        context.Songs.Add(song);
        context.Histories.Add(history);
        await context.SaveChangesAsync();

        // Próba usunięcia z żądaniem hardDelete = true
        var result = await controller.DeleteTrack(20, hardDelete: true);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("dezaktywowany", okResult.Value!.ToString()!);

        // Weryfikacja, że utwór nadal istnieje w bazie, ale IsActive = false
        var songInDb = await context.Songs.FindAsync(20);
        Assert.NotNull(songInDb);
        Assert.False(songInDb.IsActive);
    }

    [Fact]
    public async Task DeleteTrack_HardDeletes_WhenRequestedAndNoReferences()
    {
        var (controller, context) = CreateController(nameof(DeleteTrack_HardDeletes_WhenRequestedAndNoReferences));

        var artist = new Artist { Id = 1, Name = "Artist", Source = "other" };
        var song = new Song { Id = 30, Title = "Unreferenced Song", ArtistId = 1, Artist = artist, DurationMs = 150000, Source = "other", IsActive = true, CreatedAt = DateTime.UtcNow };

        context.Artists.Add(artist);
        context.Songs.Add(song);
        await context.SaveChangesAsync();

        var result = await controller.DeleteTrack(30, hardDelete: true);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("trwale", okResult.Value!.ToString()!);

        var songInDb = await context.Songs.FindAsync(30);
        Assert.Null(songInDb);
    }

    [Fact]
    public async Task RestoreTrack_SetsIsActiveToTrue()
    {
        var (controller, context) = CreateController(nameof(RestoreTrack_SetsIsActiveToTrue));

        var artist = new Artist { Id = 1, Name = "Artist", Source = "other" };
        var song = new Song { Id = 40, Title = "Disabled Song", ArtistId = 1, Artist = artist, DurationMs = 150000, Source = "other", IsActive = false, CreatedAt = DateTime.UtcNow };

        context.Artists.Add(artist);
        context.Songs.Add(song);
        await context.SaveChangesAsync();

        var result = await controller.RestoreTrack(40);
        var okResult = Assert.IsType<OkObjectResult>(result);

        var songInDb = await context.Songs.FindAsync(40);
        Assert.NotNull(songInDb);
        Assert.True(songInDb.IsActive);
    }
}
