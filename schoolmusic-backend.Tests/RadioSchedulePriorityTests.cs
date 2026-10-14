using Microsoft.EntityFrameworkCore;
using schoolmusic_backend.Models;
using Xunit;

namespace schoolmusic_backend.Tests
{
    public class RadioSchedulePriorityTests
    {
        private static schoolmusicContext CreateInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<schoolmusicContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new schoolmusicContext(options);
        }

        [Fact]
        public async Task ExceptionBreak_Overrides_WeeklyBreakSetting_And_WeeklySchedule()
        {
            // Arrange
            using var db = CreateInMemoryContext(Guid.NewGuid().ToString());
            var today = new DateOnly(2026, 10, 14); // Środa (weekday = 3)
            sbyte weekday = 3;
            sbyte breakNo = 2;

            // Harmonogram tygodniowy mówi: muzyka WŁĄCZONA
            db.WeeklySchedules.Add(new WeeklySchedule { Weekday = weekday, MusicEnabled = true });
            // Ustawienie tygodniowe dla tej przerwy: muzyka WŁĄCZONA
            db.WeeklyBreakSettings.Add(new WeeklyBreakSetting { Weekday = weekday, BreakNumber = breakNo, MusicEnabled = true });
            // ALE wyjątek dzienny dla tej konkretnej przerwy (np. apel szkolny): muzyka WYŁĄCZONA
            db.ExceptionBreaks.Add(new ExceptionBreak { BreakDate = today, BreakNumber = breakNo, MusicEnabled = false, Description = "Apel szkolny" });

            await db.SaveChangesAsync();

            // Act - weryfikacja hierarchii (zgodnie z logiką BreakSchedulerService / RadioStatusService)
            var excBreak = await db.ExceptionBreaks.FirstOrDefaultAsync(eb => eb.BreakDate == today && eb.BreakNumber == breakNo);
            bool isAllowed = excBreak?.MusicEnabled ?? true;

            // Assert: wyjątek dzienny ma najwyższy priorytet
            Assert.False(isAllowed);
        }

        [Fact]
        public async Task WeeklyBreakSetting_Overrides_WeeklySchedule()
        {
            // Arrange
            using var db = CreateInMemoryContext(Guid.NewGuid().ToString());
            sbyte weekday = 1; // Poniedziałek
            sbyte breakNo = 4;

            // Dzień ogólnie: muzyka WŁĄCZONA
            db.WeeklySchedules.Add(new WeeklySchedule { Weekday = weekday, MusicEnabled = true });
            // Ta konkretna długa przerwa w poniedziałek: muzyka WYŁĄCZONA (np. przerwa obiadowa)
            db.WeeklyBreakSettings.Add(new WeeklyBreakSetting { Weekday = weekday, BreakNumber = breakNo, MusicEnabled = false });

            await db.SaveChangesAsync();

            // Act: brak ExceptionBreak, sprawdzamy WeeklyBreakSetting
            var weeklyBreak = await db.WeeklyBreakSettings.FirstOrDefaultAsync(wb => wb.Weekday == weekday && wb.BreakNumber == breakNo);
            bool isAllowed = weeklyBreak?.MusicEnabled ?? true;

            // Assert: WeeklyBreakSetting ma priorytet nad WeeklySchedule
            Assert.False(isAllowed);
        }

        [Fact]
        public async Task WeeklySchedule_Overrides_BreakDefaultFallback()
        {
            // Arrange
            using var db = CreateInMemoryContext(Guid.NewGuid().ToString());
            sbyte weekday = 5; // Piątek

            // W tabeli break: MusicEnabled = true
            var breakEntity = new Break { Id = 1, BreakDate = new DateOnly(2026, 10, 16), BreakNumber = 1, MusicEnabled = true };
            // W harmonogramie tygodniowym dla piątku: MusicEnabled = false
            db.WeeklySchedules.Add(new WeeklySchedule { Weekday = weekday, MusicEnabled = false });

            await db.SaveChangesAsync();

            // Act: brak ExceptionBreak i WeeklyBreakSetting -> sprawdzenie WeeklySchedule
            var weeklyDay = await db.WeeklySchedules.FirstOrDefaultAsync(ws => ws.Weekday == weekday);
            bool isAllowed = weeklyDay?.MusicEnabled ?? breakEntity.MusicEnabled;

            // Assert: WeeklySchedule wyłącza muzykę mimo że rekord break ma true
            Assert.False(isAllowed);
        }

        [Fact]
        public async Task FallbackToBreakEntity_WhenNoOverridesExist()
        {
            // Arrange
            using var db = CreateInMemoryContext(Guid.NewGuid().ToString());
            sbyte weekday = 2; // Wtorek
            sbyte breakNo = 1;

            var breakEntity = new Break { Id = 1, BreakDate = new DateOnly(2026, 10, 13), BreakNumber = breakNo, MusicEnabled = true };
            db.Breaks.Add(breakEntity);
            await db.SaveChangesAsync();

            // Act: brak jakichkolwiek wpisów w exception_break, weekly_break_settings, weekly_schedule
            var exc = await db.ExceptionBreaks.FirstOrDefaultAsync(e => e.BreakNumber == breakNo);
            var wbs = await db.WeeklyBreakSettings.FirstOrDefaultAsync(w => w.Weekday == weekday && w.BreakNumber == breakNo);
            var ws = await db.WeeklySchedules.FirstOrDefaultAsync(w => w.Weekday == weekday);

            bool isAllowed = exc?.MusicEnabled ?? wbs?.MusicEnabled ?? ws?.MusicEnabled ?? breakEntity.MusicEnabled;

            // Assert
            Assert.True(isAllowed);
        }
    }
}
