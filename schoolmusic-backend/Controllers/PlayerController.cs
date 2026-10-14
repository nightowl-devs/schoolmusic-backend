using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using schoolmusic_backend.Services;


namespace schoolmusic_backend.Controllers
{
    public record SetVolumeDto(
        int Volume
    );
    public record PlayFileDto(
        string PathOrUrl,
        string? Title,
        string? Artist
    );

    /// <summary>
    /// Kontroler API do zarządzania odtwarzaczem audio. Udostępnia akcje do pobierania statusu, sterowania głośnością,
    /// wyciszania, pauzowania, wznawiania, zatrzymywania, odtwarzania plików lub URL oraz przesyłania plików.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PlayerController : Controller
    {
        private readonly IAudioPlayerService _playerServices;
        private readonly IBreakSchedulerService _schedulerService;

        public PlayerController(IAudioPlayerService audioPlayerService, IBreakSchedulerService schedulerService)
        {
            _playerServices = audioPlayerService;
            _schedulerService = schedulerService;
        }
        /// <summary>
        /// Przekształca obiekt ServiceResult na odpowiedź HTTP (IActionResult)
        /// </summary>
        private IActionResult FromResult<T>(ServiceResult<T> result)
        {
            return StatusCode(result.StatusCode, result.Success ? result.Data : new
            {
                Message = result.Message
            });
        }

        /// <summary>
        /// Zwraca aktualny status odtwarzania
        /// </summary>
        [HttpGet("status")]
        [AllowAnonymous]
        public IActionResult GetStatus()
        {
            return Ok(_playerServices.GetStatus());
        }

        /// <summary>
        /// Ustawia głośność na przesłaną
        /// </summary>
        [HttpPost("volume")]
        [Authorize(Roles ="mod,admin,head_admin")]
        public IActionResult SetVolume([FromBody] SetVolumeDto dto)
        {
            _playerServices.SetVolume(dto.Volume);
            return Ok(new
            {
                volume = _playerServices.GetVolume(),
                isMuted = _playerServices.GetStatus().IsMuted
            });
        }

        /// <summary>
        /// Zwiększa głośność o podaną
        /// </summary>
        /// <param name="step">O ile ma zwiększyć głośność (domyślnie 5)</param>
        [HttpPost("volume/up")]
        [Authorize(Roles = "mod,admin,head_admin")]
        public IActionResult VolumeUp([FromQuery] int step = 5)
        {
            _playerServices.VolumeUp(step);
            return Ok(new
            {
                volume = _playerServices.GetVolume(),
                isMuted = _playerServices.GetStatus().IsMuted
            });
        }

        /// <summary>
        /// Zmniejsza głośność o podaną
        /// </summary>
        /// <param name="step">O ile ma zmniejszyć głośność (domyślnie 5)</param>
        [HttpPost("volume/down")]
        [Authorize(Roles = "mod,admin,head_admin")]
        public IActionResult VolumeDown([FromQuery] int step = 5)
        {
            _playerServices.VolumeDown(step);
            return Ok(new
            {
                volume = _playerServices.GetVolume(),
                isMuted = _playerServices.GetStatus().IsMuted
            });
        }

        /// <summary>
        /// Wycisza lub odcisza głośnik
        /// </summary>
        [HttpPost("mute")]
        [Authorize(Roles ="mod,admin,head_admin")]
        public IActionResult ToggleMute()
        {
            _playerServices.ToggleMute();
            return Ok(new
            {
                volume = _playerServices.GetVolume(),
                isMuted = _playerServices.GetStatus().IsMuted
            });
        }
        /// <summary>
        /// Odtwarza plik lub url
        /// </summary>
        [HttpPost("play-file")]
        [Authorize(Roles ="mod,admin,head_admin")]
        public async Task<IActionResult> PlayFile([FromBody] PlayFileDto dto)
        {
            var result = await _playerServices.PlayFileOrUrlAsync(dto.PathOrUrl, dto.Title, dto.Artist);
            return FromResult(result);
        }

        /// <summary>
        /// Wstrzymuje odtwarzanie
        /// </summary>
        [HttpPost("pause")]
        [Authorize(Roles = "mod,admin,head_admin")]
        public async Task<IActionResult> Pause()
        {
            var result = await _playerServices.PauseAsync();
            return FromResult(result);
        }

        /// <summary>
        /// Wznawia odtwarzanie
        /// </summary>
        [HttpPost("resume")]
        [Authorize(Roles = "mod,admin,head_admin")]
        public async Task<IActionResult> Resume()
        {
            var result = await _playerServices.ResumeAsync();
            return FromResult(result);
        }

        /// <summary>
        /// Zatrzymuje odtwarzanie przy użyciu serwisu odtwarzacza
        /// </summary>
        /// <returns>IActionResult zawierający wynik asynchronicznej operacji zatrzymania</returns>
        [HttpPost("stop")]
        [Authorize(Roles ="mod,admin,head_admin")]
        public async Task<IActionResult> Stop()
        {
            var result = await _playerServices.StopAsync();
            return FromResult(result);
        }

        /// <summary>
        /// Pomija bieżący odtwarzany utwór i przełącza na kolejny z zaplanowanej kolejki
        /// </summary>
        [HttpPost("skip")]
        [Authorize(Roles = "mod,admin,head_admin")]
        public async Task<IActionResult> Skip()
        {
            var skipped = await _schedulerService.SkipCurrentTrackAsync();
            return Ok(new
            {
                Message = "Pominięto bieżący utwór.",
                Success = skipped,
                PlayerStatus = _playerServices.GetStatus()
            });
        }

        /// <summary>
        /// Przesyła plik audio, zapisuje go na serwerze i uruchamia jego odtwarzanie
        /// </summary>
        /// <remarks>Maksymalny rozmiar żądania: 100 MB. Pliki zapisywane są w katalogu 'recordings'</remarks>
        /// <returns>IActionResult: 400 Bad Request przy braku pliku lub niedozwolonym formacie; w przeciwnym razie wynik
        /// działania odtwarzacza.</returns>
        [HttpPost("upload-and-play")]
        [RequestSizeLimit(100_000_000)] // limit 100MB
        [Authorize(Roles ="mod,admin,head_admin")]
        public async Task<IActionResult> UploadAndPlay(
            IFormFile file,
            [FromForm] string? title = null,
            [FromForm] string? artist = null
        )
        {
            if(file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    Message = "Nie przesłano żadnego pliku"
                });
            }
            string[] allowedExtensions = new string[]
            {
                ".mp3", ".wav", ".ogg", ".flac", ".m4a"
            };
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new
                {
                    Message = "Niedozwolony format pliku. Dozwolone są tylko mp3, wav, ogg, flac, m4a"
                });
            }
            var recordingsDir = Path.Combine(AppContext.BaseDirectory, "recordings");
            if (!Directory.Exists(recordingsDir))
            {
                Directory.CreateDirectory(recordingsDir);
            }
            string safeFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            string filePath = Path.Combine(recordingsDir, safeFileName);
            await using(FileStream stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var effectiveTitle = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title;
            var result = await _playerServices.PlayFileOrUrlAsync(filePath, effectiveTitle, artist ?? "Nieznany wykonawca");
            return FromResult(result);
        }
    }
}
