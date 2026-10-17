using Microsoft.EntityFrameworkCore;
using schoolmusic_backend.Models;

namespace schoolmusic_backend.Services;

internal static class SpotifyTrackImporter
{
    public static async Task<Song?> GetOrImportAsync(schoolmusicContext context, ISongSourceService songSource, string spotifyId)
    {
        var existingSong = await context.Songs
            .Include(song => song.Artist)
            .FirstOrDefaultAsync(song => song.SpotifyId == spotifyId);
        if (existingSong != null)
        {
            return existingSong;
        }

        var externalTrack = await songSource.GetTrackByIdAsync(spotifyId);
        if (externalTrack == null)
        {
            return null;
        }

        var artistName = externalTrack.ArtistName.Trim();
        var artist = await context.Artists
            .FirstOrDefaultAsync(candidate => candidate.Name.ToLower() == artistName.ToLower());

        if (artist == null)
        {
            artist = new Artist { Name = artistName, Source = "spotify" };
            context.Artists.Add(artist);
        }

        var song = new Song
        {
            SpotifyId = externalTrack.ExternalId,
            Source = "spotify",
            Title = externalTrack.Title,
            Artist = artist,
            Cover = externalTrack.CoverUrl,
            DurationMs = externalTrack.DurationMs,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Songs.Add(song);
        await context.SaveChangesAsync();
        return song;
    }
}
