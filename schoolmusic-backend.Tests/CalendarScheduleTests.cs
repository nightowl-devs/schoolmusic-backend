using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolmusic_backend.Controllers;
using schoolmusic_backend.Models;
using Xunit;

namespace schoolmusic_backend.Tests;

public class CalendarScheduleTests
{
    private static schoolmusicContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<schoolmusicContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new schoolmusicContext(options);
    }

    private static (WeeklyScheduleController controller, schoolmusicContext context) CreateController(string dbName)
    {
        var context = CreateInMemoryContext(dbName);
        var controller = new WeeklyScheduleController(context);
        return (controller, context);
    }

    private static void SeedStandardTemplates(schoolmusicContext db)
    {
        db.BreakTemplates.AddRange(
            new BreakTemplate { Id = 1, BreakNumber = 1, StartsTime = new TimeOnly(7, 45), EndsTime = new TimeOnly(7, 55) },
            new BreakTemplate { Id = 2, BreakNumber = 2, StartsTime = new TimeOnly(8, 40), EndsTime = new TimeOnly(8, 50) },
            new BreakTemplate { Id = 3, BreakNumber = 3, StartsTime = new TimeOnly(9, 35), EndsTime = new TimeOnly(9, 45) },
            new BreakTemplate { Id = 4, BreakNumber = 4, StartsTime = new TimeOnly(10, 30), EndsTime = new TimeOnly(10, 50) },
            new BreakTemplate { Id = 5, BreakNumber = 5, StartsTime = new TimeOnly(11, 35), EndsTime = new TimeOnly(11, 45) },
            new BreakTemplate { Id = 6, BreakNumber = 6, StartsTime = new TimeOnly(12, 30), EndsTime = new TimeOnly(12, 40) },
            new BreakTemplate { Id = 7, BreakNumber = 7, StartsTime = new TimeOnly(13, 25), EndsTime = new TimeOnly(13, 35) },
            new BreakTemplate { Id = 8, BreakNumber = 8, StartsTime = new TimeOnly(14, 20), EndsTime = new TimeOnly(14, 25) }
        );
        db.SaveChanges();
    }

    [Fact]
    public async Task GetCalendarSchedule_WhenFromGreaterThanTo_ReturnsBadRequest()
    {
        // Arrange
        var (controller, _) = CreateController(Guid.NewGuid().ToString());
        var from = new DateOnly(2026, 10, 20);
        var to = new DateOnly(2026, 10, 10);

        // Act
        var result = await controller.GetCalendarSchedule(from, to);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GetCalendarSchedule_WhenRangeExceeds31Days_ReturnsBadRequest()
    {
        // Arrange
        var (controller, _) = CreateController(Guid.NewGuid().ToString());
        var from = new DateOnly(2026, 10, 1);
        var to = new DateOnly(2026, 11, 5); // 36 dni

        // Act
        var result = await controller.GetCalendarSchedule(from, to);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GetCalendarSchedule_DefaultParameters_Returns31DaysStartingToday()
    {
        // Arrange
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        SeedStandardTemplates(db);

        // Act
        var result = await controller.GetCalendarSchedule(null, null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CalendarScheduleResponseDto>(okResult.Value);

        Assert.Equal(31, response.TotalDays);
        Assert.Equal(31, response.Days.Count);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), response.From);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today).AddDays(30), response.To);
    }

    [Fact]
    public async Task GetCalendarSchedule_StandardWeek_DifferentiatesSchoolDaysAndWeekends()
    {
        // Arrange: tydzień od poniedziałku (2026-10-12) do niedzieli (2026-10-18)
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        SeedStandardTemplates(db);

        var from = new DateOnly(2026, 10, 12); // Poniedziałek
        var to = new DateOnly(2026, 10, 18);   // Niedziela

        // Act
        var result = await controller.GetCalendarSchedule(from, to);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CalendarScheduleResponseDto>(okResult.Value);

        Assert.Equal(7, response.TotalDays);
        Assert.Equal(5, response.SchoolDaysCount);
        Assert.Equal(2, response.DaysOffCount);
        Assert.Equal(5, response.MusicActiveDaysCount);

        // Poniedziałek - dzień nauki, muzyka włączona
        var monday = response.Days.First(d => d.Date == from);
        Assert.True(monday.IsSchoolDay);
        Assert.True(monday.IsMusicEnabled);
        Assert.Equal("school_day", monday.DayType);
        Assert.All(monday.Breaks, b => Assert.True(b.IsMusicEnabled));

        // Sobota - weekend, muzyka wyłączona
        var saturday = response.Days.First(d => d.Date == new DateOnly(2026, 10, 17));
        Assert.False(saturday.IsSchoolDay);
        Assert.False(saturday.IsMusicEnabled);
        Assert.Equal("weekend", saturday.DayType);
        Assert.All(saturday.Breaks, b => Assert.False(b.IsMusicEnabled));
    }

    [Fact]
    public async Task GetCalendarSchedule_WhenExceptionDayOff_MarksDayAsDayOffAndMutesBreaks()
    {
        // Arrange: środa 2026-10-14 z dniem wolnym (np. Dzień Edukacji Narodowej)
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        SeedStandardTemplates(db);

        var targetDate = new DateOnly(2026, 10, 14);
        db.ExceptionDays.Add(new ExceptionDay
        {
            Id = 1,
            StartDate = targetDate,
            EndDate = targetDate,
            DayType = "day_off",
            Description = "Dzień Edukacji Narodowej"
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.GetCalendarSchedule(targetDate, targetDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CalendarScheduleResponseDto>(okResult.Value);
        var day = Assert.Single(response.Days);

        Assert.Equal("day_off", day.DayType);
        Assert.Equal("Dzień Edukacji Narodowej", day.StatusDescription);
        Assert.False(day.IsSchoolDay);
        Assert.False(day.IsMusicEnabled);
        Assert.True(day.HasExceptions);
        Assert.NotNull(day.ExceptionDay);
        Assert.Equal("Dzień Edukacji Narodowej", day.ExceptionDay.Description);
        Assert.All(day.Breaks, b =>
        {
            Assert.False(b.IsMusicEnabled);
            Assert.Equal("exception_day", b.RuleSource);
            Assert.Equal("Dzień Edukacji Narodowej", b.Reason);
        });
    }

    [Fact]
    public async Task GetCalendarSchedule_WhenExceptionNoMusic_MarksDayAsNoMusicAndMutesBreaks()
    {
        // Arrange: wtorek 2026-10-13 bez muzyki (np. próbne egzaminy ósmoklasisty)
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        SeedStandardTemplates(db);

        var targetDate = new DateOnly(2026, 10, 13);
        db.ExceptionDays.Add(new ExceptionDay
        {
            Id = 2,
            StartDate = targetDate,
            EndDate = targetDate,
            DayType = "no_music",
            Description = "Egzaminy próbne"
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.GetCalendarSchedule(targetDate, targetDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CalendarScheduleResponseDto>(okResult.Value);
        var day = Assert.Single(response.Days);

        Assert.Equal("no_music", day.DayType);
        Assert.Equal("Egzaminy próbne", day.StatusDescription);
        Assert.True(day.IsSchoolDay); // lekcje trwają, ale bez muzyki!
        Assert.False(day.IsMusicEnabled);
        Assert.True(day.HasExceptions);
        Assert.All(day.Breaks, b =>
        {
            Assert.False(b.IsMusicEnabled);
            Assert.Equal("exception_day", b.RuleSource);
            Assert.Equal("Egzaminy próbne", b.Reason);
        });
    }

    [Fact]
    public async Task GetCalendarSchedule_WhenWeeklyScheduleDayMuted_ReflectsInCalendar()
    {
        // Arrange: środa (weekday = 3) wyciszona w harmonogramie tygodniowym
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        SeedStandardTemplates(db);

        db.WeeklySchedules.Add(new WeeklySchedule
        {
            Weekday = 3,
            MusicEnabled = false
        });
        await db.SaveChangesAsync();

        var wednesday = new DateOnly(2026, 10, 14); // Środa

        // Act
        var result = await controller.GetCalendarSchedule(wednesday, wednesday);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CalendarScheduleResponseDto>(okResult.Value);
        var day = Assert.Single(response.Days);

        Assert.Equal("school_day_no_music", day.DayType);
        Assert.True(day.IsSchoolDay);
        Assert.False(day.IsMusicEnabled);
        Assert.All(day.Breaks, b =>
        {
            Assert.False(b.IsMusicEnabled);
            Assert.Equal("weekly_day", b.RuleSource);
        });
    }

    [Fact]
    public async Task GetCalendarSchedule_WhenWeeklyBreakSettingApplied_MutesOnlySpecificBreak()
    {
        // Arrange: czwartek (weekday = 4) z wyciszoną przerwą nr 4 (np. obiad)
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        SeedStandardTemplates(db);

        db.WeeklyBreakSettings.Add(new WeeklyBreakSetting
        {
            Weekday = 4,
            BreakNumber = 4,
            MusicEnabled = false
        });
        await db.SaveChangesAsync();

        var thursday = new DateOnly(2026, 10, 15); // Czwartek

        // Act
        var result = await controller.GetCalendarSchedule(thursday, thursday);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CalendarScheduleResponseDto>(okResult.Value);
        var day = Assert.Single(response.Days);

        var break4 = day.Breaks.First(b => b.BreakNumber == 4);
        Assert.False(break4.IsMusicEnabled);
        Assert.True(break4.IsOverridden);
        Assert.Equal("weekly_break", break4.RuleSource);

        var break1 = day.Breaks.First(b => b.BreakNumber == 1);
        Assert.True(break1.IsMusicEnabled);
        Assert.False(break1.IsOverridden);
    }

    [Fact]
    public async Task GetCalendarSchedule_WhenExceptionBreakApplied_OverridesWeeklyRules()
    {
        // Arrange: środa z wyłączoną muzyką w tygodniu, ale z wyjątkiem włączającym przerwę nr 2
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        SeedStandardTemplates(db);

        var targetWednesday = new DateOnly(2026, 10, 14);

        db.WeeklySchedules.Add(new WeeklySchedule { Weekday = 3, MusicEnabled = false });
        db.ExceptionBreaks.Add(new ExceptionBreak
        {
            Id = 10,
            BreakDate = targetWednesday,
            BreakNumber = 2,
            MusicEnabled = true,
            Description = "Koncert na przerwie #2"
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.GetCalendarSchedule(targetWednesday, targetWednesday);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CalendarScheduleResponseDto>(okResult.Value);
        var day = Assert.Single(response.Days);

        Assert.True(day.HasExceptions);
        Assert.True(day.IsMusicEnabled); // Przerwa #2 gra!

        var break2 = day.Breaks.First(b => b.BreakNumber == 2);
        Assert.True(break2.IsMusicEnabled);
        Assert.True(break2.IsOverridden);
        Assert.Equal("exception_break", break2.RuleSource);
        Assert.Equal("Koncert na przerwie #2", break2.Reason);

        var break1 = day.Breaks.First(b => b.BreakNumber == 1);
        Assert.False(break1.IsMusicEnabled);
        Assert.Equal("weekly_day", break1.RuleSource);
    }

    [Fact]
    public async Task GetCalendarSchedule_IncludesSpecialSoundEventsForMatchingDays()
    {
        // Arrange: wydarzenie dźwiękowe w piątek 2026-10-16
        var (controller, db) = CreateController(Guid.NewGuid().ToString());
        SeedStandardTemplates(db);

        var targetDate = new DateOnly(2026, 10, 16);
        db.SpecialEvents.Add(new SpecialEvent
        {
            Id = 1,
            Title = "Uroczysty apel - Dzwon Zwycięstwa",
            FileUrl = "https://audio.schoolmusic.local/bell.mp3",
            StartsAt = new DateTime(2026, 10, 16, 11, 45, 0),
            EventStatus = "pending"
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.GetCalendarSchedule(targetDate, targetDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CalendarScheduleResponseDto>(okResult.Value);
        var day = Assert.Single(response.Days);

        var soundEvent = Assert.Single(day.SoundEvents);
        Assert.Equal("Uroczysty apel - Dzwon Zwycięstwa", soundEvent.Title);
        Assert.Equal("pending", soundEvent.EventStatus);
        Assert.Equal("https://audio.schoolmusic.local/bell.mp3", soundEvent.FileUrl);
    }
}
