using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolmusic_backend.Controllers;
using schoolmusic_backend.Models;

namespace schoolmusic_backend.Tests;

public class BreakScheduleDynamicTests
{
    [Fact]
    public async Task Calendar_UsesOnlyConfiguredTemplateCount()
    {
        await using var context = CreateContext();
        context.BreakTemplates.AddRange(
            new BreakTemplate { Id = 1, BreakNumber = 1, StartsTime = new TimeOnly(8, 0), EndsTime = new TimeOnly(8, 10) },
            new BreakTemplate { Id = 2, BreakNumber = 9, StartsTime = new TimeOnly(12, 0), EndsTime = new TimeOnly(12, 15) });
        await context.SaveChangesAsync();

        var controller = new WeeklyScheduleController(context);
        var result = Assert.IsType<OkObjectResult>(
            await controller.GetCalendarSchedule(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 12)));
        var response = Assert.IsType<CalendarScheduleResponseDto>(result.Value);

        Assert.Equal(new sbyte[] { 1, 9 }, response.Days.Single().Breaks.Select(item => item.BreakNumber));
    }

    [Fact]
    public async Task RecessSchedule_ReturnsOnlyBreaksForRequestedDate()
    {
        await using var context = CreateContext();
        var requestedDate = new DateOnly(2026, 10, 12);
        context.Breaks.AddRange(
            MakeBreak(1, requestedDate, 1),
            MakeBreak(2, requestedDate, 9),
            MakeBreak(3, requestedDate.AddDays(1), 1));
        await context.SaveChangesAsync();

        var controller = new global::RecessController(context);
        var result = Assert.IsType<OkObjectResult>(await controller.GetBreaks(requestedDate));
        var breaks = Assert.IsAssignableFrom<IEnumerable<global::RecessController.BreakResponseDto>>(
            result.Value!.GetType().GetProperty("Przerwy")!.GetValue(result.Value));

        Assert.Equal(new[] { 1, 9 }, breaks.Select(item => item.BreakNumber));
    }

    private static schoolmusicContext CreateContext() =>
        new(new DbContextOptionsBuilder<schoolmusicContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Break MakeBreak(int id, DateOnly date, sbyte number) => new()
    {
        Id = id,
        BreakDate = date,
        BreakNumber = number,
        StartAt = date.ToDateTime(new TimeOnly(8, 0)),
        EndsAt = date.ToDateTime(new TimeOnly(8, 10))
    };
}
