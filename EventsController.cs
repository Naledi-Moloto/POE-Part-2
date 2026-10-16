using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private static readonly string[] ValidEventTypes = { "Run", "Walk", "Cycle" };

    // A "recipe" for turning an Event into an EventDto. EF Core can translate it to SQL
    // (including the joins to Organiser and Venue), and we only write it once.
    private static readonly Expression<Func<Event, EventDto>> ToEventDto = e => new EventDto
    {
        EventID = e.EventID,
        OrganiserID = e.OrganiserID,
        OrganiserName = e.Organiser.FullName,
        VenueID = e.VenueID,
        VenueName = e.Venue.VenueName,
        City = e.Venue.City,
        Province = e.Venue.Province,
        EventName = e.EventName,
        EventType = e.EventType,
        EventDate = e.EventDate,
        StartTime = e.StartTime,
        DistanceKM = e.DistanceKM,
        Description = e.Description,
        CreatedAt = e.CreatedAt
    };

    private readonly AppDbContext _context;

    public EventsController(AppDbContext context)
    {
        _context = context;
    }

    // ==================== EVENTS ====================

    /// <summary>
    /// Returns all events, soonest first. Public: no login needed.
    /// </summary>
    /// <returns>A list of events including their venue (location) details.</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents()
    {
        var events = await _context.Events
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.StartTime)
            .Select(ToEventDto)
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// Returns the full details of one event. Public: no login needed.
    /// </summary>
    /// <param name="id">The event's ID.</param>
    /// <returns>The event, or 404 if it doesn't exist.</returns>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int id)
    {
        var eventDto = await GetEventDtoAsync(id);

        if (eventDto == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        return Ok(eventDto);
    }

    /// <summary>
    /// Creates a new event owned by the logged-in Organiser.
    /// </summary>
    /// <param name="dto">Name, date, start time, venue ID and event type (Run, Walk or Cycle) are required. Description and distance are optional.</param>
    /// <returns>The newly created event.</returns>
    [HttpPost]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEvent(SaveEventDto dto)
    {
        var eventType = ValidEventTypes.FirstOrDefault(
            t => t.Equals(dto.EventType, StringComparison.OrdinalIgnoreCase));

        if (eventType == null)
        {
            return BadRequest(new { message = "EventType must be Run, Walk or Cycle." });
        }

        var venueId = dto.VenueId!.Value;
        if (!await _context.Venues.AnyAsync(v => v.VenueID == venueId))
        {
            return BadRequest(new { message = "The selected venue does not exist." });
        }

        var newEvent = new Event
        {
            OrganiserID = User.GetUserId(),
            VenueID = venueId,
            EventName = dto.EventName.Trim(),
            EventType = eventType,
            EventDate = dto.EventDate!.Value,
            StartTime = dto.StartTime!.Value,
            DistanceKM = dto.DistanceKM,
            Description = dto.Description
        };

        _context.Events.Add(newEvent);
        await _context.SaveChangesAsync();

        var created = await GetEventDtoAsync(newEvent.EventID);

        // 201 Created, plus a Location header pointing at GET /api/events/{id}
        return CreatedAtAction(nameof(GetEvent), new { id = newEvent.EventID }, created);
    }

    /// <summary>
    /// Updates an event. Only the Organiser who created it may do this.
    /// </summary>
    /// <param name="id">The event's ID.</param>
    /// <param name="dto">The new event details (same fields as when creating).</param>
    /// <returns>The updated event.</returns>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(int id, SaveEventDto dto)
    {
        var existing = await _context.Events.FindAsync(id);

        if (existing == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        if (existing.OrganiserID != User.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only edit events that you created." });
        }

        var eventType = ValidEventTypes.FirstOrDefault(
            t => t.Equals(dto.EventType, StringComparison.OrdinalIgnoreCase));

        if (eventType == null)
        {
            return BadRequest(new { message = "EventType must be Run, Walk or Cycle." });
        }

        var venueId = dto.VenueId!.Value;
        if (!await _context.Venues.AnyAsync(v => v.VenueID == venueId))
        {
            return BadRequest(new { message = "The selected venue does not exist." });
        }

        existing.VenueID = venueId;
        existing.EventName = dto.EventName.Trim();
        existing.EventType = eventType;
        existing.EventDate = dto.EventDate!.Value;
        existing.StartTime = dto.StartTime!.Value;
        existing.DistanceKM = dto.DistanceKM;
        existing.Description = dto.Description;

        await _context.SaveChangesAsync();

        return Ok(await GetEventDtoAsync(id));
    }

    /// <summary>
    /// Deletes an event and its categories. Only the Organiser who created it may do this,
    /// and only while nobody is enrolled.
    /// </summary>
    /// <param name="id">The event's ID.</param>
    /// <returns>204 with no body when deleted.</returns>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        // Include the categories so they are deleted together with the event
        var existing = await _context.Events
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventID == id);

        if (existing == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        if (existing.OrganiserID != User.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only delete events that you created." });
        }

        // Matches the Part 1 database rule: enrolments block the delete
        bool hasEnrolments = await _context.Enrolments.AnyAsync(en => en.Category.EventID == id);
        if (hasEnrolments)
        {
            return Conflict(new { message = "This event has enrolments and cannot be deleted." });
        }

        _context.Events.Remove(existing);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // ==================== CATEGORIES OF AN EVENT ====================

    /// <summary>
    /// Returns all categories available for an event. Public: no login needed.
    /// </summary>
    /// <param name="id">The event's ID.</param>
    /// <returns>A list of categories (for example Under 20, 10km, 21km).</returns>
    [HttpGet("{id:int}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategories(int id)
    {
        if (!await _context.Events.AnyAsync(e => e.EventID == id))
        {
            return NotFound(new { message = "Event not found." });
        }

        var categories = await _context.Categories
            .Where(c => c.EventID == id)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();

        return Ok(categories.Select(c => c.ToDto()).ToList());
    }

    /// <summary>
    /// Adds a new age or distance category to an event. Only the event's own Organiser may do this.
    /// </summary>
    /// <param name="id">The event's ID.</param>
    /// <param name="dto">Category name, distance, optional minimum age and entry fee.</param>
    /// <returns>The newly created category.</returns>
    [HttpPost("{id:int}/categories")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCategory(int id, SaveCategoryDto dto)
    {
        var existing = await _context.Events.FindAsync(id);

        if (existing == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        if (existing.OrganiserID != User.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only add categories to events that you created." });
        }

        var category = new Category
        {
            EventID = id,
            CategoryName = dto.CategoryName.Trim(),
            DistanceKM = dto.DistanceKM,
            MinAge = dto.MinAge,
            EntryFee = dto.EntryFee
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, category.ToDto());
    }
    // ==================== ENROLMENTS AND RESULTS OF AN EVENT ====================

    /// <summary>
    /// Returns all enrolments for an event. Only the event's own Organiser may do this.
    /// </summary>
    /// <param name="id">The event's ID.</param>
    /// <returns>A list of enrolments, each showing the participant, category and bib number.</returns>
    [HttpGet("{id:int}/enrolments")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventEnrolments(int id)
    {
        var existing = await _context.Events.FindAsync(id);

        if (existing == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        if (existing.OrganiserID != User.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only view enrolments for events that you created." });
        }

        var enrolments = await _context.Enrolments
            .Where(e => e.Category.EventID == id)
            .OrderBy(e => e.Category.CategoryName)
            .ThenBy(e => e.Participant.FullName)
            .Select(Mappers.ToEnrolmentDto)
            .ToListAsync();

        return Ok(enrolments);
    }

    /// <summary>
    /// Returns all captured results for an event. Only the event's own Organiser may do this.
    /// </summary>
    /// <param name="id">The event's ID.</param>
    /// <returns>A list of results ordered by finishing position (those without a position last).</returns>
    [HttpGet("{id:int}/results")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventResults(int id)
    {
        var existing = await _context.Events.FindAsync(id);

        if (existing == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        if (existing.OrganiserID != User.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only view results for events that you created." });
        }

        var results = await _context.Results
            .Where(r => r.Enrolment.Category.EventID == id)
            .OrderBy(r => r.Position == null)
            .ThenBy(r => r.Position)
            .Select(Mappers.ToResultDto)
            .ToListAsync();

        return Ok(results);
    }

    // ==================== HELPER ====================

    private async Task<EventDto?> GetEventDtoAsync(int id)
    {
        return await _context.Events
            .Where(e => e.EventID == id)
            .Select(ToEventDto)
            .FirstOrDefaultAsync();
    }
}
