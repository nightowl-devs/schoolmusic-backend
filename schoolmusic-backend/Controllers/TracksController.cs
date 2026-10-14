using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibVLCSharp.Shared;
using schoolmusic_backend.Models;
using schoolmusic_backend.Extensions;
using static System.Net.WebRequestMethods;
using schoolmusic_backend.Services;

/// <summary>
/// Kontroler API obsługujący operacje związane z utworami: pobieranie i zarządzanie ulubionymi, odtwarzanie plików/URL
/// oraz obliczanie planu przerwy dla kolejki utworów
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TracksController : ControllerBase
{
    private readonly schoolmusicContext _context;
    private readonly IBreakService _breakService;
    private readonly IAudioPlayerService _audioPlayerService;

    public TracksController(
        schoolmusicContext context, 
        IBreakService breakService,
        IAudioPlayerService audioPlayerService)
    {
        _context = context;
        _breakService = breakService;
        _audioPlayerService = audioPlayerService;
    }

    /// <summary>
    /// Zwraca listę ulubionych utworów zalogowanego użytkownika
    /// </summary>
    /// <returns>Task<IActionResult> — 200 OK zawierające obiekt z polem Message i kolekcją Tracks (Id, SpotifyId, Title, Cover,
    /// DurationMs); 401 Unauthorized gdy token JWT jest nieprawidłowy</returns>
    [HttpGet("favourites")]
    public async Task<IActionResult> GetAllFavourites()
    {
        var currentUserId = HttpContext.GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized(new
            {
                Message = "Token JWT niepoprawny"
            });
        }

        //var songs = user.Songs; błąd serializacji przez tabele w MysQL
        var songs = await _context.Users.Where(u => u.Id == currentUserId)
            .SelectMany(u => u.Songs)
            .Select(s => new
            {
                s.Id,
                s.SpotifyId,
                s.Title,
                s.Cover,
                s.DurationMs,
            }).ToListAsync();
        return Ok(new
        {
            Message = "Ulubione utwory użytkownika",
            Tracks = songs
        });

    }


    /// <summary>
    /// Dodawanie po id w bazie danych
    /// </summary>
    [HttpPost("{id:int}/favourites")]
    public async Task<IActionResult> AddToFavourites(int id)
    {
        var currentUserId = HttpContext.GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized(new
            {
                Message = "Token JWT niepoprawny"
            });
        }

        var user = await _context.Users.Include(u => u.Songs)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);
        var song = await _context.Songs.FindAsync(id);

        if (song == null)
        {
            /// TODO ! tutaj trzeba zrobić podłączenie do spotify web api a później dodać do bazy danych
            // await Songs.Add(song);
            // await _context.SaveChangesAsync();
            // na razie NotFound
            return NotFound(new
            {
                Message = "Nie znaleziono piosenki (dołącz SPotify API póxniej"
            });
        }
        if (user == null)
        {
            return NotFound(new
            {
                Message = "Użytkownik nie znaleziony"
            });
        }


        if (!user.Songs.Contains(song))
        {
            user.Songs.Add(song);
            await _context.SaveChangesAsync();
            return Ok(new
            {
                Message = "Poprawnie dodano do ulubionych"
            });
        }
        else
        {
            return Conflict(new
            {
                Message = "Piosenka jest już w ulubionych"
            });
        }

    }

    /// <summary>
    /// Dodaje utwór o podanym identyfikatorze Spotify do listy ulubionych bieżącego użytkownika
    /// </summary>
    /// <returns>200 OK po pomyślnym dodaniu; 401 Unauthorized przy brakującym lub niepoprawnym tokenie; 404 NotFound gdy nie
    /// znaleziono użytkownika lub utworu; 409 Conflict gdy utwór już znajduje się w ulubionych</returns>
    [HttpPost("spotify/{spotifyId}/favourites")]
    public async Task<IActionResult> AddToFavouritesSpotifyId(string spotifyId)
    {
        var currentUserId = HttpContext.GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized(new
            {
                Message = "Token JWT niepoprawny"
            });
        }
        var user = await _context.Users.Include(u => u.Songs)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        var song = await _context.Songs
            .FirstOrDefaultAsync(s => s.SpotifyId == spotifyId);

        if (user == null)
        {
            return NotFound(new
            {
                Message = "Nie znaleziono użytkownika o podanym ID"
            });
        }
        if (song == null)
        {
            return NotFound(new
            {
                Message = "Nie znaleziono piosenki o tym ID"
            });
        }
        if (!user.Songs.Contains(song))
        {
            user.Songs.Add(song);
            await _context.SaveChangesAsync();
            return Ok(new
            {
                Message = "Poprawnie dodano do ulubionych"
            });
        }
        else
        {
            return Conflict(new
            {
                Message = "Piosenka już jest w ulubionych"
            });
        }
    }

    /// <summary>
    /// Usuwa piosenkę z listy ulubionych zalogowanego użytkownika
    /// </summary>
    /// <returns>Zwraca IActionResult: 200 (OK) po pomyślnym usunięciu; 401 (Unauthorized) jeśli brak lub niepoprawny token; 404
    /// (NotFound) jeśli użytkownik, piosenka lub powiązanie piosenki z ulubionymi nie istnieje</returns>
    [HttpDelete("favourites/{id}")]
    public async Task<IActionResult> DeleteFromFavourites(int id)
    {
        var currentUserId = HttpContext.GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized(new
            {
                Message = "Token JWT niepoprawny"
            });
        }

        var user = await _context.Users.Include(u => u.Songs)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);
        var song = await _context.Songs.FirstOrDefaultAsync(s => s.Id == id);
        if (user == null)
        {
            return NotFound(new
            {
                Message = "Nie znaleziono użytkownika"
            });
        }
        if (song == null)
        {
            return NotFound(new
            {
                Message = "Nie znaleziono piosenki"
            });
        }

        if (!user.Songs.Remove(song))
        {
            return NotFound(new
            {
                Message = "Piosenka nie znajdowała sie w ulubionych"
            });
        }
        await _context.SaveChangesAsync();
        return Ok(new
        {
            Message = "Pomyślnie usunięto piosenke z ulubionych"
        });

    }
}