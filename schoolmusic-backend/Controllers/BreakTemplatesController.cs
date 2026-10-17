using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolmusic_backend.Models;

namespace schoolmusic_backend.Controllers;

public record BreakTemplateDto(int Id, int BreakNumber, TimeOnly StartsTime, TimeOnly EndsTime);
public record CreateBreakTemplateDto(int BreakNumber, TimeOnly StartsTime, TimeOnly EndsTime);
public record UpdateBreakTemplateDto(int BreakNumber, TimeOnly StartsTime, TimeOnly EndsTime);

[Route("api/breaks/templates")]
[ApiController]
[Authorize(Roles = "admin,head_admin")]
public class BreakTemplatesController(schoolmusicContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BreakTemplateDto>>> GetBreakTemplates()
    {
        var templates = await context.BreakTemplates
            .OrderBy(template => template.BreakNumber)
            .Select(template => new BreakTemplateDto(
                template.Id,
                template.BreakNumber,
                template.StartsTime,
                template.EndsTime))
            .ToListAsync();

        return Ok(templates);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBreakTemplate(CreateBreakTemplateDto dto)
    {
        if (!IsValid(dto.BreakNumber, dto.StartsTime, dto.EndsTime))
        {
            return BadRequest(new { Message = "Numer przerwy musi być dodatni, a godzina końca późniejsza od godziny rozpoczęcia." });
        }

        if (await context.BreakTemplates.AnyAsync(template => template.BreakNumber == dto.BreakNumber))
        {
            return Conflict(new { Message = $"Szablon przerwy nr {dto.BreakNumber} już istnieje." });
        }

        var template = new BreakTemplate
        {
            BreakNumber = (sbyte)dto.BreakNumber,
            StartsTime = dto.StartsTime,
            EndsTime = dto.EndsTime
        };

        context.BreakTemplates.Add(template);
        await context.SaveChangesAsync();

        return Created("/api/breaks/templates", ToDto(template));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateBreakTemplate(int id, UpdateBreakTemplateDto dto)
    {
        if (!IsValid(dto.BreakNumber, dto.StartsTime, dto.EndsTime))
        {
            return BadRequest(new { Message = "Numer przerwy musi być dodatni, a godzina końca późniejsza od godziny rozpoczęcia." });
        }

        var template = await context.BreakTemplates.FindAsync(id);
        if (template == null)
        {
            return NotFound(new { Message = "Nie znaleziono szablonu przerwy o podanym ID." });
        }

        if (await context.BreakTemplates.AnyAsync(other =>
                other.Id != id && other.BreakNumber == dto.BreakNumber))
        {
            return Conflict(new { Message = $"Szablon przerwy nr {dto.BreakNumber} już istnieje." });
        }

        if (template.BreakNumber != dto.BreakNumber &&
            await HasReferencesAsync(template.BreakNumber))
        {
            return Conflict(new { Message = "Nie można zmienić numeru szablonu, ponieważ jest używany przez przerwę lub ustawienie tygodniowe." });
        }

        if (template.BreakNumber != dto.BreakNumber)
        {
            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync()
                : null;
            var updatedTemplate = new BreakTemplate
            {
                Id = template.Id,
                BreakNumber = (sbyte)dto.BreakNumber,
                StartsTime = dto.StartsTime,
                EndsTime = dto.EndsTime
            };

            context.BreakTemplates.Remove(template);
            await context.SaveChangesAsync();
            context.BreakTemplates.Add(updatedTemplate);
            await context.SaveChangesAsync();
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return Ok(ToDto(updatedTemplate));
        }

        template.StartsTime = dto.StartsTime;
        template.EndsTime = dto.EndsTime;
        await context.SaveChangesAsync();

        return Ok(ToDto(template));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBreakTemplate(int id)
    {
        var template = await context.BreakTemplates.FindAsync(id);
        if (template == null)
        {
            return NotFound(new { Message = "Nie znaleziono szablonu przerwy o podanym ID." });
        }

        if (await HasReferencesAsync(template.BreakNumber))
        {
            return Conflict(new { Message = "Nie można usunąć szablonu, ponieważ jest używany przez przerwę lub ustawienie tygodniowe." });
        }

        context.BreakTemplates.Remove(template);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> HasReferencesAsync(sbyte breakNumber) =>
        await context.Breaks.AnyAsync(breakEntity => breakEntity.BreakNumber == breakNumber) ||
        await context.WeeklyBreakSettings.AnyAsync(setting => setting.BreakNumber == breakNumber);

    private static bool IsValid(int breakNumber, TimeOnly startsTime, TimeOnly endsTime) =>
        breakNumber is >= 1 and <= sbyte.MaxValue && endsTime > startsTime;

    private static BreakTemplateDto ToDto(BreakTemplate template) =>
        new(template.Id, template.BreakNumber, template.StartsTime, template.EndsTime);
}
