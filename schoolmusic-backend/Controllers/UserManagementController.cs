using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolmusic_backend.Extensions;
using schoolmusic_backend.Models;

namespace schoolmusic_backend.Controllers
{
    public record UpdateUserRoleDto(int RankId);

    public record UserListItemDto(
        int Id,
        string UserLogin,
        int RankId,
        string RankName,
        DateTime CreatedAt,
        DateTime? PasswordChangedAt
    );

    /// <summary>
    /// Kontroler API do zarządzania kontami użytkowników i uprawnieniami (dostęp dla administratorów)
    /// </summary>
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class UserManagementController : ControllerBase
    {
        private readonly schoolmusicContext _context;

        public UserManagementController(schoolmusicContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Zwraca stronicowaną listę użytkowników systemu (bez haseł)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "admin,head_admin")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.Users
                .Include(u => u.Rank)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u => u.UserLogin.Contains(search));
            }

            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var users = await query
                .OrderBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListItemDto(
                    u.Id,
                    u.UserLogin,
                    u.RankId,
                    u.Rank.Rank1,
                    u.CreatedAt,
                    u.PasswordChangedAt
                ))
                .ToListAsync();

            return Ok(new
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                Users = users
            });
        }

        /// <summary>
        /// Zmienia rolę (rangę) wskazanego użytkownika
        /// Dostępne wyłącznie dla rangi head_admin
        /// </summary>
        [HttpPut("{id:int}/role")]
        [Authorize(Roles = "head_admin")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateUserRoleDto dto)
        {
            int? currentUserId = HttpContext.GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new { Message = "Nieprawidłowy token uwierzytelniający." });
            }

            if (currentUserId.Value == id)
            {
                return BadRequest(new { Message = "Nie możesz zmienić własnej roli." });
            }

            if (dto.RankId < 1 || dto.RankId > 4)
            {
                return BadRequest(new { Message = "Nieprawidłowy identyfikator rangi (dozwolone wartości: 1 = user, 2 = mod, 3 = admin, 4 = head_admin)." });
            }

            var user = await _context.Users
                .Include(u => u.Rank)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { Message = "Nie znaleziono użytkownika o podanym identyfikatorze." });
            }

            var targetRank = await _context.Ranks.FindAsync(dto.RankId);
            if (targetRank == null)
            {
                return BadRequest(new { Message = "Podana ranga nie istnieje w bazie danych." });
            }

            user.RankId = dto.RankId;

            // Synchronizacja tabeli moderators
            if (dto.RankId >= 2) // mod, admin, head_admin
            {
                bool isModeratorRecord = await _context.Moderators.AnyAsync(m => m.UserId == id);
                if (!isModeratorRecord)
                {
                    _context.Moderators.Add(new Moderator
                    {
                        UserId = id,
                        ArtistsBanned = 0,
                        SongsBanned = 0,
                        SongsApproved = 0
                    });
                }
            }
            else // degradacja do zwykłego użytkownika (1)
            {
                var modRecord = await _context.Moderators.FirstOrDefaultAsync(m => m.UserId == id);
                if (modRecord != null)
                {
                    bool hasDependencies = await _context.QueueItems.AnyAsync(q => q.ModeratorId == modRecord.Id)
                        || await _context.SongBlacklists.AnyAsync(b => b.ModId == modRecord.Id)
                        || await _context.SongGreenlists.AnyAsync(g => g.ModId == modRecord.Id)
                        || await _context.ArtistBlacklists.AnyAsync(a => a.ModeratorId == modRecord.Id)
                        || await _context.BannedUsers.AnyAsync(u => u.ModeratorId == modRecord.Id);

                    if (!hasDependencies)
                    {
                        _context.Moderators.Remove(modRecord);
                    }
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = $"Zaktualizowano rolę użytkownika {user.UserLogin} na {targetRank.Rank1}.",
                UserId = user.Id,
                UserLogin = user.UserLogin,
                RankId = targetRank.Id,
                RankName = targetRank.Rank1
            });
        }
    }
}
