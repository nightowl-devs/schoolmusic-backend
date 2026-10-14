using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolmusic_backend.Extensions;
using schoolmusic_backend.Models;
using schoolmusic_backend.Services;

namespace schoolmusic_backend.Controllers
{
    /// <summary>
    /// Kontroler API obsługujący kolejkę utworów: proponowanie piosenek, głosowanie i cofanie głosów, pobieranie listy
    /// utworów dla przerwy, generowanie harmonogramu przerwy, pobieranie aktualnie odtwarzanego utworu oraz
    /// archiwizacja przerw.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class QueueController : ControllerBase
    {
        private readonly IQueueService _queueService;
        private readonly IRadioStatusService _radioStatusService;
        private readonly IBreakService _breakService;
        private readonly schoolmusicContext _context;

        public QueueController(
            IQueueService queueService,
            IRadioStatusService radioStatusService,
            IBreakService breakService,
            schoolmusicContext context)
        {
            _queueService = queueService;
            _radioStatusService = radioStatusService;
            _breakService = breakService;
            _context = context;
        }

        /// <summary>
        /// Zwraca akcję HTTP z kodem statusu pobranym z obiektu ServiceResult; zawiera Data przy powodzeniu lub obiekt
        /// z polem Message przy niepowodzeniu
        /// </summary>
        /// <remarks>Treść odpowiedzi zależy od wartości pola Success w przekazanym obiekcie.</remarks>
        /// <returns>IActionResult z kodem statusu result.StatusCode; jeśli result.Success jest true, treść odpowiedzi to
        /// result.Data, w przeciwnym razie obiekt zawierający pole Message</returns>
        private IActionResult FromResult<T>(ServiceResult<T> result)
        {
            return StatusCode(result.StatusCode, result.Success ? result.Data : new { Message = result.Message });
        }

        /// <summary>
        /// Pobiera listę utworów zgłoszonych na daną przerwę (Live ranking)
        /// </summary>
        [HttpGet("break/{breakId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetQueueForBreak(int breakId)
        {
            int? currentUserId = HttpContext.GetCurrentUserId();
            var result = await _queueService.GetQueueForBreakAsync(breakId, currentUserId);
            return FromResult(result);
        }

        /// <summary>
        /// Zwraca ułożony przez algorytm harmonogram utworów na daną przerwę (z uwzględnieniem czasu jej trwania)
        /// </summary>
        [HttpGet("break/{breakId:int}/plan")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBreakPlan(int breakId)
        {
            var breakEntity = await _context.Breaks.FindAsync(breakId);
            if (breakEntity == null)
            {
                return NotFound(new { Message = "Nie znaleziono przerwy o podanym identyfikatorze." });
            }

            DateTime end = breakEntity.EndsAt;
            int durationSeconds = (int)(end - breakEntity.StartAt).TotalSeconds;

            if (durationSeconds <= 0)
            {
                durationSeconds = 600; // Domyślnie 10 minut
            }

            var items = await _context.QueueItems
                .Where(q => q.BreakId == breakId && (q.ModerationStatus == "approved" || q.ModerationStatus == null))
                .Include(q => q.Song)
                    .ThenInclude(s => s.Artist)
                .Include(q => q.Votes)
                .ToListAsync();

            var plan = _breakService.CalculateBreak(breakId, durationSeconds, items);
            return Ok(plan);
        }

        /// <summary>
        /// Zwraca utwór aktualnie odtwarzany na radiowęźle (dla ekranu TV i paska odtwarzania w aplikacji)
        /// </summary>
        [HttpGet("now-playing")]
        [AllowAnonymous]
        public async Task<IActionResult> GetNowPlaying()
        {
            var status = await _radioStatusService.GetNowPlayingAsync();
            return Ok(status);
        }

        /// <summary>
        /// Zgłasza piosenkę przez zalogowanego ucznia na najbliższą (lub wybraną) przerwę
        /// </summary>
        [HttpPost("propose")]
        public async Task<IActionResult> ProposeSong([FromBody] ProposeSongDto dto)
        {
            int? currentUserId = HttpContext.GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { Message = "Musisz być zalogowany, aby zgłosić piosenkę." });
            }

            var result = await _queueService.ProposeSongAsync(dto, currentUserId.Value);
            return FromResult(result);
        }

        /// <summary>
        /// Oddaje głos na utwór w kolejce (lub dodaje utwór z greenlisty i oddaje na niego głos)
        /// </summary>
        [HttpPost("vote")]
        public async Task<IActionResult> Vote([FromBody] VoteRequestDto dto)
        {
            int? currentUserId = HttpContext.GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { Message = "Musisz być zalogowany, aby oddać głos." });
            }

            var result = await _queueService.VoteAsync(dto, currentUserId.Value);
            return FromResult(result);
        }

        /// <summary>
        /// Cofa oddany wcześniej głos na utwór w kolejce
        /// </summary>
        [HttpDelete("vote/{queueItemId:int}")]
        public async Task<IActionResult> RemoveVote(int queueItemId)
        {
            int? currentUserId = HttpContext.GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { Message = "Musisz być zalogowany, aby cofnąć głos." });
            }

            var result = await _queueService.RemoveVoteAsync(queueItemId, currentUserId.Value);
            return FromResult(result);
        }

        /// <summary>
        /// Zamyka przerwę, przenosi utwory, które zagrały, do tabeli historii i czyści kolejkę
        /// Dostęp tylko dla moderatorów i administratorów
        /// </summary>
        [HttpPost("break/{breakId:int}/archive")]
        [Authorize(Roles = "mod,admin,head_admin")]
        public async Task<IActionResult> ArchiveBreak(int breakId)
        {
            var result = await _queueService.ArchiveBreakAsync(breakId);
            return FromResult(result);
        }
    }
}
