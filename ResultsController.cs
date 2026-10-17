using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/results")]
public class ResultsController : ControllerBase
{
    private static readonly string[] ValidStatuses = { "Finished", "DNF", "DNS" };

    private readonly AppDbContext _context;

    public ResultsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Captures a result (finish time or distance covered, and position) for an enrolment.
    /// Only the Organiser who owns the event may do this.
    /// </summary>
    /// <param name="dto">
    /// The enrolment ID and status (Finished, DNF or DNS).
    /// Finished needs a finish time. DNF cannot have a finish time. DNS cannot have a time or distance.
    /// Only Finished results can have a position.
    /// </param>
    /// <returns>The newly captured result.</returns>
    [HttpPost]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateResult(CreateResultDto dto)
    {
        var status = NormaliseStatus(dto.Status);

        if (status == null)
        {
            return BadRequest(new { message = "Status must be Finished, DNF or DNS." });
        }

        var ruleError = CheckResultRules(status, dto);

        if (ruleError != null)
        {
            return BadRequest(new { message = ruleError });
        }

        var enrolmentId = dto.EnrolmentId!.Value;

        var enrolment = await _context.Enrolments
            .Include(e => e.Category)
            .ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentID == enrolmentId);

        if (enrolment == null)
        {
            return NotFound(new { message = "Enrolment not found." });
        }

        if (enrolment.Category.Event.OrganiserID != User.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only capture results for events that you created." });
        }

        if (enrolment.Status == "Cancelled")
        {
            return BadRequest(new { message = "Cannot capture a result for a cancelled enrolment." });
        }

        if (await _context.Results.AnyAsync(r => r.EnrolmentID == enrolmentId))
        {
            return Conflict(new { message = "A result has already been captured for this enrolment." });
        }

        var result = new Result
        {
            EnrolmentID = enrolmentId,
            CapturedByOrganiserID = User.GetUserId(),
            FinishTime = dto.FinishTime,
            DistanceCoveredKM = dto.DistanceCoveredKM,
            Position = dto.Position,
            Status = status
        };

        _context.Results.Add(result);
        await _context.SaveChangesAsync();

        var created = await _context.Results
            .Where(r => r.ResultID == result.ResultID)
            .Select(Mappers.ToResultDto)
            .FirstOrDefaultAsync();

        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>
    /// Updates a previously captured result (for example to correct a time or position).
    /// Only the Organiser who owns the event may do this.
    /// </summary>
    /// <param name="id">The result's ID.</param>
    /// <param name="dto">The corrected status, finish time, distance covered and position.</param>
    /// <returns>The updated result.</returns>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateResult(int id, SaveResultDto dto)
    {
        // Load the result together with its event, so we can check who owns it
        var result = await _context.Results
            .Include(r => r.Enrolment)
            .ThenInclude(e => e.Category)
            .ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(r => r.ResultID == id);

        if (result == null)
        {
            return NotFound(new { message = "Result not found." });
        }

        if (result.Enrolment.Category.Event.OrganiserID != User.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only edit results for events that you created." });
        }

        var status = NormaliseStatus(dto.Status);

        if (status == null)
        {
            return BadRequest(new { message = "Status must be Finished, DNF or DNS." });
        }

        var ruleError = CheckResultRules(status, dto);

        if (ruleError != null)
        {
            return BadRequest(new { message = ruleError });
        }

        result.FinishTime = dto.FinishTime;
        result.DistanceCoveredKM = dto.DistanceCoveredKM;
        result.Position = dto.Position;
        result.Status = status;

        await _context.SaveChangesAsync();

        var updated = await _context.Results
            .Where(r => r.ResultID == id)
            .Select(Mappers.ToResultDto)
            .FirstOrDefaultAsync();

        return Ok(updated);
    }

    /// <summary>
    /// Returns all of the logged-in Participant's own results, newest event first.
    /// </summary>
    /// <returns>A list of the participant's own results.</returns>
    [HttpGet("mine")]
    [Authorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyResults()
    {
        var participantId = User.GetUserId();

        var results = await _context.Results
            .Where(r => r.Enrolment.ParticipantID == participantId)
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .Select(Mappers.ToResultDto)
            .ToListAsync();

        return Ok(results);
    }

    // ==================== HELPERS ====================

    // Accepts "dnf", "DNF", "Dnf"... and returns the correct spelling, or null if invalid
    private static string? NormaliseStatus(string status)
    {
        return ValidStatuses.FirstOrDefault(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));
    }

    // Returns an error message if the combination of fields doesn't make sense, otherwise null
    private static string? CheckResultRules(string status, SaveResultDto dto)
    {
        if (status == "Finished" && dto.FinishTime == null)
        {
            return "A finished result needs a finish time.";
        }

        if (status == "DNF" && dto.FinishTime != null)
        {
            return "A DNF result cannot have a finish time.";
        }

        if (status == "DNS" && (dto.FinishTime != null || dto.DistanceCoveredKM != null))
        {
            return "A DNS result cannot have a finish time or a distance covered.";
        }

        if (status != "Finished" && dto.Position != null)
        {
            return "Only finished results can have a position.";
        }

        return null;
    }
}
