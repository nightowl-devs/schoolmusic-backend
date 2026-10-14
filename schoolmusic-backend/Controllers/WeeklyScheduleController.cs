using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolmusic_backend.Models;

namespace schoolmusic_backend.Controllers
{
    public record UpdateWeeklyDayDto(bool MusicEnabled);
    public record UpdateWeeklyBreakDto(bool MusicEnabled);

    public record WeeklyDayDto(sbyte Weekday, string DayName, bool MusicEnabled);

    public record WeeklyBreakDetailDto(
        sbyte BreakNumber,
        string StartsTime,
        string EndsTime,
        bool MusicEnabled,
        bool IsOverridden
    );

    /// <summary>
    /// Kontroler do zarządzania domyślnym harmonogramem tygodniowym przerw i odtwarzania muzyki
    /// </summary>
    [Route("api/schedule")]
    [ApiController]
    public class WeeklyScheduleController : ControllerBase
    {
        private readonly schoolmusicContext _context;

        private static readonly Dictionary<sbyte, string> DayNames = new()
        {
            { 1, "Poniedziałek" },
            { 2, "Wtorek" },
            { 3, "Środa" },
            { 4, "Czwartek" },
            { 5, "Piątek" },
            { 6, "Sobota" },
            { 7, "Niedziela" }
        };

        public WeeklyScheduleController(schoolmusicContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Zwraca konfigurację muzyki dla wszystkich 7 dni tygodnia
        /// </summary>
        [HttpGet("weekly")]
        [AllowAnonymous]
        public async Task<IActionResult> GetWeeklySchedule()
        {
            var schedules = await _context.WeeklySchedules
                .OrderBy(s => s.Weekday)
                .ToListAsync();

            var result = new List<WeeklyDayDto>();
            for (sbyte day = 1; day <= 7; day++)
            {
                var existing = schedules.FirstOrDefault(s => s.Weekday == day);
                string dayName = DayNames.GetValueOrDefault(day, $"Dzień {day}");
                result.Add(new WeeklyDayDto(day, dayName, existing?.MusicEnabled ?? true));
            }

            return Ok(result);
        }

        /// <summary>
        /// Zmienia flagę włączenia muzyki dla całego danego dnia tygodnia
        /// </summary>
        /// <param name="weekday">Dzień tygodnia (1 = Poniedziałek, 7 = Niedziela)</param>
        /// <param name="dto">Nowy stan włączenia muzyki</param>
        [HttpPut("weekly/{weekday:int}")]
        [Authorize(Roles = "admin,head_admin")]
        public async Task<IActionResult> UpdateWeeklyDay(int weekday, [FromBody] UpdateWeeklyDayDto dto)
        {
            if (weekday < 1 || weekday > 7)
            {
                return BadRequest(new { Message = "Numer dnia tygodnia musi być w przedziale 1–7 (1 = Poniedziałek, 7 = Niedziela)." });
            }

            sbyte day = (sbyte)weekday;
            var schedule = await _context.WeeklySchedules.FindAsync(day);
            if (schedule == null)
            {
                schedule = new WeeklySchedule
                {
                    Weekday = day,
                    MusicEnabled = dto.MusicEnabled
                };
                _context.WeeklySchedules.Add(schedule);
            }
            else
            {
                schedule.MusicEnabled = dto.MusicEnabled;
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                Message = $"Zaktualizowano konfigurację dla dnia {DayNames.GetValueOrDefault(day, day.ToString())}.",
                Weekday = day,
                dto.MusicEnabled
            });
        }

        /// <summary>
        /// Pobiera listę przerw z szablonu dla danego dnia tygodnia wraz ze statusem włączenia muzyki
        /// </summary>
        [HttpGet("weekly/{weekday:int}/breaks")]
        [AllowAnonymous]
        public async Task<IActionResult> GetWeeklyDayBreaks(int weekday)
        {
            if (weekday < 1 || weekday > 7)
            {
                return BadRequest(new { Message = "Numer dnia tygodnia musi być w przedziale 1–7." });
            }

            sbyte day = (sbyte)weekday;
            var daySchedule = await _context.WeeklySchedules.FindAsync(day);
            bool defaultDayEnabled = daySchedule?.MusicEnabled ?? true;

            var templates = await _context.BreakTemplates
                .OrderBy(t => t.BreakNumber)
                .ToListAsync();

            var overrides = await _context.WeeklyBreakSettings
                .Where(w => w.Weekday == day)
                .ToListAsync();

            var result = templates.Select(t =>
            {
                var customSetting = overrides.FirstOrDefault(o => o.BreakNumber == t.BreakNumber);
                bool isOverridden = customSetting != null;
                bool isMusicEnabled = isOverridden ? customSetting!.MusicEnabled : defaultDayEnabled;

                return new WeeklyBreakDetailDto(
                    BreakNumber: t.BreakNumber,
                    StartsTime: t.StartsTime.ToString("HH:mm:ss"),
                    EndsTime: t.EndsTime.ToString("HH:mm:ss"),
                    MusicEnabled: isMusicEnabled,
                    IsOverridden: isOverridden
                );
            });

            return Ok(new
            {
                Weekday = day,
                DayName = DayNames.GetValueOrDefault(day, day.ToString()),
                DayMusicEnabled = defaultDayEnabled,
                Breaks = result
            });
        }

        /// <summary>
        /// Zmienia flagę włączenia muzyki dla konkretnej przerwy w danym dniu tygodnia
        /// </summary>
        [HttpPut("weekly/{weekday:int}/breaks/{breakNo:int}")]
        [Authorize(Roles = "admin,head_admin")]
        public async Task<IActionResult> UpdateWeeklyBreak(int weekday, int breakNo, [FromBody] UpdateWeeklyBreakDto dto)
        {
            if (weekday < 1 || weekday > 7)
            {
                return BadRequest(new { Message = "Numer dnia tygodnia musi być w przedziale 1–7." });
            }

            sbyte day = (sbyte)weekday;
            sbyte breakNumber = (sbyte)breakNo;

            var templateExists = await _context.BreakTemplates.AnyAsync(t => t.BreakNumber == breakNumber);
            if (!templateExists)
            {
                return NotFound(new { Message = $"Szablon przerwy nr {breakNo} nie istnieje." });
            }

            var setting = await _context.WeeklyBreakSettings
                .FirstOrDefaultAsync(w => w.Weekday == day && w.BreakNumber == breakNumber);

            if (setting == null)
            {
                setting = new WeeklyBreakSetting
                {
                    Weekday = day,
                    BreakNumber = breakNumber,
                    MusicEnabled = dto.MusicEnabled
                };
                _context.WeeklyBreakSettings.Add(setting);
            }
            else
            {
                setting.MusicEnabled = dto.MusicEnabled;
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                Message = $"Zaktualizowano przerwę #{breakNo} w dniu {DayNames.GetValueOrDefault(day, day.ToString())}.",
                Weekday = day,
                BreakNumber = breakNo,
                dto.MusicEnabled
            });
        }
    }
}
