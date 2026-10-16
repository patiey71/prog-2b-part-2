using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
[Produces("application/json")]
public class EnrolmentsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    public EnrolmentsController(AppDbContext db) => _db = db;

    private static EnrolmentResponse ToResponse(Enrolment e) =>
        new(e.Id, e.EventId, e.Event.Name, e.UserId, e.User.FirstName + " " + e.User.LastName,
            e.CategoryId, e.Category.Name, e.EnrolledAt);

    private IQueryable<Enrolment> Full() =>
        _db.Enrolments.AsNoTracking().Include(e => e.Event).Include(e => e.User).Include(e => e.Category);

    /// <summary>Enter an event by selecting one of its categories (Participant only).</summary>
    /// <response code="201">Enrolment recorded (participant, event and category are linked).</response>
    /// <response code="400">Category does not belong to this event.</response>
    /// <response code="403">Caller is not a Participant.</response>
    /// <response code="404">Event not found.</response>
    /// <response code="409">Already enrolled in this event.</response>
    [HttpPost("events/{eventId:int}/enrolments")]
    [Authorize(Roles = nameof(UserRole.Participant))]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int eventId, [FromBody] EnrolDto dto)
    {
        if (!await _db.Events.AnyAsync(e => e.Id == eventId)) return NotFound();

        var categoryOk = await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId && c.EventId == eventId);
        if (!categoryOk)
            return BadRequest(new { error = "The selected category does not belong to this event." });

        var userId = CurrentUserId;
        if (await _db.Enrolments.AnyAsync(e => e.UserId == userId && e.EventId == eventId))
            return Conflict(new { error = "You are already enrolled in this event." });

        var enrolment = new Enrolment { UserId = userId, EventId = eventId, CategoryId = dto.CategoryId!.Value };
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        var saved = await Full().FirstAsync(e => e.Id == enrolment.Id);
        return Created("/api/enrolments/mine", ToResponse(saved));
    }

    /// <summary>View your own enrolments (Participant only).</summary>
    [HttpGet("enrolments/mine")]
    [Authorize(Roles = nameof(UserRole.Participant))]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        var userId = CurrentUserId;
        var list = await Full().Where(e => e.UserId == userId).OrderBy(e => e.Event.Date).ToListAsync();
        return Ok(list.Select(ToResponse));
    }

    /// <summary>View all enrolments for one of your own events (Organiser only).</summary>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("events/{eventId:int}/enrolments")]
    [Authorize(Roles = nameof(UserRole.Organiser))]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForEvent(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return Forbid();

        var list = await Full().Where(e => e.EventId == eventId).OrderBy(e => e.EnrolledAt).ToListAsync();
        return Ok(list.Select(ToResponse));
    }

    /// <summary>Withdraw from an event you enrolled in (Participant only). Not allowed once a result exists.</summary>
    /// <response code="204">Enrolment removed.</response>
    /// <response code="403">Not a Participant, or the enrolment is not yours.</response>
    /// <response code="404">Enrolment not found.</response>
    /// <response code="409">A result has already been captured.</response>
    [HttpDelete("enrolments/{id:int}")]
    [Authorize(Roles = nameof(UserRole.Participant))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Withdraw(int id)
    {
        var enrolment = await _db.Enrolments.FirstOrDefaultAsync(e => e.Id == id);
        if (enrolment is null) return NotFound();
        if (enrolment.UserId != CurrentUserId) return Forbid();
        if (await _db.Results.AnyAsync(r => r.EnrolmentId == id))
            return Conflict(new { error = "A result has already been captured for this enrolment." });

        _db.Enrolments.Remove(enrolment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
